using Microsoft.KernelMemory;

namespace RagAI_v2.Extensions;

/// <summary>
/// Constantes personnalisées utilisées pour configurer le comportement de l'application.
/// </summary>
public static class CustomConstants
{
    public static class Rag
    {
        // Used to override No Answer config
        // Sert à remplacer la réponse par défaut "No Answer" dans les prompts RAG
        public const string EmptyAnswer = "custom_rag_empty_answer_str";

        // Used to override the RAG prompt
        // Sert à remplacer le prompt RAG par défaut
        public const string Prompt = "custom_rag_prompt_str";

        // Used to override how facts are injected into RAG prompt
        // Sert à remplacer le modèle de fait par défaut dans les prompts RAG
        public const string FactTemplate = "custom_rag_fact_template_str";

        // Used to override if duplicate facts are included in RAG prompts
        // Sert à remplacer le comportement par défaut d'inclusion des faits dupliqués dans les prompts RAG
        public const string IncludeDuplicateFacts = "custom_rag_include_duplicate_facts_bool";

        // Used to override the max tokens to generate when using the RAG prompt
        // Sert à remplacer le nombre maximal de jetons générés par défaut lors de l'utilisation du prompt RAG
        public const string MaxTokens = "custom_rag_max_tokens_int";

        // Used to override the max matches count used with the RAG prompt
        // Sert à remplacer le nombre maximal de correspondances par défaut utilisées avec le prompt RAG
        public const string MaxMatchesCount = "custom_rag_max_matches_count_int";

        // Used to override the temperature (default 0) used with the RAG prompt
        // Sert à remplacer la température par défaut (0) utilisée avec le prompt RAG
        public const string Temperature = "custom_rag_temperature_float";

        // Used to override the nucleus sampling probability (default 0) used with the RAG prompt
        // Sert à remplacer la probabilité d'échantillonnage du noyau par défaut (0) utilisée avec le prompt RAG
        public const string NucleusSampling = "custom_rag_nucleus_sampling_float";
    }

    // Pipeline Handlers, Step names
    // Noms des étapes du pipeline, utilisés pour les handlers et les logs
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
    // Étapes du pipeline prédéfinies pour différentes configurations
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