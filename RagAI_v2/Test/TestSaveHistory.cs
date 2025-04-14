using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RagAI_v2.Extensions;
using RagAI_v2.Prompts;
using Spectre.Console;

namespace RagAI_v2.Test;

public static class TestSaveHistory
{
#pragma warning disable SKEXP0070
    public static async Task Run()
    {
        // Ajouter le fichier de Config à environnement
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .UpdateChatModelConfig("appsettings.json")
            .Build();

// Choix du Chat modèle
//TODO:integrer dans ConsoleIO
        var model = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choisir un [green]Chat Modèle[/] : ")
                .AddChoices(config.GetSection("ChatModel:modelId").Get<List<string>>()!));

// Choix de l'embedding modèle
//TODO:integrer dans ConsoleIO
        var embedding = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choisir un[green] Embedding modèle[/] : ")
                .AddChoices(config.GetSection("ChatModel:modelId").Get<List<string>>()!));

// établir Semantic Kernel 
        var builder = Kernel.CreateBuilder();
        builder.AddOllamaChatCompletion(
            modelId: model,
            endpoint: new Uri(config["ChatModel:endpoint"]));
        var kernel = builder.Build();

// Etablir kernel memory
        var memory = new KernelMemoryBuilder()
            .WithOllamaTextGeneration(model)
            .WithOllamaTextEmbeddingGeneration(embedding)
            .WithSearchClientConfig(new SearchClientConfig()
            {
                MaxMatchesCount = 2,
                AnswerTokens = 500,
                Temperature = 0.2,
                TopP = 0.3
            })
            .Build();

// Obtenir le ChatService de SK
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(CustomTemplate.Rag.Prompt);

// Commencer Chat Loop
        var userInput = string.Empty;
        ConsoleIO.WriteTitre("Welcome to RagAI v2.0");
        while (userInput != "exit")
        {
            ConsoleIO.WriteUser();
            userInput = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userInput)) continue;
            if (userInput == "exit") break;
            
            var search = await memory.SearchAsync(userInput);
            var prompt = SearchResultProcessor.FormatSearchResultPrompt(search, userInput);
            history.AddUserMessage(prompt);
            
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
        history.SaveHistory(config["ChatHistoryReducer:Directory"]);
    }
}
