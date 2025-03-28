namespace RagAI_v2.Prompts;

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.KernelMemory;

public static class SearchResultProcessor
{  
    /// <summary>
    /// Format search result prompt
    /// </summary>
    /// <param name="searchResult"></param>
    /// <param name="userQuestion"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static string FormatSearchResultPrompt(SearchResult searchResult, string userQuestion)
    {
        if (searchResult == null)
        {
            throw new ArgumentNullException(nameof(searchResult));
        }

        if (string.IsNullOrWhiteSpace(userQuestion))
        {
            throw new ArgumentException("User question cannot be null or whitespace.", nameof(userQuestion));
        }

        var promptBuilder = new StringBuilder();

        // Add user question to the prompt
        promptBuilder.AppendLine($"User Question: {userQuestion}");
        promptBuilder.AppendLine();

        // Process each citation and partition
        if (searchResult.Results != null && searchResult.Results.Any())
        {
            promptBuilder.AppendLine("Search Results:");
            //promptBuilder.AppendLine();

            foreach (var citation in searchResult.Results)
            {
                promptBuilder.AppendLine($"Source Name: {citation.SourceName}");
                //promptBuilder.AppendLine();

                if (citation.Partitions != null && citation.Partitions.Any())
                {
                    var cleanedText = CleanText(citation.Partitions.First().Text);
                    promptBuilder.AppendLine($"Partition : {cleanedText}");
                    promptBuilder.AppendLine();
                }
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
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(" ", lines);
    }
}

