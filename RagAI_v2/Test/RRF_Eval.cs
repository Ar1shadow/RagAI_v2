using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.SemanticKernel;
using OllamaSharp;
using RagAI_v2;
using RagAI_v2.Extensions;
using RagAI_v2.Handlers;
using RagAI_v2.SearchClient;
using RagAI_v2.MemoryDataBase.Postgres;
using RagAI_v2.Utils;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml.Wordprocessing;
using RagAI_v2.Prompts;

namespace RagAI_v2.Test
{
    public class Query
    {
        public int QueryId { get; set; }
        public string QueryText { get; set; } = string.Empty;
    }
    public class QueryResult
    {
        public int QueryId { get; set; }
        public string Query { get; set; } = string.Empty;
        public List<ReturnedDoc> Results { get; set; } = new();
    }

    public class ReturnedDoc
    {
        public int Rank { get; set; }
        public string DocumentId { get; set; } = string.Empty;
        public string ExtractedText { get; set; } = string.Empty;
        public float Score { get; set; } = 0.0f;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? IsRelevant { get; set; } //  assigner mannuellement
    }

    public class Evaluator()

    {
        private  string FilePath => Path.Combine(AppPaths.Root,"Test","evaluation_data");

        public async Task SaveEvaluationAsync(List<QueryResult> evaluations)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(evaluations, options);
            if (File.Exists(FilePath+".json"))
            {
                int i = 1;
                for (; File.Exists(FilePath+$"{i}.json");i++)
                {
                    
                }
                await File.WriteAllTextAsync(FilePath + $"{i}.json", json);
            }
            else
                await File.WriteAllTextAsync(FilePath+".json", json);
        }

        public async Task<List<Query>> LoadEvaluationAsync()
        {
            if (!File.Exists(FilePath)) return new();
            var json = await File.ReadAllTextAsync(FilePath);
            return JsonSerializer.Deserialize<List<Query>>(json) ?? new();
        }

        public void PrintSummary(List<QueryResult> evaluations)
        {
            Console.WriteLine("\n===== Résumé de l'évaluation =====\n");
            ///<summary>
            ///Précision d'appel
            ///Indique si au moins un document pertinent a été trouvé dans le top k résultats.
            ///Valeur : 1 si au moins un document pertinent est trouvé, sinon 0.
            ///</summary>
            double totalPrecision = 0;

            ///<summary>
            ///Rang réciproque moyen(MRR)
            ///Moyenne des inverses des rangs des premiers documents pertinents dans les résultats.
            ///Plus le MRR est élevé, meilleure est la qualité des résultats.
            ///</summary>
            double totalMRR = 0;

            ///<summary>
            ///Gain cumulatif actualisé normalisé (nDCG)
            ///Mesure de la qualité du classement des documents pertinents dans les K premiers  résultats.
            ///Valorise les documents pertinents en haut du classement.Score entre 0 et 1.
            ///</summary>
            double totalnDCG = 0;


            int totalQueries = evaluations.Count;

            foreach (var query in evaluations)
            {
                int relevantCount = query.Results.Count(d=>d.IsRelevant != 0);
                ConsoleIO.WriteSystem($"Query #{query.QueryId}: {query.Query}");
                ConsoleIO.WriteSystem($"Documents pertinents: {relevantCount}/{query.Results.Count}");
                //MRR
                var firstRelevant = query.Results.FirstOrDefault(d => d.IsRelevant == 3);
                double mmr = firstRelevant != null ? 1.0 / (firstRelevant.Rank) : 0;
                ConsoleIO.WriteSystem($"MRR: {mmr:0.###}");
                totalMRR += mmr;

                //Precision
                double precision = (double)relevantCount / query.Results.Count;
                ConsoleIO.WriteSystem($"Précision: {precision:0.###}");
                totalPrecision += precision;
                

                //nDCG
                double nDCG = Compute_nDCG(query, 5);
                ConsoleIO.WriteSystem($"nDCG: {nDCG:0.###}");
                totalnDCG += nDCG;
                Console.WriteLine();

            }
            ConsoleIO.WriteSystem("\n===== Résumé global =====\n");
            ConsoleIO.WriteSystem($"Total Precision: {totalPrecision / totalQueries:0.###}");
            ConsoleIO.WriteSystem($"Total MRR: {totalMRR / totalQueries:0.###}");
            ConsoleIO.WriteSystem($"Total nDCG: {totalnDCG / totalQueries:0.###}");
        }

