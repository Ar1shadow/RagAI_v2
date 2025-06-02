using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using Pgvector;

namespace RagAI_v2.MemoryDataBase.Postgres;

/// <summary>
/// Postgres record schema
/// Schema du Postgres pour les enregistrements de mémoire.
/// </summary>
internal sealed class PostgresMemoryRecord
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        AllowTrailingCommas = true,
        MaxDepth = 10,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        WriteIndented = false
    };

    /// <summary>
    /// ID enregistrement
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Content embedding vector
    /// vecteur d'embedding
    /// </summary>
    public Vector Embedding { get; set; } = new Vector(new ReadOnlyMemory<float>());

    /// <summary>
    /// List of tags
    /// liste des tags
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Memory content
    /// contenu de la mémoire
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Additional payload, not searchable
    /// payload additionnel, non indexé
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Convert a Postgres record to a memory record instance
    /// Convertit un enregistrement Postgres en une instance <see cref="MemoryRecord"/>
    /// </summary>
    /// <param name="pgMemoryRecord">Postgres record data</param>
    /// <returns>Memory record data</returns>
    public static MemoryRecord ToMemoryRecord(PostgresMemoryRecord pgMemoryRecord)
    {
        var result = new MemoryRecord
        {
            Id = pgMemoryRecord.Id,
            Vector = new Embedding(pgMemoryRecord.Embedding.ToArray()),
            Payload = JsonSerializer.Deserialize<Dictionary<string, object>>(pgMemoryRecord.Payload, s_jsonOptions) ?? []
        };

        result.Payload[Constants.ReservedPayloadTextField] = pgMemoryRecord.Content;

        foreach (string[] keyValue in pgMemoryRecord.Tags.Select(tag => tag.Split(Constants.ReservedEqualsChar, 2)))
        {
            string key = keyValue[0];
            string? value = keyValue.Length == 1 ? null : keyValue[1];
            result.Tags.Add(key, value);
        }

        return result;
    }

    /// <summary>
    /// Convert a memory record to a Postgres record instance
    /// Convertit un enregistrement de mémoire <see cref="MemoryRecord"> en une instance PostgresMemoryRecord 
    /// </summary>
    /// <param name="record">Memory record</param>
    /// <returns>Postgres record data</returns>
    public static PostgresMemoryRecord FromMemoryRecord(MemoryRecord record)
    {
        var result = new PostgresMemoryRecord
        {
            Id = record.Id,
            Embedding = new Vector(record.Vector.Data),
        };

        if (record.Payload.TryGetValue(Constants.ReservedPayloadTextField, out object? value))
        {
            result.Content = (string)value;
        }

        Dictionary<string, object> payload = record.Payload.Where(kv => kv.Key != Constants.ReservedPayloadTextField).ToDictionary(kv => kv.Key, kv => kv.Value);
        result.Payload = JsonSerializer.Serialize(payload, s_jsonOptions);

        result.Tags = record.Tags.Pairs.Select(tag => $"{tag.Key}{Constants.ReservedEqualsChar}{tag.Value}").ToList();

        return result;
    }
}
