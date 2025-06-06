using System.Text.RegularExpressions;
using Microsoft.KernelMemory.Postgres;


namespace RagAI_v2.MemoryDataBase.Postgres;


/// <summary>
/// M�thodes utilitaires pour la validation des noms de sch�ma, de table et de champ dans Postgres.
/// </summary>
internal static class PostgresSchema
{
    public const string PlaceholdersTags = "{{$tags}}";

    private static readonly Regex s_validNameRegex = new(@"^[a-zA-Z0-9\-]+$");

    // Note: "_" is allowed in field names
    private static readonly Regex s_validFieldNameRegex = new(@"^[a-zA-Z0-9\-_]+$");

    public static void ValidateSchemaName(string name)
    {
        if (s_validNameRegex.IsMatch(name)) { return; }

        throw new PostgresException($"The schema name '{name}' contains invalid chars");
    }

    public static void ValidateTableName(string name)
    {
        if (s_validNameRegex.IsMatch(name)) { return; }

        throw new PostgresException("The table/index name contains invalid chars");
    }

    public static void ValidateTableNamePrefix(string name)
    {
        if (s_validNameRegex.IsMatch(name)) { return; }

        throw new PostgresException($"The table name prefix '{name}' contains invalid chars");
    }

    public static void ValidateFieldName(string name)
    {
        if (s_validFieldNameRegex.IsMatch(name)) { return; }

        throw new PostgresException($"The field name '{name}' contains invalid chars");
    }
}
