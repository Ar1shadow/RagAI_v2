namespace RagAI_v2.Prompts;

public static class CustomTemplate
{
    public static class Rag
    {
        public const string FactTemplate =
            "=== Last update:{{$meta[last_update]}} ===\nSource:{{$source}}\n{{$content}}\n";

        public const string ReorgnizeFacts =
            """
                Facts:
                {{$facts}}
                ================
                Given the facts above,if facts are not vide, compactly reformat the facts,
                only reducing spaces and line breaks without deleting any content, to optimize token consumption: .
                Output:                             
            """;
        
        public static readonly string QueryRefinementPrompt = 
            """
            Tu es un assistant de recherche documentaire.
            Ta tâche est de reformuler la question de l'utilisateur pour optimiser la recherche sémantique.
            Garde l’intention d’origine. Sois plus direct, plus clair, sans politesse ni reformulation inutile.

            Question : "{{question}}"
            Requête optimisée :
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
    }

    public static class Chat
    {
        public const string Prompt ="Répodez tousjours en français, sauf que le user demande";
            
       
    }
    
}