using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.DataFormats.WebPages;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.Pipeline;
using RagAI_v2.Extensions;
using RagAI_v2.Utils;

namespace RagAI_v2.Handlers;


/// <summary>
/// Handler responsable de l'extraction de texte et de la partition des fichiers, puis de les enregistrer dans le stockage de documents.
/// La logique interne appellera un script python pour effectuer la tâche.
/// </summary>
public class CustomTextParsingHandler : IPipelineStepHandler, IDisposable
{
    private readonly IPipelineOrchestrator _orchestrator;
    private readonly SimpleFileStorageConfig _config;
    private readonly IWebScraper _webScraper;
    private readonly ILogger<TextExtractionHandler> _log;
    private readonly PythonChunkService _pythonService;
    
    public string StepName { get; }

    /// <summary>
    /// Handler responsable de l'extraction de texte à partir de documents.
    /// Remarque : stepName et d'autres paramètres sont injectés via l'injection de dépendances (DI).
    /// </summary>
    /// <param name="stepName">Étape du pipeline pour laquelle le handler sera invoqué</param>
    /// <param name="orchestrator">Orchestrateur courant utilisé par le pipeline, donnant accès au contenu et à d'autres aides.</param>
    /// <param name="config">Permet de lire les informations du pipeline dans le stockage de documents</param>
    /// <param name="loggerFactory">Fabrique de loggers de l'application</param>
    public CustomTextParsingHandler(
        string stepName,
        IPipelineOrchestrator orchestrator,
        SimpleFileStorageConfig config,
        PythonChunkService pythonService,
        ILoggerFactory? loggerFactory = null)
    {
        this.StepName = stepName;
        this._orchestrator = orchestrator;
        this._config = config;
        this._pythonService = pythonService;
        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<TextExtractionHandler>();
        //TODO: implement web scraper in the python script
        
        //this._webScraper = webScraper ?? new WebScraper();
        
        this._log.LogInformation("Handler '{0}' ready", stepName);
        
    }

    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
        //Outils.UpdatePipAndInstallPackages();
        // Appeler à un service FastAPI en Python
        if (this._pythonService.IsStarted == false)
        {
            this._log.LogInformation("Starting Python service");
            await this._pythonService.StartAsync(AppPaths.PythonScript);
        }
        
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
            var sourceFilePath = Path.Combine(this._config.Directory, pipeline.Index, pipeline.DocumentId,sourceFile);
            Console.WriteLine($"Processing file: {sourceFile} - [{sourceFilePath}]");
            var chunks = await _pythonService.GetChunksAsync(sourceFilePath);
            //_pythonService.Dispose(); 
            
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
        _pythonService.Dispose();
        return (ReturnType.Success, pipeline);
    }
    
    

    /// </inheritdoc>
    public void Dispose()
    {
        if (this._webScraper is not IDisposable x) { return; }

        x.Dispose();
    }
    
    
}