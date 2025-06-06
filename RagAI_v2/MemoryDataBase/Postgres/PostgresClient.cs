using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.MemoryStorage;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace RagAI_v2.MemoryDataBase.Postgres;

/// <summary>
/// An implementation of a client for Postgres. This class is used to managing postgres database operations.
/// Une implémentation d’un client pour Postgres. Cette classe est utilisée pour gérer les opérations sur la base de données Postgres.
/// </summary>
internal sealed class PostgresClient : IDisposable, IAsyncDisposable
{
    // Dependencies
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger _log;

    /// <summary>
    /// Initialise une nouvelle instance de la classe <see cref="PostgresClient"/>.
    /// </summary>
    /// <param name="config">Configuration</param>
    /// <param name="loggerFactory">Fabrique de logger de l'application</param>
    public PostgresClient(CustomPostgresConfig config, ILoggerFactory? loggerFactory = null)
    {
        config.Validate();
        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<PostgresClient>();

        NpgsqlDataSourceBuilder dataSourceBuilder = new(config.ConnectionString);
        dataSourceBuilder.UseVector();
        this._dataSource = dataSourceBuilder.Build();

        this._dbNamePresent = config.ConnectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase);
        this._schema = config.Schema;
        this._tableNamePrefix = config.TableNamePrefix;

        this._colId = config.Columns[CustomPostgresConfig.ColumnId];
        this._colEmbedding = config.Columns[CustomPostgresConfig.ColumnEmbedding];
        this._colTags = config.Columns[CustomPostgresConfig.ColumnTags];
        this._colContent = config.Columns[CustomPostgresConfig.ColumnContent];
        this._colPayload = config.Columns[CustomPostgresConfig.ColumnPayload];

        PostgresSchema.ValidateSchemaName(this._schema);
        PostgresSchema.ValidateTableNamePrefix(this._tableNamePrefix);
        PostgresSchema.ValidateFieldName(this._colId);
        PostgresSchema.ValidateFieldName(this._colEmbedding);
        PostgresSchema.ValidateFieldName(this._colTags);
        PostgresSchema.ValidateFieldName(this._colContent);
        PostgresSchema.ValidateFieldName(this._colPayload);

        this._columnsListNoEmbeddings = $"{this._colId},{this._colTags},{this._colContent},{this._colPayload}";
        this._columnsListWithEmbeddings = $"{this._colId},{this._colTags},{this._colContent},{this._colPayload},{this._colEmbedding}";

