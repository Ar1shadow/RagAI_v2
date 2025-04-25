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
    /// Format search result prompt
    /// </summary>
    /// <param name="searchResult"></param>
    /// <param name="userQuestion"></param>
    /// <param name="prompt"></param>>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static string FormatSearchResultPrompt(
        SearchResult? searchResult, 
        string? userQuestion, 
        string? prompt = CustomTemplate.Rag.SimplePrompt)
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
            promptBuilder.AppendLine("No results found.");
        }

        return promptBuilder.ToString();
    }
    
    /// <summary>
    /// Clean text by removing empty lines and trimming each line
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Split text into lines, remove empty lines, and trim each line
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
