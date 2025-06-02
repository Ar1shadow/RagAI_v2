using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Postgres;
using Microsoft.KernelMemory.Text;
using Pgvector;

namespace RagAI_v2.MemoryDataBase.Postgres;

/// <summary>
/// Postgres connector for Kernel Memory.
/// Connecteur Postgres pour Kernel Memory.
/// </summary>

public sealed class PostgresService : IMemoryDb, IDisposable, IAsyncDisposable
{
    // Dependencies
    private readonly PostgresClient _db;
    private readonly ITextEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<PostgresService> _log;
    private readonly CustomPostgresConfig _config;

    /// <summary>
    /// Create a new instance of Postgres connector
    /// </summary>
    /// <param name="config">Postgres configuration</param>
    /// <param name="embeddingGenerator">Text embedding generator</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public PostgresService(
        CustomPostgresConfig config,
        ITextEmbeddingGenerator embeddingGenerator,
        ILoggerFactory? loggerFactory = null)
    {
        this._config = config;
        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<PostgresService>();

        this._embeddingGenerator = embeddingGenerator;
        if (this._embeddingGenerator == null)
        {
            throw new PostgresException("Embedding generator not configured");
        }

        // Normalize underscore and check for invalid symbols
        config.TableNamePrefix = NormalizeTableNamePrefix(config.TableNamePrefix);

        this._db = new PostgresClient(config, loggerFactory);
    }

    /// <inheritdoc />
    public async Task CreateIndexAsync(
        string index,
        int vectorSize,
        CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);

        try
        {
            if (await this._db.DoesTableExistAsync(index, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await this._db.CreateTableAsync(index, vectorSize, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            this._log.LogError(e, "DB error while attempting to create index");
            throw new PostgresException("DB error while attempting to create index", e);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<string>();
        try
        {
            var tables = this._db.GetTablesAsync(cancellationToken).ConfigureAwait(false);
            await foreach (string name in tables)
            {
                result.Add(name);
            }
        }
        catch (Exception e)
        {
            this._log.LogError(e, "DB error while fetching the list of indexes");
            throw;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task DeleteIndexAsync(
        string index,
        CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);

        try
        {
            await this._db.DeleteTableAsync(index, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            this._log.LogError(e, "DB error while deleting index");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> UpsertAsync(
        string index,
        MemoryRecord record,
        CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);

        try
        {
            await this._db.UpsertAsync(
                tableName: index,
                PostgresMemoryRecord.FromMemoryRecord(record),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            this._log.LogError(e, "DB error upserting record");
            throw;
        }

        return record.Id;
    }

    // /// <inheritdoc />
    // public async IAsyncEnumerable<(MemoryRecord, double)> GetSimilarListAsync(
    //     string index,
    //     string text,
    //     ICollection<MemoryFilter>? filters = null,
    //     double minRelevance = 0,
    //     int limit = 1,
    //     bool withEmbeddings = false,
    //     [EnumeratorCancellation] CancellationToken cancellationToken = default)
    // {
    //     index = NormalizeIndexName(index);
    //
    //     var (sql, unsafeSqlUserValues) = this.PrepareSql(filters);
    //
    //     Embedding textEmbedding = await this._embeddingGenerator.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
    //
    //     var records = this._db.GetSimilarAsync(
    //         index,
    //         target: new Vector(textEmbedding.Data),
    //         minSimilarity: minRelevance,
    //         filterSql: sql,
    //         sqlUserValues: unsafeSqlUserValues,
    //         limit: limit,
    //         withEmbeddings: withEmbeddings,
    //         cancellationToken: cancellationToken).ConfigureAwait(false);
    //
    //     await foreach ((PostgresMemoryRecord record, double similarity) result in records)
    //     {
    //         yield return (PostgresMemoryRecord.ToMemoryRecord(result.record), result.similarity);
    //     }
    // }
    
    /// <inheritdoc />
    /// <remarks>Implementation par GetHybridListAsync  </remarks>
    public async IAsyncEnumerable<(MemoryRecord, double)> GetSimilarListAsync(
        string index,
        string text,
        ICollection<MemoryFilter>? filters = null,
        double minRelevance = 0,
        int limit = 1,
        bool withEmbeddings = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var records = GetHybridListAsync(index, text, minRelevance, limit, _config.Rrf_K_Vec,_config.Rrf_K_Text, withEmbeddings,
            _config.UserNormalization);
        await foreach ((MemoryRecord record, double score) in records)
            yield return (record, score);
    }
    
    
    /// <remarks>Effectue une recherche hybride RRF en combinant les résultats de similarité sémantique et de recherche plein texte.</remarks>
    /// <Note>minRelevance sert à rien ici à cause de la calculation de RRF</Note>
    public async IAsyncEnumerable<(MemoryRecord, double)> GetHybridListAsync(
        string index,
        string text,
        double minRelevance = 0.0,
        int limit = 10,
        int rrf_K_vec = 60,
        int rrf_K_text = 30,
        bool withEmbeddings = false,
        bool useNormalization = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);

        var (sql, unsafeSqlUserValues) = this.PrepareSql(null);

        var embedding = await this._embeddingGenerator.GenerateEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
        var vector = new Vector(embedding.Data);

        var records = this._db.GetHybridSearchAsync(
            index,
            target:vector,
            textQuery:text,
             rrf_K_vec = 60,
            rrf_K_text = 30,
            limit:limit,
            withEmbeddings:withEmbeddings,
            useNormalization:useNormalization,
            cancellationToken:cancellationToken).ConfigureAwait(false);
        
        await foreach ((PostgresMemoryRecord record, double score) result in records)
        {
            yield return (PostgresMemoryRecord.ToMemoryRecord(result.record), result.score);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MemoryRecord> GetListAsync(
        string index,
        ICollection<MemoryFilter>? filters = null,
        int limit = 1,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);

        var (sql, unsafeSqlUserValues) = this.PrepareSql(filters);

        var records = this._db.GetListAsync(
            index,
            filterSql: sql,
            sqlUserValues: unsafeSqlUserValues,
            limit: limit,
            withEmbeddings: withEmbeddings,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await foreach (PostgresMemoryRecord pgRecord in records)
        {
            yield return PostgresMemoryRecord.ToMemoryRecord(pgRecord);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        string index,
        MemoryRecord record,
        CancellationToken cancellationToken = default)
    {
        index = NormalizeIndexName(index);

        return this._db.DeleteAsync(tableName: index, id: record.Id, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._db?.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await this._db.DisposeAsync().ConfigureAwait(false);
        }
        catch (NullReferenceException)
        {
            // ignore
        }
    }
    

    #region private ================================================================================

    // Note: "_" is allowed in Postgres, but we normalize it to "-" for consistency with other DBs
    private static readonly Regex s_replaceIndexNameCharsRegex = new(@"[\s|\\|/|.|_|:]");
    private const string ValidSeparator = "-";

    private static string NormalizeIndexName(string index)
    {
        ArgumentNullExceptionEx.ThrowIfNullOrWhiteSpace(index, nameof(index), "The index name is empty");
        index = s_replaceIndexNameCharsRegex.Replace(index.Trim().ToLowerInvariant(), ValidSeparator);

        PostgresSchema.ValidateTableName(index);

        return index;
    }

    private static string NormalizeTableNamePrefix(string? name)
    {
        if (name == null) { return string.Empty; }

        name = s_replaceIndexNameCharsRegex.Replace(name.Trim().ToLowerInvariant(), ValidSeparator);
        PostgresSchema.ValidateTableNamePrefix(name);

        return name;
    }

    private (string sql, Dictionary<string, object> unsafeSqlUserValues) PrepareSql(
        ICollection<MemoryFilter>? filters = null)
    {
        var sql = "";
        Dictionary<string, object> unsafeSqlUserValues = [];

        if (filters is not { Count: > 0 })
        {
            return (sql, unsafeSqlUserValues);
        }

        var tagCounter = 0;
        var orConditions = new List<string>();

        foreach (MemoryFilter filter in filters.Where(f => !f.IsEmpty()))
        {
            var andSql = new StringBuilder();
            andSql.AppendLineNix("(");

            if (filter is PostgresMemoryFilter)
            {
                // use PostgresMemoryFilter filtering logic
                throw new NotImplementedException("PostgresMemoryFilter is not supported yet");
            }

            List<string> requiredTags = filter.GetFilters().Select(x => $"{x.Key}{Constants.ReservedEqualsChar}{x.Value}").ToList();

            // TODO: what is this collection for?
            List<string> safeSqlPlaceholders = [];

            if (requiredTags.Count > 0)
            {
                var safeSqlPlaceholder = $"@placeholder{tagCounter++}";
                safeSqlPlaceholders.Add(safeSqlPlaceholder);
                unsafeSqlUserValues[safeSqlPlaceholder] = requiredTags;

                // All tags are required
                //  tags @> ARRAY['user:001', 'type:news', '__document_id:b405']::text[]  <== all tags are required <=== we are using this
                //  tags && ARRAY['user:001', 'type:news', '__document_id:b405']::text[]  <== one tag is sufficient
                // Available syntax:
                //  $"{PostgresSchema.PlaceholdersTags} @> " + safeSqlPlaceholder
                //  $"{PostgresSchema.PlaceholdersTags} @> " + safeSqlPlaceholder + "::text[]"
                //  $"{PostgresSchema.PlaceholdersTags} @> ARRAY[" + safeSqlPlaceholder + "]::text[]"
                andSql.AppendLineNix($"{PostgresSchema.PlaceholdersTags} @> " + safeSqlPlaceholder);
            }

            andSql.AppendLineNix(")");
            orConditions.Add(andSql.ToString());
        }

        sql = string.Join(" OR ", orConditions);

        return (sql, unsafeSqlUserValues);
    }

    #endregion

    
}
