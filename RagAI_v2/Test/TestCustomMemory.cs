using System.Diagnostics;
using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.Handlers;
using RagAI_v2.Extensions;

namespace RagAI_v2.Test;

#pragma warning disable SKEXP0070
//TODO:Supprimer tous les fichiers générés par le code
//TODO: Tester un fichier du taille enorme
//TODO: Tester un nombre de fichier important


public class TestCustomMemory
{
 
    
    public async Task Run()
    {
        // Ajouter le fichier de Config à environnement
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .UpdateChatModelConfig("appsettings.json")
            .Build();

        ConsoleIO.WriteTitre("Welcome to RagAI v2.0");
// Choix du Chat modèle
        var model = ConsoleIO.WriteSelection("Choisir un [green]Chat Modèle[/] : ",
            config.GetSection("ChatModel:modelId").Get<List<string>>()!);


// Choix de l'embedding modèle
        var embedding = ConsoleIO.WriteSelection("Choisir un [yellow]Embedding Modèle[/] : ",
            config.GetSection("ChatModel:modelId").Get<List<string>>()!);

// établir Semantic Kernel 
        var builder = Kernel.CreateBuilder();
        builder.AddOllamaChatCompletion(
            modelId: model,
            endpoint: new Uri(config["ChatModel:endpoint"]));
        var kernel = builder.Build();


// Obtenir le ChatService de SK
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        
        
        // Etablir kernel memory
        //
        //Custom-Pipeline
        //- WithoutDefaultHandlers() to remove the default handlers
        //- memory.Orchestrator.AddHandler() to add the customized handlers
        //
        //Custom-Prompt            quand utilisant AskAysnc
        //- WithCustomPromptProvider(IPromptProvider)
        //
        //Custom-PartitionOption
        //- WithCustomTextPartitioningOptions(TextPartitioningOptions)
        //
        //Custom-EmbeddingGenerator ex.103
        //- WithCustomEmbeddingGenerator(ITextEmbeddingGenerator)
        //
        //Custom-LLM ex.104
        //-WithCustomTextGenerator(ITextGenerator)
        //
        //Custom-Content Decoder ex.108
        //- WithContentDecoder<my:IContentDecoder>()
        //- With(new ...)
        //
        //Custom-MemoryServerless
        //- With(new KernelMemoryConfig{... })
        var memory = new KernelMemoryBuilder()
            .WithOllamaTextGeneration(model)
            .WithOllamaTextEmbeddingGeneration(embedding)
            .WithSimpleFileStorage(SimpleFileStorageConfig.Persistent)
            .WithPostgresMemoryDb(config["MemoryDB:Postgres:ConnectString"])
            .WithSearchClientConfig(new SearchClientConfig()
            {
                MaxMatchesCount = 10,
                AnswerTokens = 500,
                Temperature = 0.2,
                TopP = 0.3
            })
            .Build<MemoryServerless>();

        
        var context = new RequestContext();//????
        
    }

}