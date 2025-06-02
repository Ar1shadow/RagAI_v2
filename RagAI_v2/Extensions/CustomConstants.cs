using Microsoft.KernelMemory;

namespace RagAI_v2.Extensions;

public static class CustomConstants
{
    public static class Rag
    {
        // Used to override No Answer config
        public const string EmptyAnswer = "custom_rag_empty_answer_str";

        // Used to override the RAG prompt
        public const string Prompt = "custom_rag_prompt_str";

        // Used to override how facts are injected into RAG prompt
        public const string FactTemplate = "custom_rag_fact_template_str";

        // Used to override if duplicate facts are included in RAG prompts
        public const string IncludeDuplicateFacts = "custom_rag_include_duplicate_facts_bool";

        // Used to override the max tokens to generate when using the RAG prompt
        public const string MaxTokens = "custom_rag_max_tokens_int";

        // Used to override the max matches count used with the RAG prompt
        public const string MaxMatchesCount = "custom_rag_max_matches_count_int";

        // Used to override the temperature (default 0) used with the RAG prompt
        public const string Temperature = "custom_rag_temperature_float";

        // Used to override the nucleus sampling probability (default 0) used with the RAG prompt
        public const string NucleusSampling = "custom_rag_nucleus_sampling_float";
    }
    
    // Pipeline Handlers, Step names
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