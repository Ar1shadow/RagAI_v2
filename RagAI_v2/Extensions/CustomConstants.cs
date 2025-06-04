using Microsoft.KernelMemory;

namespace RagAI_v2.Extensions;

/// <summary>
/// Constantes personnalis�es utilis�es pour configurer le comportement de l'application.
/// </summary>
public static class CustomConstants
{
    public static class Rag
    {
        // Used to override No Answer config
        // Sert � remplacer la r�ponse par d�faut "No Answer" dans les prompts RAG
        public const string EmptyAnswer = "custom_rag_empty_answer_str";

        // Used to override the RAG prompt
        // Sert � remplacer le prompt RAG par d�faut
        public const string Prompt = "custom_rag_prompt_str";

        // Used to override how facts are injected into RAG prompt
        // Sert � remplacer le mod�le de fait par d�faut dans les prompts RAG
        public const string FactTemplate = "custom_rag_fact_template_str";

        // Used to override if duplicate facts are included in RAG prompts
        // Sert � remplacer le comportement par d�faut d'inclusion des faits dupliqu�s dans les prompts RAG
        public const string IncludeDuplicateFacts = "custom_rag_include_duplicate_facts_bool";

        // Used to override the max tokens to generate when using the RAG prompt
        // Sert � remplacer le nombre maximal de jetons g�n�r�s par d�faut lors de l'utilisation du prompt RAG
        public const string MaxTokens = "custom_rag_max_tokens_int";

        // Used to override the max matches count used with the RAG prompt
        // Sert � remplacer le nombre maximal de correspondances par d�faut utilis�es avec le prompt RAG
        public const string MaxMatchesCount = "custom_rag_max_matches_count_int";

        // Used to override the temperature (default 0) used with the RAG prompt
        // Sert � remplacer la temp�rature par d�faut (0) utilis�e avec le prompt RAG
        public const string Temperature = "custom_rag_temperature_float";

        // Used to override the nucleus sampling probability (default 0) used with the RAG prompt
        // Sert � remplacer la probabilit� d'�chantillonnage du noyau par d�faut (0) utilis�e avec le prompt RAG
        public const string NucleusSampling = "custom_rag_nucleus_sampling_float";
    }

    // Pipeline Handlers, Step names
    // Noms des �tapes du pipeline, utilis�s pour les handlers et les logs
    public const string PipelineStepsExtract = "extract";
    public const string PipelineStepsPartition = "partition";
    public const string PipelineStepsGenEmbeddings = "gen_embeddings";
    public const string PipelineStepsSaveRecords = "save_records";
    public const string PipelineStepsSummarize = "summarize";
    public const string PipelineStepsDeleteGeneratedFiles = "delete_generated_files";
    public const string PipelineStepsDeleteDocument = "private_delete_document";
    public const string PipelineStepsDeleteIndex = "private_delete_index";
    public const string PipelineStepsParsing = "extract_partition";

    // Pipeline steps
    // �tapes du pipeline pr�d�finies pour diff�rentes configurations
    public static readonly string[] DefaultPipeline =
    [
        PipelineStepsExtract, PipelineStepsPartition, PipelineStepsGenEmbeddings, PipelineStepsSaveRecords
    ];

    public static readonly string[] PipelineWithoutSummary =
    [
        PipelineStepsExtract, PipelineStepsPartition, PipelineStepsGenEmbeddings, PipelineStepsSaveRecords
    ];

    public static readonly string[] PipelineWithSummary =
    [
        PipelineStepsExtract, PipelineStepsPartition, PipelineStepsGenEmbeddings, PipelineStepsSaveRecords,
        PipelineStepsSummarize, PipelineStepsGenEmbeddings, PipelineStepsSaveRecords
    ];

    public static readonly string[] PipelineOnlySummary =
    [
        PipelineStepsExtract, PipelineStepsSummarize, PipelineStepsGenEmbeddings, PipelineStepsSaveRecords
    ];

    public static readonly string[] PipelineRagWithoutLocalFiles =
    [
        PipelineStepsExtract, PipelineStepsPartition, PipelineStepsGenEmbeddings, PipelineStepsSaveRecords, PipelineStepsDeleteGeneratedFiles
    ];

    public static readonly string[] PipelineCustomParsing =
 [
     PipelineStepsParsing, PipelineStepsGenEmbeddings, PipelineStepsSaveRecords, PipelineStepsDeleteGeneratedFiles
 ];
}