using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RagAI_v2.Extensions;


using Spectre.Console;

namespace RagAI_v2.Test;

public class TestSaveHistory
{
#pragma warning disable SKEXP0070
    public async Task Run()
    {
        // Ajouter le fichier de Config à environnement
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .UpdateChatModelConfig("appsettings.json")
            .Build();

// Choix du Chat modèle
//TODO:integrer dans IOmanager
        var model = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choisir un [green]Chat Modèle[/] : ")
                .AddChoices(config.GetSection("ChatModel:modelId").Get<List<string>>()!));

// Choix de l'embedding modèle
//TODO:integrer dans IOmanager
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
                MaxMatchesCount = 10,
                AnswerTokens = 500,
                Temperature = 0.2,
                TopP = 0.3
            })
            .Build();

// Obtenir le ChatService de SK
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are a helpful assistant.Always answer in French and willing to give a concise answer.");

// Commencer Chat Loop
        var userInput = string.Empty;
        IOmanager.WriteSystem("Welcome to RagAI v2.0"); // TODO:ajouter Spectre Write titre
        while (userInput != "exit")
        {
            IOmanager.WriteUser();
            userInput = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userInput)) continue;
            if (userInput == "exit") break;
            history.AddUserMessage(userInput);

            IOmanager.WriteAssistant();
            var response = new StringBuilder();
            await foreach (var text in
                           chatService.GetStreamingChatMessageContentsAsync(history))
            {
                IOmanager.WriteAssistant(text);
                response.Append(text);
            }
            history.AddAssistantMessage(response.ToString());
        }

        Directory.CreateDirectory(config["ChatHistory:Directory"]);
        history.SaveHistory(config["ChatHistory:Directory"]);
    }
}
