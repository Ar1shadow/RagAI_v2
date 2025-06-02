
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.SemanticKernel.Connectors.Ollama;
using RagAI_v2.Extensions;
using RagAI_v2.Prompts;
using RagAI_v2.Cmd;
using RagAI_v2.Handlers;
using RagAI_v2.SearchClient;
using RagAI_v2.MemoryDataBase.Postgres;
using RagAI_v2.Utils;
using OllamaSharp;
using DocumentFormat.OpenXml.Office2010.Word;


namespace RagAI_v2.Test;
// Test 1 : Modification de Prompt
// Méthode 1 → Context.setArg et AskAsync     <Non, temp de generation>
// Méthode 2 → SearchAsync et Custom fonction à processe les resultats <complete>
public static class Test_SK_KM_ChatCompletion
{
#pragma warning disable SKEXP0070
    public static async Task Run()
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMinutes(100));

        //libérer le port 8000 si il est utilisé
        if (Outils.IsPortUse(8000))
        {
            ConsoleIO.WriteSystem("Port 8000 est déjà utilisé, on va le libérer");
            Outils.KillProcessByPort(8000);
        }

        // Ajouter le fichier de Config à environnement
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .UpdateChatModelConfig("appsettings.json")
            .ConfigureInteractiveSettings("appsettings.json")
            .Build();

        ConsoleIO.WriteTitre("Welcome to RagAI v2.0");
        // Choix du Chat modèle
        var model = ConsoleIO.WriteSelection("Choisir un [green]Chat Modèle[/] : ",
            config.GetSection("ChatModel:modelId").Get<List<string>>()!);


        // Choix de l'embedding modèle
        var embedding = ConsoleIO.WriteSelection("Choisir un [yellow]Embedding Modèle[/] : ",
            config.GetSection("ChatModel:modelId").Get<List<string>>()!);



        // Etablir kernel memory afin de activier le module de recherche
        var memory = new KernelMemoryBuilder()
            .AddSingleton<PythonChunkService, PythonChunkService>()
            .WithOllamaTextGeneration(model)
            .WithOllamaTextEmbeddingGeneration(embedding)
            .WithSimpleFileStorage(SimpleFileStorageConfig.Persistent)
            .WithPostgresMemoryDb(new PostgresConfig()
            {
                ConnectionString = config["MemoryDB:Postgres:ConnectString"]!,
                TableNamePrefix = "test-"
            })
            .WithSearchClientConfig(new SearchClientConfig()
            {
                MaxMatchesCount = 5,
                Temperature = 0.2,
                TopP = 0.3
            })
            .WithCustomPostgresMemoryDb(new CustomPostgresConfig()
            {
                ConnectionString = config["MemoryDB:Postgres:ConnectString"]!,
                TableNamePrefix = "test-",
                UserNormalization = true,
            })
            .WithCustomSearchClient<CustomSearchClient>()
            .Build<MemoryServerless>();
        memory.Orchestrator.AddHandler<CustomTextParsingHandler>(CustomConstants.PipelineStepsParsing);
        ConsoleIO.WriteSystem(" -- Kernel Memory est prêt");

        //Etablir SK afin de activier le module de Chat
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
                ollamaClient: null,
                serviceId: null);
        var kernel = kernelBuilder.Build();


        #region Importation des documents
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
                    steps: CustomConstants.PipelineCustomParsing);
                ConsoleIO.WriteSystem($" --Document ID :{documentID} importé à {sw.Elapsed} s");
                sw.Restart();
            }
            else
                ConsoleIO.WriteSystem($" --Document ID :{documentID} déja importé");
        }
        #endregion

        #region Chat Loop
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory(CustomTemplate.Chat.Prompt);
        var router = new CommandRouter(config, history, memory, chatService, kernel);

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
            

            
            ConsoleIO.WriteAssistant();
            var response = new StringBuilder();

            var sw_answer = Stopwatch.StartNew();
            await foreach (var text in
                           chatService.GetStreamingChatMessageContentsAsync(history,cancellationToken: cts.Token))
            {
                ConsoleIO.WriteAssistant(text);
                response.Append(text);
            }
            ConsoleIO.WriteSystem($"--Reponse est générée à {sw_answer.Elapsed} s");
            history.AddAssistantMessage(response.ToString());
            ConsoleIO.WriteUser();
        }
        
     
        #endregion

    }
}