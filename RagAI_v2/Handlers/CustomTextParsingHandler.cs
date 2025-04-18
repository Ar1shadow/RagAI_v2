using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.DataFormats.WebPages;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.Pipeline;

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
        IWebScraper? webScraper = null,
        ILoggerFactory? loggerFactory = null)
    {
        this.StepName = stepName;
        this._orchestrator = orchestrator;
        this._decoders = decoders;
        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<TextExtractionHandler>();
        //TODO: implement web scraper in the python script
        this._webScraper = webScraper ?? new WebScraper();

        this._log.LogInformation("Handler '{0}' ready", stepName);
    }

    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
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
            var destFile = $"{uploadedFile.Name}.extract.txt";
            //TODO:Whether delete or not
            var destFile2 = $"{uploadedFile.Name}.extract.json";
            BinaryData fileContent = await this._orchestrator.ReadFileAsync(pipeline, sourceFile, cancellationToken).ConfigureAwait(false);
            
            string text = string.Empty;
            FileContent content = new(MimeTypes.PlainText);
            bool skipFile = false;
        }
        
        
        return (ReturnType.Success, pipeline);
    }
    
    
    
    
    public void Dispose()
    {
        if (this._webScraper is not IDisposable x) { return; }

        x.Dispose();
    }
}