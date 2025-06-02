using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace RagAI_v2.Extensions.ChatHistoryReducer;

/// <summary>
/// Certains méthodes d'extension pour réduire l'historique de chat.
/// </summary>

public static class ChatHistoryReducerExtensions
{

    /// <summary>
    /// Extraire un sous-ensemble de messages de l'historique de chat source.
    /// </summary>
    /// <param name="chatHistory">l'historique à effectuer</param>
    /// <param name="startIndex">l'index de départ pour l'opération</param>
    /// <param name="finalIndex">L'index du dernier message à extraire</param>
    /// <param name="systemMessage">un message de système optionel à inclure</param>
    /// <param name="filter">Un filtre optionel appliquant à chaque message</param>
    public static IEnumerable<ChatMessageContent> Extract(
        this IReadOnlyList<ChatMessageContent> chatHistory,
        int startIndex,
        int? finalIndex = null,
        ChatMessageContent? systemMessage = null,
        Func<ChatMessageContent, bool>? filter = null)
    {
        int maxIndex = chatHistory.Count - 1;
        if (startIndex > maxIndex)
        {
            yield break;
        }

        if (systemMessage is not null)
        {
            yield return systemMessage;
        }

        finalIndex ??= maxIndex;

        finalIndex = Math.Min(finalIndex.Value, maxIndex);

        for (int index = startIndex; index <= finalIndex; ++index)
        {
            if (filter?.Invoke(chatHistory[index]) ?? false)
            {
                continue;
            }

            yield return chatHistory[index];
        }
    }

    /// <summary>
    /// Identifie l'index du premier message qui n'est pas un message de résumé, comme indiqué par
    /// la présence de la clé de métadonnée spécifiée. Ou l'index du résumé qui dépasse le nombre maximal de résumés.
    /// </summary>
    /// <param name="chatHistory">L'historique source</param>
    /// <param name="summaryKey">La clé de métadonnée qui identifie un message de résumé.</param>
    /// <param name="maxSummaryCount">Le nombre maximal de résumés dans un seul historique de chat</param>

    public static int LocateSummarizationBoundary(this IReadOnlyList<ChatMessageContent> chatHistory, string summaryKey, int maxSummaryCount = 1)
    {
        int summaryCount = 0;
        for (int index = 0; index < chatHistory.Count; ++index)
        {
            ChatMessageContent message = chatHistory[index];

            if (!message.Metadata?.ContainsKey(summaryKey) ?? false || ++summaryCount > maxSummaryCount)
            {
                return index;
            }
        }

        return chatHistory.Count;
    }

    /// <summary>
    /// Identifie l’index du premier message à ou au-delà du nombre cible spécifié qui
    /// ne sépare pas de contenu sensible.
    /// Plus précisément : les appels de fonction et leurs résultats ne doivent pas être séparés, car la complétion de chat exige
    /// qu’un appel de fonction soit toujours suivi d’un résultat de fonction.
    /// De plus, le premier message utilisateur (s’il est présent) dans la fenêtre de seuil sera inclus
    /// afin de maintenir le contexte avec les réponses suivantes de l’assistant.
    /// </summary>
    /// <param name="chatHistory">L’historique source</param>
    /// <param name="targetCount">Le nombre de messages souhaité, si une réduction doit avoir lieu.</param>
    /// <param name="thresholdCount">
    /// Le seuil, au-delà de targetCount, requis pour déclencher la réduction.
    /// L’historique n’est pas réduit si le nombre de messages est inférieur à targetCount + thresholdCount.
    /// </param>
    /// <param name="offsetCount">
    /// Permet d’ignorer éventuellement un décalage depuis le début de l’historique.
    /// Utile lorsque des messages ont été injectés et ne font pas partie du dialogue brut
    /// (comme un résumé).
    /// </param>
    /// <param name="hasSystemMessage">Indique si l’historique de chat contient un message système.</param>
    /// <returns>Un index identifiant le point de départ pour un historique réduit qui ne sépare pas de contenu sensible.</returns> 
    public static int LocateSafeReductionIndex(
        this IReadOnlyList<ChatMessageContent> chatHistory,
        int targetCount,
        int? thresholdCount = null,
        int offsetCount = 0,
        bool hasSystemMessage = false)
    {
        targetCount -= hasSystemMessage ? 1 : 0;

        // Calculer l'index de seuil pour truncation
        int thresholdIndex = chatHistory.Count - (thresholdCount ?? 0) - targetCount;

        if (thresholdIndex <= offsetCount)
        {

            // l'historique est trop court pour être tronqué
            return -1;
        }

        // Calculer l'index de truncation cible
        int messageIndex = chatHistory.Count - targetCount;

        // Sauter les messages de fonction et leurs résultats
        while (messageIndex >= 0)
        {
            if (!chatHistory[messageIndex].Items.Any(i => i is FunctionCallContent || i is FunctionResultContent))
            {
                break;
            }

            --messageIndex;
        }

        // Capturer le premier message non lié aux fonctions
        int targetIndex = messageIndex;

        // Rechercher un message utilisateur dans la plage de troncature pour maximiser la cohésion du chat
        while (messageIndex >= thresholdIndex)
        {
            // Un message utilisateur fournit un excellent point de troncature
            if (chatHistory[messageIndex].Role == AuthorRole.User)
            {
                return messageIndex;
            }

            --messageIndex;
        }
        // Si aucun message utilisateur n'est trouvé, utiliser le message non lié aux fonctions le plus proche
        return targetIndex;
    }
}
