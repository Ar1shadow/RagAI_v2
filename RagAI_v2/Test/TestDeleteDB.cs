﻿using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using RagAI_v2.Extensions;

namespace RagAI_v2.Test
{
    public static class TestDeleteDB
    {
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
                    Temperature = 0.2,
                    TopP = 0.3
                })
                .WithCustomTextPartitioningOptions(new TextPartitioningOptions()
                {
                    MaxTokensPerParagraph = 1000,
                    OverlappingTokens = 200,
                })
                .Build<MemoryServerless>();
            ConsoleIO.WriteSystem("Test commence");
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
