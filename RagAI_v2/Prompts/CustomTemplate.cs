namespace RagAI_v2.Prompts;

public static class CustomTemplate
{
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

        public const string SimplePrompt =
            """
            [Règle]
            Tu dois répondre à la question ci-dessous uniquement à partir des documents fournis.
            Ces consignes s’appliquent uniquement à cette question.
            Réponds en français, précisément, sans inventer, sans intro ni conclusion, uniquement selon les faits donnés.
            [Fin de Règle]
            """;
           
    }

    internal static class Chat
    {
        public const string Prompt ="Répodez tousjours en français, sauf que le user demande";
            
       
    }
    
}