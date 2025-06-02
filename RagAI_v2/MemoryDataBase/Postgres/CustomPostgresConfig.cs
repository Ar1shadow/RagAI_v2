using System;
using System.Collections.Generic;
using Microsoft.KernelMemory;

#pragma warning disable IDE0130 // reduce number of "using" statements
// ReSharper disable once CheckNamespace - reduce number of "using" statements
namespace RagAI_v2.MemoryDataBase.Postgres;

/// <summary>
/// Postgres configuration
/// </summary>
public class CustomPostgresConfig
{
    /// <summary>
    /// Key for the Columns dictionary
    /// clé pour le dictionnaire des colonnes
    /// </summary>
    public const string ColumnId = "id";

    /// <summary>
    /// Key for the Columns dictionary
    /// clé pour le dictionnaire des colonnes
    /// </summary>
    public const string ColumnEmbedding = "embedding";

    /// <summary>
    /// Key for the Columns dictionary
    /// clé pour le dictionnaire des colonnes
    /// </summary>
    public const string ColumnTags = "tags";

    /// <summary>
    /// Key for the Columns dictionary
    /// clé pour le dictionnaire des colonnes
    /// </summary>
    public const string ColumnContent = "content";

    /// <summary>
    /// Key for the Columns dictionary
    /// clé pour le dictionnaire des colonnes
    /// </summary>
    public const string ColumnPayload = "payload";

    /// <summary>
    /// Name of the default schema
    /// nom du schéma par défaut
    /// </summary>
    public const string DefaultSchema = "public";

    /// <summary>
    /// Default prefix used for table names
    /// nom de préfixe par défaut utilisé pour les noms de tables
    /// </summary>
    public const string DefaultTableNamePrefix = "rag-";

    /// <summary>
    /// Connection string required to connect to Postgres
    /// chaine de connexion requise pour se connecter à Postgres
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Name of the schema where to read and write records.
    /// nom du schéma où lire et écrire les enregistrements.
    /// </summary>
    public string Schema { get; set; } = DefaultSchema;

    /// <summary>
    /// Mandatory prefix to add to tables created by RagAI.This is used to distinguish Rag tables from others in the same schema.
    /// Prefix obligatoire à ajouter aux tables créées par RagAI. Ceci est utilisé pour distinguer les tables Rag des autres dans le même schéma.
    /// </summary>
    /// <remarks>
    /// Default value is set to "rag-" but can be override when creating a config.
    /// Valeur par défaut est définie sur "rag-" mais peut être remplacée lors de la création d'une configuration.
    /// </remarks>
    public string TableNamePrefix { get; set; } = DefaultTableNamePrefix;

    /// <summary>
    /// Configurable column names used with Postgres
    /// Columnes configurables utilisées avec Postgres
    /// </summary>
    public Dictionary<string, string> Columns { get; set; }

    /// <summary>
    /// Mandatory placeholder required in CreateTableSql
    /// Placeholder obligatoire requis dans CreateTableSql
    /// </summary>
    public const string SqlPlaceholdersTableName = "%%table_name%%";

    /// <summary>
    /// Mandatory placeholder required in CreateTableSql
    /// Espace réservé obligatoire requis dans CreateTableSql
    /// </summary>
    public const string SqlPlaceholdersVectorSize = "%%vector_size%%";

    /// <summary>
    /// Optional placeholder required in CreateTableSql
    /// Espace réservé optionnel requis dans CreateTableSql
    /// </summary>
    public const string SqlPlaceholdersLockId = "%%lock_id%%";

    /// <summary>
    /// Constante de pondération RRF (Reciprocal Rank Fusion) utilisée pour combiner les scores
    /// de similarité sémantique (vecteur) et de recherche plein texte.
    /// Plus la valeur est élevée, plus les éléments en bas du classement sont favorisés.
    /// </summary>
    public int Rrf_K_Vec { get; set; } = 40;

    /// <summary>
    /// Paramètre de pondération pour le classement RRF appliqué à la recherche en texte intégral (full-text).
    /// Une valeur plus faible donne plus de poids aux résultats mieux classés.
    /// Par défaut : 30
    /// </summary>
    public int Rrf_K_Text { get; set; } = 30;


    /// <summary>
    /// Active ou désactive la normalisation des scores avant la fusion.
    /// Lorsque activé, les scores sont mis à l'échelle pour limiter l'influence de la longueur
    /// du texte ou des valeurs extrêmes.
    /// </summary>
    public bool UserNormalization { get; set; } = false;