        public double Compute_nDCG(QueryResult query_result, int k)
        {
            if (query_result.Results.Count == 0 || query_result == null || query_result.Results.Any(d => d.IsRelevant == null)) return 0.0;
            
            double dcg = (double)query_result.Results[0].IsRelevant;
            for (int i = 1; i < k; i++)
            {
                var doc = query_result.Results[i];
                if (doc.IsRelevant == 0) continue;
                
                dcg +=  (double)doc.IsRelevant/ Math.Log2(i + 2);    
            }

            var idealRank = query_result.Results.OrderByDescending(x => x.IsRelevant).Take(k).ToList();
            double idcg = (double)idealRank[0].IsRelevant;
            for (int i = 1; i < k; i++)
            {
                var doc = idealRank[i];
                if (doc.IsRelevant == 0) continue;

                idcg += (double)doc.IsRelevant / Math.Log2(i + 2);
            }
            return idcg == 0? 0.0 : dcg / idcg;
        }

        public async Task<List<QueryResult>>EvaluateQueriesAsync(List<Query> queries, IKernelMemory _memory, Kernel kernel)
        {
            var data = new List<QueryResult>();
            foreach (var query in queries)
            {
                #region reformuler par LLM
                var userInputRefined = await UserQueryProcessor.ReformulerUserInput(query.QueryText, kernel);
                ConsoleIO.WriteSystem($"Question réformulée[{query.QueryId}] {query.QueryText} ---> {userInputRefined}");
                #endregion

                // Rechercher dans la BD
                var result = await _memory.SearchAsync(query.QueryText);
                var rankedResults = result.Results.SelectMany(citation => citation.Partitions.Select(p => (Partitions:p, Citation:citation)))
                                    .OrderByDescending(pc => pc.Partitions.Relevance)
                                    .Select((p,i) => new ReturnedDoc
                                    {
                                        Rank = i + 1,
                                        DocumentId = p.Citation.SourceName,
                                        ExtractedText = p.Partitions.Text,
                                        Score = p.Partitions.Relevance
                                    })
                                    .ToList();
                var queryResult = new QueryResult
                {
                    QueryId = query.QueryId,
                    Query = query.QueryText,
                    Results = rankedResults
                };
                
                data.Add(queryResult);
            }

            foreach (var q in data)
            {
                ConsoleIO.WriteLineAssistant($"\n--- Query: {q.Query} ---\n");
                foreach (var doc in q.Results)
                {
                    ConsoleIO.WriteLineAssistant($"[{doc.Rank}-{doc.Score}] {doc.ExtractedText}\nEst-ce pertinent ? (0/1/3): ");
                    var key = Console.ReadKey();
                    doc.IsRelevant = int.Parse(key.KeyChar.ToString());
                    Console.WriteLine();
                }
            }
            await SaveEvaluationAsync(data);
            return data;
        }

        public static async Task run()
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
                    Rrf_K_Text = 30,
                    Rrf_K_Vec = 30,
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
#pragma warning disable SKEXP0070
            kernelBuilder.AddOllamaChatCompletion(
                    ollamaClient: null,
                    serviceId: null);

            var kernel = kernelBuilder.Build();
            #endregion

            // Créer un client de recherche
            var evaluator = new Evaluator();
            var data = new List<Query> { };

            if (data.Count == 0)
            {
                // créer un jet de test
                data = new List<Query>
                {
                    new Query { QueryId = 1, QueryText = "Qui gère les ressources humaines (RH) chez GAMBA ?" },
                    new Query { QueryId = 2, QueryText = "Qui prend en charge des RH ?" },
                    new Query { QueryId = 3, QueryText = "Dis-moi Group Gamba ?" },
                    new Query { QueryId = 4, QueryText = "la valeur d'un ticket-restaurant dans group gamba  ?" },
                    new Query { QueryId = 5, QueryText = "la constitution de service informatique ?" },
                    new Query { QueryId = 6, QueryText = "résumé technique dans la rapport TN09 " },
                    new Query { QueryId = 7, QueryText = "l'histoire de la détection d'objet ?" },
                    new Query { QueryId = 8, QueryText = "les métrique pour évaluerdes modèles de détection d'objet" },
                    new Query { QueryId = 9, QueryText = "le contact utile pour les déplacements ?" },
                    new Query { QueryId = 10, QueryText = "Les règles d'abonnement transports de l'entreprise" },
                    new Query { QueryId = 11, QueryText = "les infos sur la plage de travail " },
                };
            }

            // Évaluer les requêtes
            var results = await evaluator.EvaluateQueriesAsync(data, memory, kernel);
            evaluator.PrintSummary(results);
        }
    }
}
