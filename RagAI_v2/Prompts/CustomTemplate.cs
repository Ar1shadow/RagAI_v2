namespace RagAI_v2.Prompts;

/// <summary>
/// Template de prompt personnalisé pour les comportements de LLM.
/// </summary>
public static class CustomTemplate
{
    /// <summary>
    /// Template de prompt pour les comportements de RAG.
    /// </summary>
    internal static class Rag
    {
        public const string FactTemplate =
            "=== Last update:{{$meta[last_update]}} ===\nSource:{{$source}}\n{{$content}}\n";

        public const string RecognizeFacts =
            """
                Facts:
                {{$facts}}
                ================
                Given the facts above,if facts are not vide, compactly reformat the facts,
                only reducing spaces and line breaks without deleting any content, to optimize token consumption: .
                Output:                             
            """;

        /// <summary>
        /// Prompt pour affiner les requêtes de recherche vectorielle à partir d'une question.
        /// </summary>
        public const string QueryRefinementPrompt =
            """
            Ta tâche est d'extraire les mots ou expressions clés utiles à une recherche vectorielle à partir de la question fournie.

            **Directives :**
            - Identifiez 3 à 5 termes ou expressions clés qui résument la question.
            - N'inclus aucune traduction. Garde les mots dans leur langue d'origine.
            - Ne reformulez pas la question. Ne fournis pas de réponse ou d'explication.
            - Garder les mots ou expressions clés de la question, sans les déformer.
            - Garder les nom propres (organisation, personne), les acronymes et les abréviations tels qu'ils sont.
            - Répondez uniquement avec des mots ou groupes de mots, séparés par des virgules.
            - Préfèrez des expressions exactes (ex. : "intelligence artificielle") plutôt que des mots isolés.
            - Évitez les mots génériques ou trop larges comme "information", "contenu", "données", "entreprise" sauf si présents explicitement dans la question.
            - Ignorez toute généralisation implicite ou interprétation (ex. Ne expandez "Gourp gamba" à "Group de la musique").
            
            **Exemples :**
            Question : Comment fonctionne la photosynthèse chez les plantes aquatique?
            Requête : photosynthèse, plantes aquatiques, mécanisme de photosynthèse, milieu aquatique

            **Exemples :**
            Question : Combien de personnes travaillent chez Group GAMBA ?
            Requête : effectifs chez Group GAMBA, nombre d'employés

            **Exemples :**
            Question : Quelle la différence entre la fusion nucléaire et la  fission nucléaire ?
            Requête : fusion nucléaire, fission nucléaire, réaction nucléaire, différence entre fusion et fission

            **Exemples :**
            Question : c'est quoi l'IoU ?
            Requête : IoU, Intersection over Union, métrique de vision par ordinateur

            =====
            Question : {{$question}}
            Requête :
            """;

        /// <summary>
        /// Prompt pour répondre à une question en utilisant les résultats de recherche fournis.
        /// </summary>
        public const string Prompt =
            """
            Tu dois répondre à la question ci-dessous uniquement en utilisant les résultats de recherche fournis.
            
            Règles :
            - Réponds en français.
            - Utilise les informations telles qu'elles sont.
            - Sois précis, clair et concis.
            - N'invente rien. Si tu ne sais pas, dis-le.
            - Aucune introduction ni conclusion.
            """;

           
    }

    /// <summary>
    /// Template de prompt pour les interactions de chat.
    /// </summary>
    internal static class Chat
    {
        public const string Prompt ="Répodez tousjours en français, sauf que le user demande";
            
       
    }
    
}