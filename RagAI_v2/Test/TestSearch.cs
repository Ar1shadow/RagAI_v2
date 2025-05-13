using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OllamaSharp;
using RagAI_v2;
using RagAI_v2.Cmd;
using RagAI_v2.Extensions;
using RagAI_v2.Handlers;
using RagAI_v2.Prompts;
using RagAI_v2.SearchClient;
using RagAI_v2.MemoryDataBase.Postgres;
using RagAI_v2.Utils;

namespace RagAI_v2.Test
{
#pragma warning disable SKEXP0070
    public static class TestSearch
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
            
            // Etablir kernel memory
            var memory = new KernelMemoryBuilder()
                .AddSingleton<PythonChunkService, PythonChunkService>()
                .WithOllamaTextGeneration(model)
                .WithOllamaTextEmbeddingGeneration(embedding)
                .WithSimpleFileStorage(SimpleFileStorageConfig.Persistent)
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
                    UserNormalization = false,
                    Rrf_K_Text = 60,
                    Rrf_K_Vec = 60,
                })
                .WithCustomSearchClient<CustomSearchClient>()
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
            //    await memory.DeleteIndexAsync(index.Name);
            //    ConsoleIO.WriteSystem($"Index {index.Name} supprimé");
            //}


            var sw = Stopwatch.StartNew();
            // charger document 
            string[] files = Directory.GetFiles(config["MemoryDB:LocalFileStorage"]!)
                .Where(file => !Path.GetFileName(file).StartsWith("."))
                .ToArray();
            foreach (var file in files)
            {
                ConsoleIO.WriteSystem($"Fichier : {file}");
            }
            ConsoleIO.WriteSystem("Importation commence");
            for (int i = 0; i < 5; i++)
            {
                var file = files[i];
                var documentID = $"doc{i}";
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






            #region chat loop
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory(CustomTemplate.Chat.Prompt);
            //Command
            var router = new CommandRouter(config, history, memory, chatService, kernel);
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
                var userInputRefined = await UserQueryProcessor.ReformulerUserInput(userInput, kernel);
                ConsoleIO.WriteSystem(userInputRefined);
                var searchAnswer = await memory.SearchAsync(userInputRefined);

                foreach (var citation in searchAnswer.Results)
                {
                    Console.WriteLine($"Source: {citation.SourceName}");
                    if (citation.Partitions.Count != 0)
                    {
                        foreach (var partition in citation.Partitions)
                        {
                            
                            if (!string.IsNullOrWhiteSpace(partition.Text))
                            {
                                ConsoleIO.WriteSystem($"—— {partition.Text}\n-[score : {partition.Relevance}]\n");
                            }
                        }
                    }
                    Console.WriteLine();
                }
                Console.WriteLine();

                //var prompt = SearchResultProcessor.FormatSearchResultPrompt(searchAnswer, userInput);
                //Pour Tester
                //ConsoleIO.WriteSystem($"prompt: {prompt}");

                //ConsoleIO.WriteAssistant();
                //var response = new StringBuilder();
                //history.AddUserMessage(userInput);
                //await foreach (var text in
                //               chatService.GetStreamingChatMessageContentsAsync(history))
                //{
                //    ConsoleIO.WriteAssistant(text);
                //    response.Append(text);
                //}
                //history.AddAssistantMessage(response.ToString());
                ConsoleIO.WriteUser();
            }
            #endregion
        }
    }
}