        this._createTableSql = string.Empty;
        if (config.CreateTableSql?.Count > 0)
        {
            this._createTableSql = string.Join('\n', config.CreateTableSql).Trim();
        }
    }

    /// <summary>
    /// Vérifier l'existence d'une table dans la base de données.
    /// </summary>
    /// <param name="tableName">Nom attribué à une table d’entrées</param>
    /// <param name="cancellationToken">Jeton d’annulation pour la tâche asynchrone</param>
    /// <returns>Vrai si la table exists</returns>
    public async Task<bool> DoesTableExistAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        tableName = this.WithTableNamePrefix(tableName);
        this._log.LogTrace("Checking if table {0} exists", tableName);

        NpgsqlConnection connection = await this.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await using (connection)
        {
            try
            {
                NpgsqlCommand cmd = connection.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
#pragma warning disable CA2100 // SQL reviewed
                    cmd.CommandText = $@"
                        SELECT table_name
                        FROM information_schema.tables
                            WHERE table_schema = @schema
                                AND table_name = @table
                                AND table_type = 'BASE TABLE'
                        LIMIT 1
                    ";

                    cmd.Parameters.AddWithValue("@schema", this._schema);
                    cmd.Parameters.AddWithValue("@table", tableName);
#pragma warning restore CA2100

                    this._log.LogTrace("Schema: {0}, Table: {1}, SQL: {2}", this._schema, tableName, cmd.CommandText);

                    NpgsqlDataReader dataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (dataReader.ConfigureAwait(false))
                    {
                        if (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var name = dataReader.GetString(dataReader.GetOrdinal("table_name"));

                            return string.Equals(name, tableName, StringComparison.OrdinalIgnoreCase);
                        }

                        this._log.LogTrace("Table {0} does not exist", tableName);
                        return false;
                    }
                }
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Créer une table dans la base de données.
    /// </summary>
    /// <param name="tableName">Nom attribué à une table d’entrées</param>
    /// <param name="vectorSize">Dimension de vecteur d'embedding</param>
    /// <param name="cancellationToken">Async task cancellation token</param>
    public async Task CreateTableAsync(
        string tableName,
        int vectorSize,
        CancellationToken cancellationToken = default)
    {
        var origInputTableName = tableName;
        tableName = this.WithSchemaAndTableNamePrefix(tableName);
        this._log.LogTrace("Creating table: {0}", tableName);

        Npgsql.PostgresException? createErr = null;

        NpgsqlConnection connection = await this.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await using (connection)
        {
            try
            {
                NpgsqlCommand cmd = connection.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
                    var lockId = GenLockId(tableName);

#pragma warning disable CA2100 // SQL reviewed
                    if (!string.IsNullOrEmpty(this._createTableSql))
                    {
                        cmd.CommandText = this._createTableSql
                            .Replace(CustomPostgresConfig.SqlPlaceholdersTableName, tableName, StringComparison.Ordinal)
                            .Replace(CustomPostgresConfig.SqlPlaceholdersVectorSize, $"{vectorSize}", StringComparison.Ordinal)
                            .Replace(CustomPostgresConfig.SqlPlaceholdersLockId, $"{lockId}", StringComparison.Ordinal);

                        this._log.LogTrace("Creating table with custom SQL: {0}", cmd.CommandText);
                    }
                    else
                    {
                        cmd.CommandText = $@"
                            BEGIN;
                            SELECT pg_advisory_xact_lock({lockId});
                            CREATE TABLE IF NOT EXISTS {tableName} (
                                {this._colId}        TEXT NOT NULL PRIMARY KEY,
                                {this._colEmbedding} vector({vectorSize}),
                                {this._colTags}      TEXT[] DEFAULT '{{}}'::TEXT[] NOT NULL,
                                {this._colContent}   TEXT DEFAULT '' NOT NULL,
                                {this._colPayload}   JSONB DEFAULT '{{}}'::JSONB NOT NULL
                            );
                            CREATE INDEX IF NOT EXISTS idx_embedding ON {tableName} 
                            USING hnsw({this._colEmbedding} vector_cosine_ops);
                            
                            CREATE INDEX IF NOT EXISTS idx_fts ON {tableName}
                            USING GIN (to_tsvector('french', {this._colContent}));
                            
                            CREATE INDEX IF NOT EXISTS idx_tags ON {tableName} USING GIN({this._colTags});
                            COMMIT;
                        ";
#pragma warning restore CA2100

                        this._log.LogTrace("Creating table with default SQL: {0}", cmd.CommandText);
                    }

                    int result = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    this._log.LogTrace("Table '{0}' creation result: {1}", tableName, result);
                }
            }
            catch (Npgsql.PostgresException e) when (IsVectorTypeDoesNotExistException(e))
            {
                this._log.LogError(e, "Vector type not installed, check 'SELECT * FROM pg_extension'");
                throw;
            }
            catch (Npgsql.PostgresException e) when (e.SqlState == PgErrUniqueViolation)
            {
                createErr = e;
            }
            catch (Exception e)
            {
                this._log.LogError(e, "Table '{0}' creation error: {1}. Err: {2}. InnerEx: {3}", tableName, e, e.Message, e.InnerException);
                throw;
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }

        if (createErr != null)
        {
            // If the table exists, assume the table state is fine, logs some warnings, and continue
            if (await this.DoesTableExistAsync(origInputTableName, cancellationToken).ConfigureAwait(false))
            {
                // Check if the custom SQL contains the lock placeholder (assuming it's not commented out)
                bool missingLockStatement = !string.IsNullOrEmpty(this._createTableSql)
                                            && !this._createTableSql.Contains(CustomPostgresConfig.SqlPlaceholdersLockId, StringComparison.Ordinal);

                if (missingLockStatement)
                {
                    this._log.LogWarning(
                        "Concurrency error: {0}; {1}; {2}. Add '{3}' to the custom SQL statement used to create tables. The table exists so the application will continue",
                        createErr.SqlState, createErr.Message, createErr.Detail, CustomPostgresConfig.SqlPlaceholdersLockId);
                }
                else
                {
                    this._log.LogWarning("Postgres error while creating table: {0}; {1}; {2}. The table exists so the application will continue",
                        createErr.SqlState, createErr.Message, createErr.Detail);
                }
            }
            else
            {
                // But if the table doesn't exist, throw
                this._log.LogError(createErr, "Table creation failed: {0}", tableName);
                throw createErr;
            }
        }
    }

    /// <summary>
    /// Obtenir toutes les tables
    /// </summary>
    /// <param name="cancellationToken">Async task cancellation token</param>
    /// <returns>Lists de tables</returns>
    public async IAsyncEnumerable<string> GetTablesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        NpgsqlConnection connection = await this.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await using (connection)
        {
            try
            {
                NpgsqlCommand cmd = connection.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
                    cmd.CommandText = @"SELECT table_name FROM information_schema.tables
                                WHERE table_schema = @schema AND table_type = 'BASE TABLE';";
                    cmd.Parameters.AddWithValue("@schema", this._schema);

                    this._log.LogTrace("Fetching list of tables. SQL: {0}. Schema: {1}", cmd.CommandText, this._schema);

                    NpgsqlDataReader dataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (dataReader.ConfigureAwait(false))
                    {
                        while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var tableNameWithPrefix = dataReader.GetString(dataReader.GetOrdinal("table_name"));
                            if (tableNameWithPrefix.StartsWith(this._tableNamePrefix, StringComparison.OrdinalIgnoreCase))
                            {
                                yield return tableNameWithPrefix.Remove(0, this._tableNamePrefix.Length);
                            }
                        }
                    }
                }
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Supprimer une table.
    /// </summary>
    /// <param name="tableName">Nom d'une table à supprimer</param>
    /// <param name="cancellationToken">Async task cancellation token</param>
    public async Task DeleteTableAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        tableName = this.WithSchemaAndTableNamePrefix(tableName);

        NpgsqlConnection connection = await this.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await using (connection)
        {
            try
            {
                NpgsqlCommand cmd = connection.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
#pragma warning disable CA2100 // SQL reviewed
                    cmd.CommandText = $"DROP TABLE IF EXISTS {tableName}";
#pragma warning restore CA2100

                    this._log.LogTrace("Deleting table. SQL: {0}", cmd.CommandText);
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Npgsql.PostgresException e) when (IsTableNotFoundException(e))
            {
                this._log.LogTrace("Table not found: {0}", tableName);
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Insérer ou mettre à jour une entrée dans une table.
    /// </summary>
    /// <param name="tableName">Nom attribué d'une table d'entrée</param>
    /// <param name="record">enregistrement à insérer/mis à jour</param>
    /// <param name="cancellationToken">Async task cancellation token</param>
    public async Task UpsertAsync(
        string tableName,
        PostgresMemoryRecord record,
        CancellationToken cancellationToken = default)
    {
        tableName = this.WithSchemaAndTableNamePrefix(tableName);

        const string EmptyPayload = "{}";
        const string EmptyContent = "";
        string[] emptyTags = [];

        NpgsqlConnection connection = await this.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await using (connection)
        {
            try
            {
                NpgsqlCommand cmd = connection.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
#pragma warning disable CA2100 // SQL reviewed
                    cmd.CommandText = $@"
                        INSERT INTO {tableName}
                            ({this._colId}, {this._colEmbedding}, {this._colTags}, {this._colContent}, {this._colPayload})
                            VALUES
                            (@id, @embedding, @tags, @content, @payload)
                        ON CONFLICT ({this._colId})
                        DO UPDATE SET
                            {this._colEmbedding} = @embedding,
                            {this._colTags}      = @tags,
                            {this._colContent}   = @content,
                            {this._colPayload}   = @payload
                    ";

                    cmd.Parameters.AddWithValue("@id", record.Id);
                    cmd.Parameters.AddWithValue("@embedding", record.Embedding);
                    cmd.Parameters.AddWithValue("@tags", NpgsqlDbType.Array | NpgsqlDbType.Text, record.Tags.ToArray() ?? emptyTags);
                    cmd.Parameters.AddWithValue("@content", NpgsqlDbType.Text, CleanContent(record.Content) ?? EmptyContent);
                    cmd.Parameters.AddWithValue("@payload", NpgsqlDbType.Jsonb, record.Payload ?? EmptyPayload);
#pragma warning restore CA2100

                    this._log.LogTrace("Upserting record '{0}' in table '{1}'", record.Id, tableName);

                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Npgsql.PostgresException e) when (IsTableNotFoundException(e))
            {
                throw new IndexNotFoundException(e.Message, e);
            }
            catch (Exception e)
            {
                throw new Microsoft.KernelMemory.Postgres.PostgresException(e.Message, e);
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Obtenir une liste d'enregistrements en comparant la similarité vectorielle avec un vecteur cible.
    /// </summary>
    /// <param name="tableName">Table contenant les enregistrements à récupérer</param>
    /// <param name="target">Vecteur source à comparer pour la similarité</param>
    /// <param name="minSimilarity">Seuil minimal de similarité</param>
    /// <param name="filterSql">Filtre SQL à appliquer</param>
    /// <param name="sqlUserValues">Liste de valeurs utilisateur passées avec des espaces réservés pour éviter l'injection SQL</param>
    /// <param name="limit">Nombre maximal d'enregistrements à récupérer</param>
    /// <param name="offset">Nombre d'enregistrements à ignorer depuis le début</param>
    /// <param name="withEmbeddings">Indique s'il faut inclure les vecteurs d'embedding</param>
    /// <param name="cancellationToken">Jeton d'annulation pour la tâche asynchrone</param>
    public async IAsyncEnumerable<(PostgresMemoryRecord record, double similarity)> GetSimilarAsync(
        string tableName,
        Vector target,
        double minSimilarity,
        string? filterSql = null,
        Dictionary<string, object>? sqlUserValues = null,
        int limit = 1,
        int offset = 0,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        tableName = this.WithSchemaAndTableNamePrefix(tableName);

        if (limit <= 0) { limit = int.MaxValue; }

        // Column names
        string columns = withEmbeddings ? this._columnsListWithEmbeddings : this._columnsListNoEmbeddings;

        // Filtering logic, including filter by similarity
        filterSql = filterSql?.Trim().Replace(PostgresSchema.PlaceholdersTags, this._colTags, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(filterSql))
        {
            filterSql = "TRUE";
        }

        var maxDistance = 1 - minSimilarity;
        filterSql += $" AND {this._colEmbedding} <=> @embedding < @maxDistance";

        if (sqlUserValues == null) { sqlUserValues = []; }

        this._log.LogTrace("Searching by similarity. Table: {0}. Threshold: {1}. Limit: {2}. Offset: {3}. Using SQL filter: {4}",
            tableName, minSimilarity, limit, offset, string.IsNullOrWhiteSpace(filterSql) ? "false" : "true");

        NpgsqlConnection connection = await this.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await using (connection)
        {
            try
            {
                NpgsqlCommand cmd = connection.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
#pragma warning disable CA2100 // SQL reviewed
                    string colDistance = "__distance";

                    // When using 1 - (embedding <=> target) the index is not being used, therefore we calculate
                    // the similarity (1 - distance) later. Furthermore, colDistance can't be used in the WHERE clause.
                    cmd.CommandText = @$"
                        SELECT {columns}, {this._colEmbedding} <=> @embedding AS {colDistance}
                        FROM {tableName}
                        WHERE {filterSql}
                        ORDER BY {colDistance} ASC
                        LIMIT @limit
                        OFFSET @offset
                    ";

                    cmd.Parameters.AddWithValue("@embedding", target);
                    cmd.Parameters.AddWithValue("@maxDistance", maxDistance);
                    cmd.Parameters.AddWithValue("@limit", limit);
                    cmd.Parameters.AddWithValue("@offset", offset);

                    foreach (KeyValuePair<string, object> kv in sqlUserValues)
                    {
                        cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                    }
#pragma warning restore CA2100
                    // TODO: rewrite code to stream results (need to combine yield and try-catch)
                    var result = new List<(PostgresMemoryRecord record, double similarity)>();
                    try
                    {
                        NpgsqlDataReader dataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        await using (dataReader.ConfigureAwait(false))
                        {
                            while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                double distance = dataReader.GetDouble(dataReader.GetOrdinal(colDistance));
                                double similarity = 1 - distance;
                                result.Add((this.ReadEntry(dataReader, withEmbeddings), similarity));
                            }
                        }
                    }
                    catch (Npgsql.PostgresException e) when (IsTableNotFoundException(e))
                    {
                        this._log.LogTrace("Table not found: {0}", tableName);
                    }

                    // TODO: rewrite code to stream results (need to combine yield and try-catch)
                    foreach (var x in result)
                    {
                        yield return x;

                        // If requested cancel potentially long-running loop
                        if (cancellationToken is { IsCancellationRequested: true })
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Recherche hybride combinant la similarité vectorielle et la recherche plein texte.
    /// </summary>
    /// <param name="tableName">Nom de la table</param>
    /// <param name="target">Vecteur d'embedding pour la recherche sémantique</param>
    /// <param name="textQuery">Requête plein texte</param>
    /// <param name="rrf_K_vec">Constante RRF pour la pondération des résultats véctoriels</param>
    /// <param name="rrf_K_vec">Constante RRF pour la pondération des résultats en texte</param>
    /// <param name="limit">Nombre maximum de résultats</param>
    /// <param name="withEmbeddings">Si avec embedding ou pas</param>>
    /// <param name="useNormalization">Si employer le stratégie de normalization</param>>
    /// <param name="cancellationToken">Token d'annulation</param>
    public async IAsyncEnumerable<(PostgresMemoryRecord record, double score)> GetHybridSearchAsync(
        string tableName,
        Vector target,
        string textQuery,
        int rrf_K_vec = 60,
        int rrf_K_text = 30,
        int limit = 10,
        bool withEmbeddings = true,
        bool useNormalization = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        tableName = this.WithSchemaAndTableNamePrefix(tableName);

        string colVecScore = "__vec_score";
        string colTextScore = "__text_score";
        string colRrfScore = "__rrf_score";
        string columns = withEmbeddings ? this._columnsListWithEmbeddings : this._columnsListNoEmbeddings;
        
#pragma warning disable CA2100 // SQL reviewed
        // La logique suivante :
        // - Ajoute un try-catch autour de ExecuteReaderAsync pour capturer les exceptions PostgresException (notamment table inexistante)
        // - En cas de table inexistante, log "Table not found: {tableName}" (niveau Trace)
        // - Les résultats sont collectés dans une liste puis yield à la fin, permettant de contrôler l'annulation et la gestion d'exceptions
        // - cancellationToken est vérifié à chaque itération pour permettre l'arrêt rapide si demandé
        // - La méthode ReadEntry est utilisée pour construire le PostgresMemoryRecord
        
        NpgsqlConnection connection = await this.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await using (connection)
        {
            var cmd = connection.CreateCommand();
            await using (cmd.ConfigureAwait(false))
            {
                // SQL pour récupérer les résultats vectoriels et plein texte, puis fusionner avec pondération RRF
                if (useNormalization)
                {
                    cmd.CommandText = @$"
                        WITH vector_results AS (
                            SELECT {columns}, 1 - ({this._colEmbedding} <=> @embedding) AS {colVecScore}
                            FROM {tableName}
                            ORDER BY {colVecScore} DESC
                            LIMIT @internal_limit
                        ),
                        text_results AS (
                            SELECT {columns}, ts_rank_cd(to_tsvector('french', {this._colContent}), plainto_tsquery('french', @query), 1) AS {colTextScore}
                            FROM {tableName}
                            WHERE to_tsvector('french', {this._colContent}) @@ plainto_tsquery('french', @query)
                            LIMIT @internal_limit
                        ),
                        max_scores AS (
                            SELECT 
                                (SELECT MAX({colVecScore}) FROM vector_results) AS max_vec,
                                (SELECT MAX({colTextScore}) FROM text_results) AS max_text
                        ),
                        fusion AS (
                            SELECT DISTINCT ON (COALESCE(v.id, t.id))
                                COALESCE(v.id, t.id) AS id,
                                COALESCE(v.{this._colEmbedding}, t.{this._colEmbedding}) AS {this._colEmbedding},
                                COALESCE(v.{this._colTags}, t.{this._colTags}) AS {this._colTags},
                                COALESCE(v.{this._colContent}, t.{this._colContent}) AS {this._colContent},
                                COALESCE(v.{this._colPayload}, t.{this._colPayload}) AS {this._colPayload},
                                COALESCE(v.{colVecScore}, 0) AS {colVecScore},
                                COALESCE(t.{colTextScore}, 0) AS {colTextScore},
                                COALESCE(
                                    COALESCE((COALESCE(v.{colVecScore}, 0) / NULLIF(max_scores.max_vec, 0)),0) +
                                    COALESCE((COALESCE(t.{colTextScore}, 0) / NULLIF(max_scores.max_text, 0)),0)
                                    ,0) 
                                AS {colRrfScore}
                            FROM vector_results v
                            FULL OUTER JOIN text_results t ON v.id = t.id
                            CROSS JOIN max_scores
                        )
                        SELECT * FROM fusion
                        ORDER BY {colRrfScore} DESC
                        LIMIT @limit;
                            ";
                }
                else
                {
                    cmd.CommandText = @$"
                        WITH vector_results AS (
                            SELECT {columns}, {this._colEmbedding} <=> @embedding AS {colVecScore}
                            FROM {tableName}
                            ORDER BY {colVecScore}
                            LIMIT @internal_limit
                        ),
                        text_results AS (
                            SELECT {columns}, ts_rank_cd(to_tsvector('french', {this._colContent}), plainto_tsquery('french', @query)) AS {colTextScore}
                            FROM {tableName}
                            WHERE to_tsvector('french', {this._colContent}) @@ plainto_tsquery('french', @query)
                            LIMIT @internal_limit
                        ),
                        fusion AS (
                            SELECT DISTINCT ON (COALESCE(v.id, t.id))
                                COALESCE(v.id, t.id) AS id,
                                COALESCE(v.{this._colEmbedding}, t.{this._colEmbedding}) AS {this._colEmbedding},
                                COALESCE(v.{this._colTags}, t.{this._colTags}) AS {this._colTags},
                                COALESCE(v.{this._colContent}, t.{this._colContent}) AS {this._colContent},
                                COALESCE(v.{this._colPayload}, t.{this._colPayload}) AS {this._colPayload},
                                COALESCE(v.{colVecScore}, 999) AS {colVecScore},
                                COALESCE(t.{colTextScore}, 0) AS {colTextScore},
                                COALESCE((1.0 / (@rrf_K_vec + ROW_NUMBER() OVER (ORDER BY v.{colVecScore}))),0) +
                                COALESCE((1.0 / (@rrf_K_text + ROW_NUMBER() OVER (ORDER BY t.{colTextScore} DESC))),0) AS {colRrfScore}
                            FROM vector_results v
                            FULL OUTER JOIN text_results t ON v.id = t.id
                        )
                        SELECT * FROM fusion
                        ORDER BY {colRrfScore} DESC
                        LIMIT @limit;
                            ";
                }
                int multiple = 3;
                int internal_limit = limit * multiple;
                

                cmd.Parameters.AddWithValue("@embedding", target);
                cmd.Parameters.AddWithValue("@query", textQuery);
                cmd.Parameters.AddWithValue("@rrf_K_vec", rrf_K_vec);
                cmd.Parameters.AddWithValue("@rrf_K_text", rrf_K_text);
                cmd.Parameters.AddWithValue("@limit", limit);
                cmd.Parameters.AddWithValue("@internal_limit", internal_limit);
                

                // On collecte tous les résultats dans une liste pour pouvoir gérer l'annulation et les exceptions uniformément
                var result = new List<(PostgresMemoryRecord record, double score)>();
                try
                {
                    var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var record = this.ReadEntry(reader, true);
                            double score = reader.GetDouble(reader.GetOrdinal(colRrfScore));
                            result.Add((record, score));
                        }
                    }
                }
                catch (Npgsql.PostgresException e) when (IsTableNotFoundException(e))
                {
                    this._log.LogTrace("Table not found: {0}", tableName);
                }

                foreach (var x in result)
                {
                    yield return x;
                    // Vérifie si l'annulation a été demandée, pour interrompre la boucle rapidement
                    if (cancellationToken is { IsCancellationRequested: true })
                    {
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Obtenir une liste d'enregistrements à partir d'une table.
    /// </summary>
    /// <param name="tableName">Table contenant enregistrement à récupérer</param>
    /// <param name="filterSql">SQL filtre à appliquer</param>
    /// <param name="sqlUserValues">Liste de valeurs utilisateur passées avec des espaces réservés pour éviter l'injection SQL</param>
    /// <param name="orderBySql">SQL ordonnant enregistrements</param>
    /// <param name="limit">Nombre maximal d'enregistrement à récupérer</param>
    /// <param name="offset">Nombre d'enregistrement à ignorer depuis le début</param>
    /// <param name="withEmbeddings">S'il faut inclure vecteur d'embedding</param>
    /// <param name="cancellationToken">Async task cancellation token</param>
    public async IAsyncEnumerable<PostgresMemoryRecord> GetListAsync(
        string tableName,
        string? filterSql = null,
        Dictionary<string, object>? sqlUserValues = null,
        string? orderBySql = null,
        int limit = 1,
        int offset = 0,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        tableName = this.WithSchemaAndTableNamePrefix(tableName);

        if (limit <= 0) { limit = int.MaxValue; }

        string columns = withEmbeddings ? this._columnsListWithEmbeddings : this._columnsListNoEmbeddings;

        // Filtering logic
        filterSql = filterSql?.Trim().Replace(PostgresSchema.PlaceholdersTags, this._colTags, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(filterSql))
        {
            filterSql = "TRUE";
        }

        // Custom ordering
        if (string.IsNullOrWhiteSpace(orderBySql))
        {
            orderBySql = this._colId;
        }

        this._log.LogTrace("Fetching list of records. Table: {0}. Order by: {1}. Limit: {2}. Offset: {3}. Using SQL filter: {4}",
            tableName, orderBySql, limit, offset, string.IsNullOrWhiteSpace(filterSql) ? "false" : "true");

        NpgsqlConnection connection = await this.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await using (connection)
        {
            try
            {
                NpgsqlCommand cmd = connection.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
#pragma warning disable CA2100 // SQL reviewed
                    cmd.CommandText = @$"
                        SELECT {columns} FROM {tableName}
                        WHERE {filterSql}
                        ORDER BY {orderBySql}
                        LIMIT @limit
                        OFFSET @offset
                    ";

                    cmd.Parameters.AddWithValue("@limit", limit);
                    cmd.Parameters.AddWithValue("@offset", offset);

                    if (sqlUserValues != null)
                    {
                        foreach (KeyValuePair<string, object> kv in sqlUserValues)
                        {
                            cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                        }
                    }
#pragma warning restore CA2100

                    // TODO: rewrite code to stream results (need to combine yield and try-catch)
                    var result = new List<PostgresMemoryRecord>();
                    try
                    {
                        NpgsqlDataReader dataReader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        await using (dataReader.ConfigureAwait(false))
                        {
                            while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                result.Add(this.ReadEntry(dataReader, withEmbeddings));
                            }
                        }
                    }
                    catch (Npgsql.PostgresException e) when (IsTableNotFoundException(e))
                    {
                        this._log.LogTrace("Table not found: {0}", tableName);
                    }

                    // TODO: rewrite code to stream results (need to combine yield and try-catch)
                    foreach (var x in result)
                    {
                        yield return x;

                        // If requested cancel potentially long-running loop
                        if (cancellationToken is { IsCancellationRequested: true })
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Supprimer un enregistrement d'une table.
    /// </summary>
    /// <param name="tableName">Nom attribué d'une table d'entrée</param>
    /// <param name="id">Clé d'enregistrement à supprimer</param>
    /// <param name="cancellationToken">Async task cancellation token</param>
    public async Task DeleteAsync(
        string tableName,
        string id,
        CancellationToken cancellationToken = default)
    {
        tableName = this.WithSchemaAndTableNamePrefix(tableName);
        this._log.LogTrace("Deleting record '{0}' from table '{1}'", id, tableName);

        NpgsqlConnection connection = await this.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await using (connection)
        {
            try
            {
                NpgsqlCommand cmd = connection.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
#pragma warning disable CA2100 // SQL reviewed
                    cmd.CommandText = $"DELETE FROM {tableName} WHERE {this._colId}=@id";
                    cmd.Parameters.AddWithValue("@id", id);
#pragma warning restore CA2100

                    try
                    {
                        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Npgsql.PostgresException e) when (IsTableNotFoundException(e))
                    {
                        this._log.LogTrace("Table not found: {0}", tableName);
                    }
                }
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._dataSource?.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await this._dataSource.DisposeAsync().ConfigureAwait(false);
        }
        catch (NullReferenceException)
        {
            // ignore
        }
    }

    #region private ================================================================================

    // See: https://www.postgresql.org/docs/current/errcodes-appendix.html
    private const string PgErrUndefinedTable = "42P01"; // undefined_table
    private const string PgErrUniqueViolation = "23505"; // unique_violation
    private const string PgErrTypeDoesNotExist = "42704"; // undefined_object
    private const string PgErrDatabaseDoesNotExist = "3D000"; // invalid_catalog_name

    private readonly string _schema;
    private readonly string _tableNamePrefix;
    private readonly string _createTableSql;
    private readonly string _colId;
    private readonly string _colEmbedding;
    private readonly string _colTags;
    private readonly string _colContent;
    private readonly string _colPayload;
    private readonly string _columnsListNoEmbeddings;
    private readonly string _columnsListWithEmbeddings;
    private readonly bool _dbNamePresent;

    /// <summary>
    /// Try to connect to PG, handling exceptions in case the DB doesn't exist
    /// </summary>
    /// <param name="cancellationToken"></param>
    private async Task<NpgsqlConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await this._dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Npgsql.PostgresException e) when (IsDbNotFoundException(e))
        {
            if (this._dbNamePresent)
            {
                this._log.LogCritical("DB not found. Try checking the connection string, e.g. whether the `Database` parameter is empty or incorrect: {0}", e.Message);
            }
            else
            {
                this._log.LogCritical("DB not found. Try checking the connection string, e.g. specifying the `Database` parameter: {0}", e.Message);
            }

            throw;
        }
    }

    private static string CleanContent(string input)
    {
        // Remove 0x00 null, not supported by Postgres text fields, to avoid
        // exception: 22021: invalid byte sequence for encoding "UTF8": 0x00
        return input.Replace("\0", "", StringComparison.Ordinal);
    }

    private PostgresMemoryRecord ReadEntry(NpgsqlDataReader dataReader, bool withEmbeddings)
    {
        string id = dataReader.GetString(dataReader.GetOrdinal(this._colId));
        string content = dataReader.GetString(dataReader.GetOrdinal(this._colContent));
        string payload = dataReader.GetString(dataReader.GetOrdinal(this._colPayload));
        List<string> tags = dataReader.GetFieldValue<List<string>>(dataReader.GetOrdinal(this._colTags));

        Vector embedding = withEmbeddings
            ? dataReader.GetFieldValue<Vector>(dataReader.GetOrdinal(this._colEmbedding))
            : new Vector(new ReadOnlyMemory<float>());

        return new PostgresMemoryRecord
        {
            Id = id,
            Embedding = embedding,
            Tags = tags,
            Content = content,
            Payload = payload
        };
    }

    /// <summary>
    /// Get full table name with schema from table name
    /// </summary>
    /// <param name="tableName"></param>
    /// <returns>Valid table name including schema</returns>
    private string WithSchemaAndTableNamePrefix(string tableName)
    {
        tableName = this.WithTableNamePrefix(tableName);
        PostgresSchema.ValidateTableName(tableName);

        return $"{this._schema}.\"{tableName}\"";
    }

    private string WithTableNamePrefix(string tableName)
    {
        return $"{this._tableNamePrefix}{tableName}";
    }

    private static bool IsDbNotFoundException(Npgsql.PostgresException e)
    {
        return e.SqlState == PgErrDatabaseDoesNotExist;
    }

    private static bool IsTableNotFoundException(Npgsql.PostgresException e)
    {
        return e.SqlState == PgErrUndefinedTable || e.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVectorTypeDoesNotExistException(Npgsql.PostgresException e)
    {
        return e.SqlState == PgErrTypeDoesNotExist
               && e.Message.Contains("type", StringComparison.OrdinalIgnoreCase)
               && e.Message.Contains("vector", StringComparison.OrdinalIgnoreCase)
               && e.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generate a consistent lock id for a given resource, reducing the chance of collisions.
    /// If a collision happens because two resources have the same lock id, when locks are used
    /// these resources will be accessible one at a time, and not concurrently.
    /// </summary>
    /// <param name="resourceId">Resource Id</param>
    /// <returns>A number assigned to the resource</returns>
    private static long GenLockId(string resourceId)
    {
        return BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(resourceId)), 0)
               % short.MaxValue;
    }

    #endregion
}

    