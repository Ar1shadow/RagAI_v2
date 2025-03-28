using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;

namespace RagAI_v2.Handlers;

public class DeleteLocalFileHandlers : IPipelineStepHandler
{
    private readonly IPipelineOrchestrator _orchestrator;
    private readonly ILogger<DeleteLocalFileHandlers> _logger;
    
    public DeleteLocalFileHandlers(
        string stepName,
        IPipelineOrchestrator orchestrator, 
        ILoggerFactory? logger = null)
    {
        this.StepName = stepName;
        this._orchestrator = orchestrator;
        this._logger = (logger ?? DefaultLogger.Factory).CreateLogger<DeleteLocalFileHandlers>();
    }

    /// <inheritdoc />
    public string StepName { get; }
    
    
    /// <inheritdoc />
    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
        



        return (ReturnType.Success, pipeline);
    }
}