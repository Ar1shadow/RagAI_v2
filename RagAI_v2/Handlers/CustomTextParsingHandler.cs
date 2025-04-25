
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.DataFormats.WebPages;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.Pipeline;
using RagAI_v2.Extensions;
using RagAI_v2.Utils;

namespace RagAI_v2.Handlers;


/// <summary>
/// Memory ingestion pipeline handler responsible for extracting text and partitioning from files and saving it to document storage.
/// Inner logic will call a python script to do the task.
/// </summary>
public class CustomTextParsingHandler : IPipelineStepHandler, IDisposable
{
    private readonly IPipelineOrchestrator _orchestrator;
    private readonly IEnumerable<IContentDecoder> _decoders;
    private readonly IWebScraper _webScraper;
    private readonly ILogger<TextExtractionHandler> _log;
    private readonly PythonChunkService _pythonService;
    
    public string StepName { get; }

    /// <summary>
    /// Handler responsible for extracting text from documents.
    /// Note: stepName and other params are injected with DI.
    /// </summary>
    /// <param name="stepName">Pipeline step for which the handler will be invoked</param>
    /// <param name="orchestrator">Current orchestrator used by the pipeline, giving access to content and other helps.</param>
    /// <param name="decoders">The list of content decoders for extracting content</param>
    /// <param name="webScraper">Web scraper instance used to fetch web pages</param>
    /// <param name="loggerFactory">Application logger factory</param>
    public CustomTextParsingHandler(
        string stepName,
        IPipelineOrchestrator orchestrator,
        IEnumerable<IContentDecoder> decoders,
        PythonChunkService pythonService,
        IWebScraper? webScraper = null,
        ILoggerFactory? loggerFactory = null)
    {
        this.StepName = stepName;
        this._orchestrator = orchestrator;
        this._decoders = decoders;
        this._pythonService = pythonService;
        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<TextExtractionHandler>();
        //TODO: implement web scraper in the python script
        
        //this._webScraper = webScraper ?? new WebScraper();
        
        this._log.LogInformation("Handler '{0}' ready", stepName);
        
    }

    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
        Outils.UpdatePipAndInstallPackages();
        // Appeler à un service FastAPI en Python
        await _pythonService.StartAsync("RagAI_v2/Extensions/Python/run_server.py");
        foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
        {
            Dictionary<string, DataPipeline.GeneratedFileDetails> newFiles = [];

            foreach (KeyValuePair<string, DataPipeline.GeneratedFileDetails> generatedFile in uploadedFile
                         .GeneratedFiles)
            {
                if (uploadedFile.AlreadyProcessedBy(this))
                {
                    this._log.LogTrace("File {0} already processed by this handler", uploadedFile.Name);
                    continue;
                }
            }
            
            var sourceFile = uploadedFile.Name;
            
            var chunks = await _pythonService.GetChunksAsync(sourceFile);
            _pythonService.Dispose(); 
            
            if (chunks.Count == 0) { continue; }

            this._log.LogDebug("Saving {0} file partitions", chunks.Count);
            for (int partitionNumber = 0; partitionNumber < chunks.Count; partitionNumber++)
            {
                // TODO: turn partitions in objects with more details, e.g. page number
                string text = chunks[partitionNumber];
                int sectionNumber = 0; // TODO: use this to store the page number (if any)
                BinaryData textData = new(text);

                var destFile = uploadedFile.GetPartitionFileName(partitionNumber);
                await this._orchestrator.WriteFileAsync(pipeline, destFile, textData, cancellationToken).ConfigureAwait(false);

                var destFileDetails = new DataPipeline.GeneratedFileDetails
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ParentId = uploadedFile.Id,
                    Name = destFile,
                    Size = text.Length,
                    MimeType = MimeTypes.PlainText,
                    ArtifactType = DataPipeline.ArtifactTypes.TextPartition,
                    PartitionNumber = partitionNumber,
                    SectionNumber = sectionNumber,
                    Tags = pipeline.Tags,
                    ContentSHA256 = textData.CalculateSHA256(),
                };
                newFiles.Add(destFile, destFileDetails);
                destFileDetails.MarkProcessedBy(this);
            }

            uploadedFile.MarkProcessedBy(this);
            // Add new files to pipeline status
            foreach (var file in newFiles)
            {
                uploadedFile.GeneratedFiles.Add(file.Key, file.Value);
            }
        }

        return (ReturnType.Success, pipeline);
    }
    
    
    
    
    public void Dispose()
    {
        if (this._webScraper is not IDisposable x) { return; }

        x.Dispose();
    }
    
    
}