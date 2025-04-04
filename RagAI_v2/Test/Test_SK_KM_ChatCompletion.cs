
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.SemanticKernel.Connectors.Ollama;
using RagAI_v2.Extensions;
using RagAI_v2.Prompts;

namespace RagAI_v2.Test;
// Test 1 : Modification de Prompt
// Méthode 1 → Context.setArg et AskAsync     <Non, temp de generation>
// Méthode 2 → SearchAsync et Custom fonction à processe les resultats <complete>
public static class Test_SK_KM_ChatCompletion
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

        IOmanager.WriteTitre("Welcome to RagAI v2.0");
// Choix du Chat modèle
        var model = IOmanager.WriteSelection("Choisir un [green]Chat Modèle[/] : ",
            config.GetSection("ChatModel:modelId").Get<List<string>>()!);


// Choix de l'embedding modèle
        var embedding = IOmanager.WriteSelection("Choisir un [yellow]Embedding Modèle[/] : ",
            config.GetSection("ChatModel:modelId").Get<List<string>>()!);


        var pgcfg = new PostgresConfig()
        {
            ConnectionString = config["MemoryDB:Postgres:ConnectString"],
            TableNamePrefix = "test-"
        };
        
        // Etablir kernel memory
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
            .Build<MemoryServerless>();
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOllamaChatCompletion(
            modelId : model,
            endpoint : new Uri(config["ChatModel:endpoint"]));
        var kernel = kernelBuilder.Build();

        memory.Orchestrator.RunPipelineAsync();
       
        

        #region Utiliser SearchAsync
        
        var sw = Stopwatch.StartNew();
        // charger document 
        if (!await memory.IsDocumentReadyAsync("doc1"))
        {
            var id = await memory.ImportDocumentAsync(new Document("doc1")
                .AddFile("/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/file7-submarine.html")
                .AddTag("type","docx"));
            IOmanager.WriteSystem($" --Document ID :{id} importé à {sw.Elapsed} s");
        }else
            IOmanager.WriteSystem($" --Document ID :doc1 déjà importé");
        if (!await memory.IsDocumentReadyAsync("doc3"))
        {
            sw.Restart();
            var id_3 = await memory.ImportDocumentAsync(new Document("doc3")
                .AddFile("/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/file5-NASA-news.pdf")
                .AddTag("type","news"));
            IOmanager.WriteSystem($" --Document ID :{id_3} importé à {sw.Elapsed} s");
        }else
            IOmanager.WriteSystem($" --Document ID :doc3 déjà importé");
        
        //var searchAnswer = await memory.SearchAsync("what is NASA's projet");
        //var prompt = SearchResultProcessor.FormatSearchResultPrompt(searchAnswer, "what is NASA's projet");
        //IOmanager.WriteAssistant(prompt);
        // Obtenir le ChatService de SK
        
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory(CustomTemplate.Rag.Prompt);
        //history.LoadHistory(config["ChatHistory:Directory"]);

// Commencer Chat Loop
        var userInput = string.Empty;
        IOmanager.WriteTitre("Welcome to RagAI v2.0");
        while (userInput != "exit")
        {
            IOmanager.WriteUser();
            userInput = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userInput)) continue;
            if (userInput == "exit") break;
            
            var search = await memory.SearchAsync(userInput);
            var prompt = SearchResultProcessor.FormatSearchResultPrompt(search, userInput);
            history.AddUserMessage(prompt);
            IOmanager.WriteAssistant();
            var response = new StringBuilder();
            await foreach (var text in
                           chatService.GetStreamingChatMessageContentsAsync(history))
            {
                IOmanager.WriteAssistant(text);
                response.Append(text);
            }
            //history.AddAssistantMessage(response.ToString());
        }
        
        history.SaveHistory(config["ChatHistory:Directory"]);
        #endregion



    }
}