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
            Ta tâche est de comprendre la question et de générer une liste de mots-clés utiles à la recherche vectorielle.

            Ne reformule pas la question. Ne réponds pas. N'explique pas.
            Réponds uniquement avec des mots ou groupes de mots, séparés par des virgules.

            #ExempleQuestion : Qui est le président actuel de SpaceX ?
            Requête : président, SpaceX, Elon Musk

            #ExempleQuestion : Combien de personnes travaillent chez Google ?
            Requête : Google, effectifs, nombre d'employés
            ======
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