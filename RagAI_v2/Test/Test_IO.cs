using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RagAI_v2;
using RagAI_v2.Extensions;
using RagAI_v2.Prompts;

namespace RagAI_v2.Test;

#pragma warning disable SKEXP0070

public static class Test_IO
{
     public static async Task Run()
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
        var history = new ChatHistory(CustomTemplate.Rag.Prompt);

// Commencer Chat Loop
        var userInput = string.Empty;
        while (userInput != "exit")
        {
            ConsoleIO.WriteUser();
            userInput = Console.ReadLine() ?? string.Empty;
            history.AddUserMessage(userInput);
            if (string.IsNullOrWhiteSpace(userInput)) continue;
            if (userInput == "exit") break;
            

            ConsoleIO.WriteAssistant();
            var response = new StringBuilder();
            await foreach (var text in
                           chatService.GetStreamingChatMessageContentsAsync(history))
            {
                ConsoleIO.WriteAssistant(text);
                response.Append(text);
            }
            history.AddAssistantMessage(response.ToString());
        }
        
    }
}

