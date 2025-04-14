using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using RagAI_v2.Extensions;

namespace RagAI_v2.Test;


//TODO:Supprimer tous les fichiers générés par le code <complete>
//TODO: Tester de supprimer les documents <complete>
//TODO: Tester de supprimer les index <complete>

//TODO: Tester un fichier du taille enorme
//TODO: Tester un nombre de fichier important
//TODO: Avec Index et Filters on peut séparer les documents visibles et non visibles
//LLama3.2 meilleur que mistral
//les documents relevants en fait continnent les informations, LLM ne peut pas repondre à la question.Mais ChatGPT peut repondre à la question.
// La qualite de reponse depende de la capacite de LLM


#pragma warning disable SKEXP0070
public static class TestDB
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
        
        
        // Tester l'importation de document avec Custom Pipeline
        var sw = Stopwatch.StartNew();
        if (!await memory.IsDocumentReadyAsync("doc1"))
        {
            var id_1 = await memory.ImportDocumentAsync(
                "/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/file1-Wikipedia-Carbon.txt",
                documentId:"doc1",
                steps:CustomConstants.PipelineRagWithoutLocalFiles);
            ConsoleIO.WriteSystem($" --Document ID :{id_1}");
            ConsoleIO.WriteSystem($"Tâche se fait à {sw.Elapsed} s");
        }else
            ConsoleIO.WriteSystem($" --Document ID :doc1 déjà importé");
        
        
        // TODO: Tester l'importation d'image (OCR required)
        /*
        sw.Restart();
        var id_2 = await memory.ImportDocumentAsync(
            "/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/file6-ANWC-image.jpg",
            documentId:"img1");
        ConsoleIO.WriteSystem($" --Document ID :{id_2} importé à {sw.Elapsed} s");
        */
        
        //Tester l'importation de document avec tags
        if (!await memory.IsDocumentReadyAsync("doc3"))
        {
            sw.Restart();
            var id_3 = await memory.ImportDocumentAsync(new Document("doc3")
                .AddFile("/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/file5-NASA-news.pdf")
                .AddTag("type","news"));
            ConsoleIO.WriteSystem($" --Document ID :{id_3} importé à {sw.Elapsed} s");
        }else
            ConsoleIO.WriteSystem($" --Document ID :doc3 déjà importé");
        
        
        // Tester l'importation de document excel avec Index
        if (!await memory.IsDocumentReadyAsync("doc4"))
        {
            sw.Restart();
            var id_4 = await memory.ImportDocumentAsync(new Document("doc4")
                .AddFile("/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/file8-data.xlsx")
                .AddTag("type","excel"));
            ConsoleIO.WriteSystem($" --Document ID :{id_4} importé à {sw.Elapsed} s");
        }else
            ConsoleIO.WriteSystem($" --Document ID :doc4 déjà importé");
       
        // Tester l'importation de document excel avec json
        if (!await memory.IsDocumentReadyAsync("doc5"))
        {
            sw.Restart();
            var id_5 = await memory.ImportDocumentAsync(new Document("doc5")
                .AddFile("/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/file9-settings.json")
                .AddTag("type","json"));
            ConsoleIO.WriteSystem($" --Document ID :{id_5} importé à {sw.Elapsed} s");
        }else
            ConsoleIO.WriteSystem($" --Document ID :doc5 déjà importé");
        
        
        if (!await memory.IsDocumentReadyAsync("doc6"))
        {
            sw.Restart();
            var id_6 = await memory.ImportDocumentAsync(new Document("doc6")
                .AddFile("/Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Assets/file3-lorem-ipsum.docx")
                .AddTag("type","docx"));
            ConsoleIO.WriteSystem($" --Document ID :{id_6} importé à {sw.Elapsed} s");
        }
       
        // Tester cherche avec filter
        var qustion = "which countries dont have code in this sheet";
        var Answer = await memory.AskAsync(qustion,index:"shadow",filter: MemoryFilters.ByDocument("doc4"));
        ConsoleIO.WriteLineUser(qustion);
        ConsoleIO.WriteLineAssistant($"Answer{Answer.Result}");
        foreach (var source in Answer.RelevantSources)
        {
            ConsoleIO.WriteLineAssistant($"- {source.SourceName} - {source.Link} [{source.Partitions.First().LastUpdate:d}]");
        }
        
        
        
        // Tester la streaming  Asking using KM
        ConsoleIO.WriteSystem("Test Streaming");
        ConsoleIO.WriteUser();
        var userInput = Console.ReadLine() ?? string.Empty;
        while (userInput is not "exit")
        {
            ConsoleIO.WriteAssistant();
            var answerStreaming = memory.AskStreamingAsync(userInput,
                options: new SearchOptions() { Stream = true });
            List<Citation> sources = [];
            await foreach (var answer in answerStreaming)
            {
                ConsoleIO.WriteAssistant(answer.Result);
                sources.AddRange(answer.RelevantSources);
            }
            Console.WriteLine();
            foreach (var source in sources)
            {
                ConsoleIO.WriteLineAssistant($"- {source.SourceName} - {source.Link} [{source.Partitions.First().LastUpdate:d}]");
            }
            ConsoleIO.WriteSystem("Relevant ressources:");
            foreach (var source in sources)
            {
                ConsoleIO.WriteLineUser($"----First : [{source.Partitions.First().Text}]=====");
                foreach (var partition in source.Partitions)
                {
                    ConsoleIO.WriteLineAssistant($"- [{partition.Text}]======\n");
                } 
            }
            
            
            
            
            ConsoleIO.WriteUser();
            userInput = Console.ReadLine() ?? string.Empty;
        }

        
        
        //await memory.DeleteDocumentAsync(index:"teki",documentId:"doc4");
        //await memory.DeleteDocumentAsync(documentId:"img1");
        //await memory.DeleteIndexAsync(index: "teki");

        await memory.DeleteDocumentAsync("doc4");

    }
    

}