using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using RagAI_v2.Extensions;
using RagAI_v2.Prompts;
using RagAI_v2.Utils;
using RagAI_v2.Cmd;
using Microsoft.KernelMemory.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace RagAI_v2.Test;

public class TestCommand
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

        ConsoleIO.WriteTitre("Welcome to RagAI v2.0");
        // Choix du Chat modèle
        var model = ConsoleIO.WriteSelection("Choisir un [green]Chat Modèle[/] : ",
            config.GetSection("ChatModel:modelId").Get<List<string>>()!);


        // Choix de l'embedding modèle
        var embedding = ConsoleIO.WriteSelection("Choisir un [yellow]Embedding Modèle[/] : ",
            config.GetSection("ChatModel:modelId").Get<List<string>>()!);

        // Configuration de la base de données
        var pgcfg = new PostgresConfig()
        {
            ConnectionString = config["MemoryDB:Postgres:ConnectString"]!,
            TableNamePrefix = "test-"
        };

        // Etablir KM
        var memory = new KernelMemoryBuilder()
            .WithOllamaTextGeneration(model)
            .WithOllamaTextEmbeddingGeneration(embedding)
            .WithSimpleFileStorage(SimpleFileStorageConfig.Persistent)
            .WithPostgresMemoryDb(pgcfg)
            .WithSearchClientConfig(new SearchClientConfig()
            {
                MaxMatchesCount = 3,
                AnswerTokens = 500,
                Temperature = 0.2,
                TopP = 0.3
            })
            .WithCustomTextPartitioningOptions(new TextPartitioningOptions()
            {
                MaxTokensPerParagraph = 700,
                OverlappingTokens = 200,
            })
            .Build<MemoryServerless>();
        
        // SK
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton<HttpClient>(sp =>
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(20),
                BaseAddress = new Uri("http://localhost:11434")
            };
            return client;
        });
        kernelBuilder.Services.AddSingleton<OllamaApiClient>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            return new OllamaApiClient(httpClient, model!);
        });

    kernelBuilder.AddOllamaChatCompletion(
            ollamaClient:null,
            serviceId:null);

        var kernel = kernelBuilder.Build();
        
        
        
        #region Utiliser SearchAsync

        var sw = Stopwatch.StartNew(); 
        // charger document 
        string[] files = Directory.GetFiles(config["MemoryDB:LocalFileStorage"])
            .Where(file=> !Path.GetFileName(file).StartsWith("."))
            .ToArray();
        
        
        for(int i = 0; i < files.Length; i++)
        {
            var file = files[i];
            var documentID = $"doc{i}";
            if (! await memory.IsDocumentReadyAsync(documentID)) {
                var id = await memory.ImportDocumentAsync(
                    file, 
                    documentId: documentID, 
                    steps: CustomConstants.PipelineRagWithoutLocalFiles);
                ConsoleIO.WriteSystem($" --Document ID :{documentID} importé à {sw.Elapsed} s");
                sw.Restart();
            }
            else
                ConsoleIO.WriteSystem($" --Document ID :{documentID} déja importé");
        }
     

       
        
        //var searchAnswer = await memory.SearchAsync("what is NASA's projet");
        //var prompt = SearchResultProcessor.FormatSearchResultPrompt(searchAnswer, "what is NASA's projet");
        //ConsoleIO.WriteAssistant(prompt);
        // Obtenir le ChatService de SK
        
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory(CustomTemplate.Chat.Prompt);
        //Command
        var router = new CommandRouter(config, history, memory, chatService);
        //history.LoadHistory(config["ChatHistoryReducer:Directory"]);
       
        // Commencer Chat Loop
        var userInput = string.Empty;
        ConsoleIO.WriteTitre("Welcome to RagAI v2.0");
        ConsoleIO.WriteUser();
        while (true)
        {
           
            
            userInput = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userInput)) continue;
            if (Outils.IsCommand(userInput))
            {
                await router.HandleCommand(userInput);
                ConsoleIO.WriteUser();
                continue;
            }
            
            
            //if (userInput == "exit") break;
            
            // var search = await memory.SearchAsync(userInput);
            // var prompt = SearchResultProcessor.FormatSearchResultPrompt(search, userInput);
            // ConsoleIO.WriteSystem(prompt);
            // history.AddUserMessage(prompt);
            ConsoleIO.WriteAssistant();
            var response = new StringBuilder();
            history.AddUserMessage(userInput);
            await foreach (var text in
                           chatService.GetStreamingChatMessageContentsAsync(history))
            {
                ConsoleIO.WriteAssistant(text);
                response.Append(text);
            }
            history.AddAssistantMessage(response.ToString());
            ConsoleIO.WriteUser();
        }
        
        
        #endregion

    }
}