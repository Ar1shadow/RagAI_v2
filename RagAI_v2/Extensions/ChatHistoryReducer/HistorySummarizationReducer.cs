using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace RagAI_v2.Extensions.ChatHistoryReducer;
/// <summary>
/// Réduire l'historique de chat en résumant les messages au-delà du nombre cible de messages.
/// </summary>
/// <remarks>
/// Le résumé va tousjours éviter d'orpheliner le contenu de la fonction, 
/// car la présence d'un appel de fonction _doit_ être suivie d'un résultat de fonction.
/// Quand un seuil de compte est fourni (recommandé), la réduction va scanner dans la fenêtre de seuil
/// afin d'éviter d'orpheliner un message utilisateur d'une réponse d'assistant.
/// </remarks>

#pragma warning disable SKEXP0001
public class HistorySummarizationReducer : IChatHistoryReducer
{
    /// <summary>
    /// clé metadonnée pour indiquer un message de résumé.
    /// </summary>
    public const string SummaryMetadataKey = "__summary__";

    /// <summary>
    /// l'intructions par défaut pour le système de résumé.
    /// </summary>
    public const string DefaultSummarizationPrompt =
        """
        Provide a concise and complete summarization of the entire dialog that does not exceed 5 sentences

        This summary must always:
        - Consider both user and assistant interactions
        - Maintain continuity for the purpose of further dialog
        - Include details from any existing summary
        - Focus on the most significant aspects of the dialog

        This summary must never:
        - Critique, correct, interpret, presume, or assume
        - Identify faults, mistakes, misunderstanding, or correctness
        - Analyze what has not occurred
        - Exclude details from any existing summary
        """;

    /// <summary>
    /// l'instruction pour le système de résumé. Par defaults  <see cref="DefaultSummarizationPrompt"/>.
    /// </summary>
    public string SummarizationInstructions { get; init; } = DefaultSummarizationPrompt;

    /// <summary>
    /// Drapeau pour indiquer si une exception doit être levée lors d'échoue de résumé.
    /// </summary>
    public bool FailOnError { get; init; } = true;

    /// <summary>
    /// Drapeau pour indiquer si la réduction doit être effectuée en utilisant un seul message de résumé 
    /// ou plusieurs messages de résumé au fil du temps.
    /// </summary>
    /// <remarks>
    /// Ne pas utiliser 'SingleSummary' peut finalement entraîner un historique de chat qui dépasse la limite de jetons.
    /// </remarks>
    public bool UseSingleSummary { get; init; }

    /// <summary>
    /// le nombre maximum de messages dans l'historique de chat après réduction.
    /// </summary>
    /// <remarks>
    /// Trop de résumés peuvent entraîner un historique de chat qui dépasse la limite de jetons.
    /// <see cref="UseSingleSummary"/> et <see cref="MaxSummaryCount"/> ne peuvent pas être définis en même temps.
    /// </remarks>
    public int MaxSummaryCount { get; init; }


