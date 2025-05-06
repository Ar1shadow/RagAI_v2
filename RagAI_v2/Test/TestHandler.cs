using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using RagAI_v2.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using RagAI_v2.Handlers;
using System.Diagnostics;
using Microsoft.SemanticKernel.ChatCompletion;
using RagAI_v2.Cmd;
using RagAI_v2.Prompts;
using RagAI_v2.Utils;
using System.Text;

#pragma warning disable SKEXP0070

namespace RagAI_v2.Test
{
    internal class TestHandler
    {
        public static async Task Run()
        {
            #region Configuration
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(100));

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

            // Etablir kernel memory
            var memory = new KernelMemoryBuilder()
                .AddSingleton<PythonChunkService,PythonChunkService>()
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
            memory.Orchestrator.AddHandler<CustomTextParsingHandler>(CustomConstants.PipelineStepsParsing);
            
            ConsoleIO.WriteSystem("Handlers ajoutés avec succès");

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
            #endregion



            #region importation des document
            //ConsoleIO.WriteSystem("Supprimer les anciens fichiers");
            //var list = await memory.ListIndexesAsync();
            //foreach (var index in list)
            //{
            //    ConsoleIO.WriteSystem($"Index : {index.Name}");
            //  await memory.DeleteIndexAsync(index.Name);
            //    ConsoleIO.WriteSystem($"Index {index.Name} supprimé");
            //}

            var sw = Stopwatch.StartNew();
            // charger document 
            string[] files = Directory.GetFiles(config["MemoryDB:LocalFileStorage"])
                .Where(file => !Path.GetFileName(file).StartsWith("."))
                .ToArray();
            foreach (var file in files)
            {
                ConsoleIO.WriteSystem($"Fichier : {file}");
            }
            ConsoleIO.WriteSystem("Importation commence");
            for (int i = 4; i < 5; i++)
            {
                var file = files[i];
                var documentID = $"NASA";
                if (!await memory.IsDocumentReadyAsync(documentID))
                {
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
            ConsoleIO.WriteSystem("l'importation avec succès");
            #endregion

            #region importation des document via KM
            //for (int i = 0; i < 1; i++)
            //{
            //    var file = files[i];
            //    var documentID = $"doc{i}KM";
            //    if (!await memory.IsDocumentReadyAsync(documentID))
            //    {
            //        var id = await memory.ImportDocumentAsync(
            //            file,
            //            documentId: documentID,
            //            steps: CustomConstants.DefaultPipeline);
            //        ConsoleIO.WriteSystem($" --Document ID :{documentID} importé à {sw.Elapsed} s");
            //        sw.Restart();
            //    }
            //    else
            //        ConsoleIO.WriteSystem($" --Document ID :{documentID} déja importé");
            //}
            //ConsoleIO.WriteSystem("l'importation avec succès");
            #endregion


            #region chat loop
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
}
