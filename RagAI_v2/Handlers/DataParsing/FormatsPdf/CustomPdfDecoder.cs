
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.KernelMemory.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace RagAI_v2.Handlers.DataParsing.FormatsPdf;

public class CustomPdfDecoder : IContentDecoder
{
    private readonly ILogger<CustomPdfDecoder> _log;

    public CustomPdfDecoder(ILoggerFactory? loggerFactory = null)
    {
        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<CustomPdfDecoder>();
    }

    /// <inheritdoc />
    public bool SupportsMimeType(string mimeType)
    {
        return mimeType != null && mimeType.StartsWith(MimeTypes.Pdf, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(filename);
        return this.DecodeAsync(stream, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default)
    {
        using var stream = data.ToStream();
        return this.DecodeAsync(stream, cancellationToken);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default)
    {
        this._log.LogDebug("Extracting text from PDF file");

        var result = new FileContent(MimeTypes.PlainText);
        using PdfDocument? pdfDocument = PdfDocument.Open(data);
        if (pdfDocument == null)
        {
            return Task.FromResult(result);
        }

        var headerCandidates = new Dictionary<string, int>();
        var footerCandidates = new Dictionary<string, int>();

        foreach (Page? page in pdfDocument.GetPages().Where(x => x != null))
        {
            // Note: no trimming, use original spacing when working with pages
            //string pageContent = ContentOrderTextExtractor.GetText(page).NormalizeNewlines(false) ?? string.Empty;
            var height = page.Height;
            var words = page.GetWords(NearestNeighbourWordExtractor.Instance);
            var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);

            foreach (var block in blocks)
            {
                var text = string.Concat(block.Text).Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                var top = block.BoundingBox.Top;
                var bottom = block.BoundingBox.Bottom;

                if (top > height * 0.85)
                {
                    if (!footerCandidates.ContainsKey(text)) footerCandidates[text] = 0;
                    footerCandidates[text]++;
                }
                else if (bottom < height * 0.15)
                {
                    if (!headerCandidates.ContainsKey(text)) headerCandidates[text] = 0;
                    headerCandidates[text]++;
                }
            }
        }

        var commonHeaders = headerCandidates.Where(kv => kv.Value >= 3).Select(kv => kv.Key).ToHashSet();
        var commonFooters = footerCandidates.Where(kv => kv.Value >= 3).Select(kv => kv.Key).ToHashSet();

        // Parcourt à nouveau chaque page pour extraire et afficher le contenu principal
        foreach (var page in pdfDocument.GetPages().Where(x => x != null))
        {
            var words = page.GetWords(NearestNeighbourWordExtractor.Instance);
            var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
            var pageContent = new StringBuilder();
            foreach (var block in blocks)
            {
                var text = block.Text.Trim();
                
                // exclude header and footer
                if (commonHeaders.Contains(text) || commonFooters.Contains(text)) continue;
                if (string.IsNullOrWhiteSpace(text)) continue;

                pageContent.Append(text);
            }

            result.Sections.Add(new Chunk(pageContent.ToString(), page.Number, Chunk.Meta(sentencesAreComplete: false)));
        }

        return Task.FromResult(result);
    }
    
    
    
    
}