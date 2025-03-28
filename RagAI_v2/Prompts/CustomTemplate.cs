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
        
        public const string KMAnswerPrompt =
            """
                {{$facts}}
                ================
                Given the facts above,if facts are not vide,answer the user's quesiton, the answer maybe inevident, parse
                the key words and provide a clear and rational answer, include source.IF you don't find answer, just reply{{$notFound}}
                question:{{$input}}
            """;
        public const string Prompt =
            """
                Tu es une intelligence artificielle spécialisée dans la recherche d’informations précises. 
                Ta tâche est de répondre à la Question uniquement en utilisant les informations contenues dans SearchResult.
            •	Ne fais pas de suppositions ni d’inventions.
            •	Reformule et structure ta réponse de manière claire et concise.
            •	Si les informations ne sont pas suffisantes, indique-le explicitement.
            •	Évite toute introduction ou conclusion non demandée.                          
            """;
    }
}