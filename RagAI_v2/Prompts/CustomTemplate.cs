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

            -N'inclus aucune traduction. Garde tous les mots dans la langue d'origine, même s'ils sont abrégés.
            -Ne reformule pas la question. 
            -Ne réponds pas directement. N'explique pas.
            -Réponds uniquement avec des mots ou groupes de mots, séparés par des virgules.
            -Garder les mots ou expressions importantes de la question, sans les déformer.
            -Préfère des groupes de mots exacts plutôt que des mots isolés(ex.: "intelligence artificielle", pas de "intelligence, artificielle").
            -Garder des éléments importants de la question (nom, concepts, objectifs) et les noms propres tels quels (organisations, acronymes, personnes) si ils sont utiles.

            #ExempleQuestion : Qui est le président actuel de SpaceX ?
            Requête : président actuel de SpaceX, Elon Musk, président 

            #ExempleQuestion : Combien de personnes travaillent chez Google ?
            Requête :effectifs chez Google, nombre d'employés        

            #ExempleQuestion : Localisation de Group Gamba ?
            Requête :localisation, Group Gamba, adresse d'agence

            #ExempleQuestion : c'est quoi l'IoU ?
            Requête :IoU, Intersection over Union, définition, vision par ordinateur

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