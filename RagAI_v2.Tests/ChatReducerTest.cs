using Xunit;
using Moq;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using RagAI_v2.ChatHistoryReducer;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RagAI_v2.Extensions;
using Xunit.Abstractions;

namespace RagAI_v2.Tests;
#pragma warning disable SKEXP0070,SKEXP0001

public class ChatReducerTests
{
    private readonly ITestOutputHelper _output;
    public ChatReducerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ReduceAsync_ShouldReturnSummary_WhenExceedsLimit()
    {
        // Ajouter le fichier de Config à environnement
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .UpdateChatModelConfig("appsettings.json")
            .Build();
        // Choix du Chat modèle
        var model = "llama3.2:latest";
          
        var kernelBuilder = Kernel.CreateBuilder();
         
        kernelBuilder.Services.AddOllamaChatCompletion(
            modelId: model,
            endpoint: new Uri(config["ChatModel:endpoint"]!));
        var kernel = kernelBuilder.Build();
          
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        var reducer = new HistorySummarizationReducer(
            service: chatService,
            targetCount: 3,
            summaryCount: 1);

        var chatHistory = new List<ChatMessageContent>
        {
            new(AuthorRole.System, "Contexte du système initial."),
            new(AuthorRole.User, "Bonjour"),
            new(AuthorRole.Assistant, "Salut, comment puis-je vous aider ?"),
            new(AuthorRole.User, "Parle-moi de la météo."),
            new(AuthorRole.Assistant, "Il fait beau aujourd'hui.")
        };
        foreach (var message in chatHistory)
        {
            _output.WriteLine($"{message.Role}: {message.Content}");
        }
        
        // Act
        var result = await reducer.ReduceAsync(chatHistory);

        // Assert
        Assert.NotNull(result);
        foreach (var message in result)
        {
            _output.WriteLine($"{message.Role}: {message.Content}");
        }
        //Assert.Contains(result, m => m.Content?.Contains("résumé") == true);
        
    }
}