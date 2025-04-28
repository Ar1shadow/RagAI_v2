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
        var model = "llama3.2";
          
        var kernelBuilder = Kernel.CreateBuilder();
         
        kernelBuilder.Services.AddOllamaChatCompletion(
            modelId: model,
            endpoint: new Uri(config["ChatModel:endpoint"]!));
        var kernel = kernelBuilder.Build();
          
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        
        var reducer = new HistorySummarizationReducer(
            service: chatService,
            targetCount: 5,
            summaryCount: 1);

        var chatHistory = new List<ChatMessageContent>
        {
            new(AuthorRole.System, "Contexte du système initial."),
            
            new(AuthorRole.User, "Bonjour"),
            new(AuthorRole.Assistant, "Salut, comment puis-je vous aider ?"),

            new(AuthorRole.User, "Je voudrais organiser un voyage en France."),
            new(AuthorRole.Assistant, "Très bien ! Avez-vous une destination en tête ?"),

            new() {
                Role = AuthorRole.Tool,
                Items = [
                    new FunctionResultContent(
                        functionName: "get_user_allergies",
                        pluginName: "User",
                        callId: "0001",
                        result: "{ \"allergies\": [\"peanuts\", \"gluten\"] }"
                    )
                ]
            },
            
            new(AuthorRole.User, "Je pense à visiter Paris et la région de la Loire."),
            new(AuthorRole.Assistant, "Excellent choix. Préférez-vous les musées, les paysages ou la gastronomie ?"),

            new(AuthorRole.User, "J’aime surtout la culture et les monuments historiques."),
            new(AuthorRole.Assistant, "Dans ce cas, je vous recommande le Louvre, Notre-Dame, et les châteaux de la Loire."),

            new(AuthorRole.User, "Quel est le meilleur moment pour partir ?"),
            new(AuthorRole.Assistant, "Le printemps ou l’automne sont idéaux : il y a moins de monde et le climat est agréable."),

            
            new(AuthorRole.User, "Dois-je réserver les billets à l’avance ?"),
            new(AuthorRole.Assistant, "Oui, surtout pour les musées et les châteaux très fréquentés comme Versailles."),
            
            new(AuthorRole.User, "Bonjour"),
            new(AuthorRole.Assistant, "Salut, comment puis-je vous aider ?"),
            new(AuthorRole.User, "Parle-moi de la météo."),
            new(AuthorRole.Assistant, "Il fait beau aujourd'hui.")
        };
   
        
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