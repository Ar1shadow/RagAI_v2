namespace RagAI_v2.Prompts;

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.KernelMemory;
using System.Collections.Concurrent;


public static class SearchResultProcessor
{
    /// <summary>
    /// Formate le prompt de résultat de recherche pour l'IA.
    /// </summary>
    /// <param name="searchResult">List d'enregistrements obtenus</param>
    /// <param name="userQuestion">Question d'utilisateur</param>
    /// <param name="prompt">Prompt pour RAG</param>>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static string FormatSearchResultPrompt(
        SearchResult? searchResult, 
        string? userQuestion, 
        string? prompt = CustomTemplate.Rag.Prompt)
    {
        if (searchResult == null)
            ArgumentNullException.ThrowIfNull(searchResult);

        if (string.IsNullOrWhiteSpace(userQuestion))
        {
            throw new ArgumentException("User question cannot be null or whitespace.", nameof(userQuestion));
        }

        var promptBuilder = new StringBuilder(prompt);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("[QUESTION]");
        promptBuilder.AppendLine(userQuestion);
        promptBuilder.AppendLine();

        // Process each citation and partition
        if (searchResult.Results.Count != 0)
        {
            promptBuilder.AppendLine("[SEARCH RESULTS]");
            foreach (var citation in searchResult.Results)
            {
                promptBuilder.AppendLine($"Source: {citation.SourceName}");
                if (citation.Partitions.Count != 0)
                {
                    foreach (var partition in citation.Partitions)
                    {
                        var cleaned = CleanText(partition.Text);
                        if (!string.IsNullOrWhiteSpace(cleaned))
                        {
                            promptBuilder.AppendLine($"—— {cleaned}");
                        }
                    }
                }
                promptBuilder.AppendLine();
            }
        }
        else
        {
            promptBuilder.AppendLine("Aucun result trouvé.");
        }

        return promptBuilder.ToString();
    }

    /// <summary>
    /// Nettoie le texte en supprimant les lignes vides et en coupant chaque ligne
    /// </summary>
    /// <param name="text">Text à nettoyer</param>
    /// <returns></returns>
    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Découper le texte en lignes, supprimer les lignes vides et couper chaque ligne
        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                        .Select(line => line.Trim());

        var paragraphs = new List<string>();
        var currentParagraph = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentParagraph.Any())
                {
                    paragraphs.Add(string.Join(" ", currentParagraph));
                    currentParagraph.Clear();
                }
            }
            else
            {
                currentParagraph.Add(line);
            }
        }

        if (currentParagraph.Any())
        {
            paragraphs.Add(string.Join(" ", currentParagraph));
        }

        return string.Join(Environment.NewLine, paragraphs);
    }
}
