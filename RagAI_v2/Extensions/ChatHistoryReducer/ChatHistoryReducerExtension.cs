using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace RagAI_v2.Extensions.ChatHistoryReducer;

/// <summary>
/// Certains m�thodes d'extension pour r�duire l'historique de chat.
/// </summary>

public static class ChatHistoryReducerExtensions
{

    /// <summary>
    /// Extraire un sous-ensemble de messages de l'historique de chat source.
    /// </summary>
    /// <param name="chatHistory">l'historique � effectuer</param>
    /// <param name="startIndex">l'index de d�part pour l'op�ration</param>
    /// <param name="finalIndex">L'index du dernier message � extraire</param>
    /// <param name="systemMessage">un message de syst�me optionel � inclure</param>
    /// <param name="filter">Un filtre optionel appliquant � chaque message</param>
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
    /// Identifie l'index du premier message qui n'est pas un message de r�sum�, comme indiqu� par
    /// la pr�sence de la cl� de m�tadonn�e sp�cifi�e. Ou l'index du r�sum� qui d�passe le nombre maximal de r�sum�s.
    /// </summary>
    /// <param name="chatHistory">L'historique source</param>
    /// <param name="summaryKey">La cl� de m�tadonn�e qui identifie un message de r�sum�.</param>
    /// <param name="maxSummaryCount">Le nombre maximal de r�sum�s dans un seul historique de chat</param>

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
    /// Identifie l�index du premier message � ou au-del� du nombre cible sp�cifi� qui
    /// ne s�pare pas de contenu sensible.
    /// Plus pr�cis�ment�: les appels de fonction et leurs r�sultats ne doivent pas �tre s�par�s, car la compl�tion de chat exige
    /// qu�un appel de fonction soit toujours suivi d�un r�sultat de fonction.
    /// De plus, le premier message utilisateur (s�il est pr�sent) dans la fen�tre de seuil sera inclus
    /// afin de maintenir le contexte avec les r�ponses suivantes de l�assistant.
    /// </summary>
    /// <param name="chatHistory">L�historique source</param>
    /// <param name="targetCount">Le nombre de messages souhait�, si une r�duction doit avoir lieu.</param>
    /// <param name="thresholdCount">
    /// Le seuil, au-del� de targetCount, requis pour d�clencher la r�duction.
    /// L�historique n�est pas r�duit si le nombre de messages est inf�rieur � targetCount + thresholdCount.
    /// </param>
    /// <param name="offsetCount">
    /// Permet d�ignorer �ventuellement un d�calage depuis le d�but de l�historique.
    /// Utile lorsque des messages ont �t� inject�s et ne font pas partie du dialogue brut
    /// (comme un r�sum�).
    /// </param>
    /// <param name="hasSystemMessage">Indique si l�historique de chat contient un message syst�me.</param>
    /// <returns>Un index identifiant le point de d�part pour un historique r�duit qui ne s�pare pas de contenu sensible.</returns> 
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

            // l'historique est trop court pour �tre tronqu�
            return -1;
        }

        // Calculer l'index de truncation cible
        int messageIndex = chatHistory.Count - targetCount;

        // Sauter les messages de fonction et leurs r�sultats
        while (messageIndex >= 0)
        {
            if (!chatHistory[messageIndex].Items.Any(i => i is FunctionCallContent || i is FunctionResultContent))
            {
                break;
            }

            --messageIndex;
        }

        // Capturer le premier message non li� aux fonctions
        int targetIndex = messageIndex;

        // Rechercher un message utilisateur dans la plage de troncature pour maximiser la coh�sion du chat
        while (messageIndex >= thresholdIndex)
        {
            // Un message utilisateur fournit un excellent point de troncature
            if (chatHistory[messageIndex].Role == AuthorRole.User)
            {
                return messageIndex;
            }

            --messageIndex;
        }
        // Si aucun message utilisateur n'est trouv�, utiliser le message non li� aux fonctions le plus proche
        return targetIndex;
    }
}
