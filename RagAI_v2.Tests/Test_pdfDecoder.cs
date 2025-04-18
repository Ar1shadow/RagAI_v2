using System.Text;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Xunit.Abstractions;


namespace RagAI_v2.Tests;

public class TestPdfDecoder
{
    private readonly ITestOutputHelper _output;
    
    public TestPdfDecoder(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task PdfDecoder()
    {
        var filename1 = "/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/2024 - Livret d'accueil GAMBA V2.pdf";
        var filename2 = "/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/Chenyu SHAO - Rapport de stage GAMBA 2024.pdf";
        var filename3 = "/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/2023AnnualReport.pdf";
        await using Stream data = File.OpenRead(filename3);
        var result = new Document();

        using (var pdf = PdfDocument.Open(data))
        {
            var headerCandidates = new Dictionary<string, int>();
            var footerCandidates = new Dictionary<string, int>();

            // Statistiques de fréquence des textes en haut et en bas de chaque page
            foreach (var page in pdf.GetPages())
            {
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
            foreach (var page in pdf.GetPages().Where(x => x !=null))
            {
                var words = page.GetWords(NearestNeighbourWordExtractor.Instance);
                var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
                var sb = new StringBuilder();
                foreach (var block in blocks)
                {
                    var text = block.Text.Trim();
                    
                    // exclude header and footer
                    if (commonHeaders.Contains(text) || commonFooters.Contains(text)) continue;
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    sb.Append(text);
                }
                _output.WriteLine("-PageContent : ");
                _output.WriteLine(sb.ToString());
                _output.WriteLine("=========================");
            }
        }        
    }
}