using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RagAI_v2;
using RagAI_v2.Extensions;
using RagAI_v2.Handlers;
using RagAI_v2.Prompts;
using RagAI_v2.SearchClient;

namespace RagAI_v2.Test
{
    public static class TestSearch
    {
        public static async Task Run() 
        {
            #region Configuration
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
                .WithOllamaTextGeneration(model)
                .WithOllamaTextEmbeddingGeneration(embedding)
                .WithSimpleFileStorage(SimpleFileStorageConfig.Persistent)
                .WithSearchClientConfig(new SearchClientConfig()
                {
                    MaxMatchesCount = 3,
                    Temperature = 0.2,
                    TopP = 0.3
                })
                .WithCustomPostgresMemoryDb(new PostgresConfig()
                {
                    ConnectionString = config["MemoryDB:Postgres:ConnectString"]!,
                    TableNamePrefix = "test-"
                })
                .WithCustomSearchClient<CustomSearchClient>()
                .Build<MemoryServerless>();
            memory.Orchestrator.AddHandler<CustomTextParsingHandler>(CustomConstants.PipelineStepsParsing);
            #endregion

            var sw = Stopwatch.StartNew();
            // charger document 
            string[] files = Directory.GetFiles(config["MemoryDB:LocalFileStorage"])
                .Where(file => !Path.GetFileName(file).StartsWith("."))
                .ToArray();
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var documentID = $"doc{i}";
                if (!await memory.IsDocumentReadyAsync(documentID))
                {
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



            var userInput = string.Empty;
            while (userInput != "exit")
            {
                ConsoleIO.WriteUser();
                userInput = Console.ReadLine() ?? string.Empty;
               
                if (string.IsNullOrWhiteSpace(userInput)) continue;
                if (userInput == "exit") break;

                var searchResult = await memory.SearchAsync(userInput);
               
                //foreach (var result in searchResult.Results)
                //{
                //    ConsoleIO.WriteSystem($"Source : {result.SourceName}");
                //    foreach (var item in result.Partitions) 
                //    {
                //        ConsoleIO.WriteSystem($"Partition : {item.PartitionNumber}");
                //        ConsoleIO.WriteSystem($"Score : {item.Relevance}");
                //        ConsoleIO.WriteSystem($"Texte : {item.Text}");
                //    }
                //}
                var prompt = SearchResultProcessor.FormatSearchResultPrompt(searchResult,userInput);
                ConsoleIO.WriteSystem($"Prompt : {prompt}");


            }
            
            ConsoleIO.WriteSystem("Test Search terminé");
            var list = await memory.ListIndexesAsync();
            foreach (var index in list)
            {
                ConsoleIO.WriteSystem($"Index : {index.Name}");
                await memory.DeleteIndexAsync(index.Name);
                ConsoleIO.WriteSystem($"Index {index.Name} supprimé");
            }
        }
    }
}
