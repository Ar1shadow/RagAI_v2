using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.KernelMemory;
using RagAI_v2.Extensions;

namespace RagAI_v2.Test;

#pragma warning disable SKEXP0070
public class TestDB
{
    public async Task Run()
    {
        // Ajouter le fichier de Config à environnement
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .UpdateChatModelConfig("appsettings.json")
            .Build();

        IOmanager.WriteTitre("Welcome to RagAI v2.0");
// Choix du Chat modèle
        var model = IOmanager.WriteSelection("Choisir un [green]Chat Modèle[/] : ",
            config.GetSection("ChatModel:modelId").Get<List<string>>()!);


// Choix de l'embedding modèle
        var embedding = IOmanager.WriteSelection("Choisir un [yellow]Embedding Modèle[/] : ",
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
        var memory = new KernelMemoryBuilder()
            .WithOllamaTextGeneration(model)
            .WithOllamaTextEmbeddingGeneration(embedding)
            .WithPostgresMemoryDb()
            .WithSearchClientConfig(new SearchClientConfig()
            {
                MaxMatchesCount = 10,
                AnswerTokens = 500,
                Temperature = 0.2,
                TopP = 0.3
            })
            .Build();
    }

}