    /// <summary>
    /// Optional, custom SQL statements for creating new tables, in case
    /// you need to add custom columns, indexing, etc.
    /// The SQL must contain two placeholders: %%table_name%% and %%vector_size%%.
    /// You can put the SQL in one line or split it over multiple lines for
    /// readability. Lines are automatically merged with a new line char.
    /// Example:
    ///   BEGIN;
    ///   "SELECT pg_advisory_xact_lock(%%lock_id%%);
    ///   CREATE TABLE IF NOT EXISTS %%table_name%% (
    ///     id           TEXT NOT NULL PRIMARY KEY,
    ///     embedding    vector(%%vector_size%%),
    ///     tags         TEXT[] DEFAULT '{}'::TEXT[] NOT NULL,
    ///     content      TEXT DEFAULT '' NOT NULL,
    ///     payload      JSONB DEFAULT '{}'::JSONB NOT NULL,
    ///     some_text    TEXT DEFAULT '',
    ///     last_update  TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
    ///   );
    ///   CREATE INDEX IF NOT EXISTS idx_tags ON %%table_name%% USING GIN(tags);
    ///   COMMIT;
    ///   
    /// =========================================
    /// Instructions SQL optionnelles, personnalisées pour la création de nouvelles tables, au cas où
    /// vous auriez besoin d'ajouter des colonnes personnalisées, des index, etc.
    /// Le SQL doit contenir deux espaces réservés : %%table_name%% et %%vector_size%%.
    /// vous pouvez mettre le SQL sur une seule ligne ou le diviser en plusieurs lignes pour
    /// lisibilité. Les lignes sont automatiquement fusionnées avec une nouvelle ligne de caractères.
    /// Example:
    ///   BEGIN;
    ///   "SELECT pg_advisory_xact_lock(%%lock_id%%);
    ///   CREATE TABLE IF NOT EXISTS %%table_name%% (
    ///     id           TEXT NOT NULL PRIMARY KEY,
    ///     embedding    vector(%%vector_size%%),
    ///     tags         TEXT[] DEFAULT '{}'::TEXT[] NOT NULL,
    ///     content      TEXT DEFAULT '' NOT NULL,
    ///     payload      JSONB DEFAULT '{}'::JSONB NOT NULL,
    ///     some_text    TEXT DEFAULT '',
    ///     last_update  TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
    ///   );
    ///   CREATE INDEX IF NOT EXISTS idx_tags ON %%table_name%% USING GIN(tags);
    ///   COMMIT;
    /// </summary>
    public List<string> CreateTableSql { get; set; } = [];

    /// <summary>
    /// Create a new instance of the configuration
    /// Créer une nouvelle instance de la configuration
    /// </summary>
    public CustomPostgresConfig()
    {
        this.Columns = new Dictionary<string, string>
        {
            [ColumnId] = "id",
            [ColumnEmbedding] = "embedding",
            [ColumnTags] = "tags",
            [ColumnContent] = "content",
            [ColumnPayload] = "payload"
        };
    }

    /// <summary>
    /// Verify that the current state is valid.
    /// Vérifier que l'état actuel est valide.
    /// </summary>
    public void Validate()
    {
        this.TableNamePrefix = this.TableNamePrefix?.Trim() ?? string.Empty;
        this.ConnectionString = this.ConnectionString?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(this.ConnectionString))
        {
            throw new ConfigurationException($"Postgres: {nameof(this.ConnectionString)} is empty.");
        }

        if (string.IsNullOrWhiteSpace(this.TableNamePrefix))
        {
            throw new ConfigurationException($"Postgres: {nameof(this.TableNamePrefix)} is empty.");
        }

        // ID

        if (!this.Columns.TryGetValue(ColumnId, out var columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Id column is not defined.");
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Id column is empty.");
        }

        // Embedding

        if (!this.Columns.TryGetValue(ColumnEmbedding, out columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Embedding column is not defined.");
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Embedding column is empty.");
        }

        // Tags

        if (!this.Columns.TryGetValue(ColumnTags, out columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Tags column is not defined.");
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Tags column is empty.");
        }

        // Content

        if (!this.Columns.TryGetValue(ColumnContent, out columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Content column is not defined.");
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Content column is empty.");
        }

        // Payload

        if (!this.Columns.TryGetValue(ColumnPayload, out columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Payload column is not defined.");
        }

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ConfigurationException("Postgres: the name of the Payload column is empty.");
        }

        // Custom schema

        if (this.CreateTableSql?.Count > 0)
        {
            var sql = string.Join('\n', this.CreateTableSql).Trim();
            if (!sql.Contains(SqlPlaceholdersTableName, StringComparison.Ordinal))
            {
                throw new ConfigurationException(
                    "Postgres: the custom SQL to create tables is not valid, " +
                    $"it should contain a {SqlPlaceholdersTableName} placeholder.");
            }

            if (!sql.Contains(SqlPlaceholdersVectorSize, StringComparison.Ordinal))
            {
                throw new ConfigurationException(
                    "Postgres: the custom SQL to create tables is not valid, " +
                    $"it should contain a {SqlPlaceholdersVectorSize} placeholder.");
            }
        }

        this.Columns[ColumnId] = this.Columns[ColumnId].Trim();
        this.Columns[ColumnEmbedding] = this.Columns[ColumnEmbedding].Trim();
        this.Columns[ColumnTags] = this.Columns[ColumnTags].Trim();
        this.Columns[ColumnContent] = this.Columns[ColumnContent].Trim();
        this.Columns[ColumnPayload] = this.Columns[ColumnPayload].Trim();
    }
}