    /// <summary>
    /// Initialization d'une nouvelle instance de la classe <see cref="HistorySummarizationReducer"/>.
    /// </summary>
    /// <param name="service">A <see cref="IChatCompletionService"/> Instance pour résumer</param>
    /// <param name="targetCount">Le nombre souhaité de meassages cibles après réduction</param>
    /// <param name="thresholdCount">Un nombre optionnel de messages au-delà du 'targetCount' qui 
    /// doivent être présents pour déclencher la réduction.</param>
    /// <param name="summaryCount">Un nombre optionnel de résumés à conserver dans l'historique de chat après réduction.</param>
    /// <remarks>
    /// Bien que 'summaryCount' soit optionnel, il est recommandé de le fournir pour éviter que la réduction ne soit déclenchée
    /// </remarks>>
    public HistorySummarizationReducer(IChatCompletionService service, int targetCount, int? thresholdCount = null, int? summaryCount = null)
    {
        ArgumentNullException.ThrowIfNull(service, nameof(service));
        if (!(targetCount > 0))
        {
            throw new ArgumentException("Target message count must be greater than zero.");
        }
        if (!(!thresholdCount.HasValue || thresholdCount > 0))
        {
            throw new ArgumentException("The reduction threshold length must be greater than zero.");
        }
        this._service = service;
        this._targetCount = targetCount;
        this._thresholdCount = thresholdCount ?? 0;
        
        //If not set the summaryCount, the default value is 1 to match the value of UseSingleSummary
        MaxSummaryCount = summaryCount ?? 1;
        // To avoid confusion, we only allow one of the two parameters to be set.
        UseSingleSummary = MaxSummaryCount == 1;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ChatMessageContent>?> ReduceAsync(IReadOnlyList<ChatMessageContent> chatHistory, CancellationToken cancellationToken = default)
    {
        
        
        var systemMessage = chatHistory.FirstOrDefault(l => l.Role == AuthorRole.System);

        //Identifier le point d'insertion pour les messages de résumé
        int insertionPoint = chatHistory.LocateSummarizationBoundary(SummaryMetadataKey, MaxSummaryCount);

        //déterminer l'index de troncature
        int truncationIndex = chatHistory.LocateSafeReductionIndex(
            this._targetCount,
            this._thresholdCount,
            insertionPoint,
            hasSystemMessage: systemMessage is not null);

        IEnumerable<ChatMessageContent>? truncatedHistory = null;
        IEnumerable<ChatMessageContent>? functionCallsToPreserve = null;

        if (truncationIndex >= 0)
        {
            // Extaire l'historique de chat à résumer
            IEnumerable<ChatMessageContent> summarizedHistory =
                chatHistory.Extract(
                    this.UseSingleSummary ? 0 : insertionPoint,
                    truncationIndex,
                    filter: (m) => m.Items.Any(i => i is FunctionCallContent || i is FunctionResultContent));
            
            // Sauvegarder（function call/result）potentiel dans la plage de résume
            functionCallsToPreserve =
                chatHistory
                .Skip(insertionPoint)
                .Take(truncationIndex - insertionPoint)
                .Where(m => m.Items.Any(i => i is FunctionCallContent || i is FunctionResultContent))
                .ToList();

            try
            {
                // Summarization
                // Il vaut mieux de utiliser 'AuthorRole' comme 'User' au lieu de 'System'
                ChatHistory summarizationRequest = [.. summarizedHistory, new ChatMessageContent(AuthorRole.User, this.SummarizationInstructions)];
                ChatMessageContent summaryMessage = await this._service.GetChatMessageContentAsync(summarizationRequest, cancellationToken: cancellationToken).ConfigureAwait(false);
                summaryMessage.Metadata = new Dictionary<string, object?> { { SummaryMetadataKey, true } };

                // Assemblage de l'historique résumé
                truncatedHistory = AssemblySummarizedHistory(summaryMessage, systemMessage);
            }
            catch
            {
                if (this.FailOnError)
                {
                    throw;
                }
            }
        }

        return truncatedHistory;

        /// <summary>
        /// fonction imbriquée pour assembler l'historique résumé.
        /// </summary>
        IEnumerable<ChatMessageContent> AssemblySummarizedHistory(ChatMessageContent? summaryMessage, ChatMessageContent? systemMessages)
        {
            if (systemMessages is not null)
            {
                yield return systemMessages;
            }

            if (insertionPoint > 0 && !this.UseSingleSummary)
            {
                for (int index = 0; index <= insertionPoint - 1; ++index)
                {
                    yield return chatHistory[index];
                }
            }
            
            if (summaryMessage is not null)
            {
                yield return summaryMessage;
            }

            foreach (var message in functionCallsToPreserve)
            {
                yield return message;
            }
                
            for (int index = truncationIndex; index < chatHistory.Count; ++index)
            {
                yield return chatHistory[index];
            }
        }
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        HistorySummarizationReducer? other = obj as HistorySummarizationReducer;
        return other != null &&
               this._thresholdCount == other._thresholdCount &&
               this._targetCount == other._targetCount &&
               this.UseSingleSummary == other.UseSingleSummary &&
               string.Equals(this.SummarizationInstructions, other.SummarizationInstructions, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(HistorySummarizationReducer), this._thresholdCount, this._targetCount, this.SummarizationInstructions, this.UseSingleSummary);

    private readonly IChatCompletionService _service;
    private readonly int _thresholdCount;
    private readonly int _targetCount;
}
