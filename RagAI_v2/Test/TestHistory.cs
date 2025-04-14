using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using RagAI_v2.Extensions;


namespace RagAI_v2.Test;
#pragma warning disable SKEXP0070,SKEXP0001
public class TestHistory
{
     
     public static async Task Run()
     {
          // Ajouter le fichier de Config à environnement
          var config = new ConfigurationBuilder()
               .SetBasePath(AppContext.BaseDirectory)
               .AddJsonFile("appsettings.json", optional: false)
               .UpdateChatModelConfig("appsettings.json")
               .Build();
          // Choix du Chat modèle
          var model = ConsoleIO.WriteSelection("Choisir un [green]Chat Modèle[/] : ",
               config.GetSection("ChatModel:modelId").Get<List<string>>()!);
          
          var kernelBuilder = Kernel.CreateBuilder();
         
          kernelBuilder.Services.AddOllamaChatCompletion(
               modelId: model,
               endpoint: new Uri(config["ChatModel:endpoint"]!));
          var kernel = kernelBuilder.Build();
          
          var chatService = kernel.GetRequiredService<IChatCompletionService>();
          var reducer = new ChatHistoryTruncationReducer(targetCount: 2); // Keep system message and last user message

          var chatHistory = new ChatHistory("You are a librarian and expert on books about cities");

          string[] userMessages = [
               "Recommend a list of books about Seattle",
               "Recommend a list of books about Dublin",
               "Recommend a list of books about Amsterdam",
               "Recommend a list of books about Paris",
               "Recommend a list of books about London"
          ];

          int totalTokenCount = 0;

          foreach (var userMessage in userMessages)
          {
               chatHistory.AddUserMessage(userMessage);

               Console.WriteLine($"\n>>> User:\n{userMessage}");

               var reducedMessages = await reducer.ReduceAsync(chatHistory);

               if (reducedMessages is not null)
               {
                    chatHistory = new ChatHistory(reducedMessages);
               }

               var response = await chatService.GetChatMessageContentAsync(chatHistory);

               chatHistory.AddAssistantMessage(response.Content!);

               Console.WriteLine($"\n>>> Assistant:\n{response.Content!}");

               if (response.InnerContent is OpenAI.Chat.ChatCompletion chatCompletion)
               {
                    totalTokenCount += chatCompletion.Usage?.TotalTokenCount ?? 0;
               }
          }

          Console.WriteLine($"Total Token Count: {totalTokenCount}");



     }
}