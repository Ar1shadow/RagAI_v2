
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace RagAI_v2.Extensions;

//TODO: Mettre limite de taille de l'historique, supprimer les plus anciennes historiques si la dépasser, 
public static class ChatHistoryExtensions
{
    //TODO: Ajouter une option de customisation du nom de sauvegarde
    //TODO: Ajouter une option pour generer un nom de fichier automatique par LLM
    public static void SaveHistory(this ChatHistory history, string savePath)
    {
        // Assurer history non null
        if (history == null)
        {
            throw new ArgumentNullException(nameof(history), "ChatHistory cannot be null.");
        }
        
        //  Assurer Assets/ChatHistory exists
        string directoryPath = Path.GetDirectoryName(savePath) 
                               ?? throw new InvalidOperationException("Invalid directory path.");
        Directory.CreateDirectory(directoryPath); 

        try
        {
            // Créer le répertoire s'il n'existe pas
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
            var timestamp = DateTime.Now.ToString("dd-MM-yy-HHmm");
            var fileName = $"ChatHistory-{timestamp}.json";
            savePath = Path.Combine(savePath, fileName);
            // Serialize the ChatHistory to a text-based format.
            // Assuming ChatHistory has a meaningful .ToString() implementation or a method to serialize.
            var historyJson = JsonSerializer.Serialize(history,new JsonSerializerOptions {WriteIndented = true} );
            File.WriteAllText(savePath, historyJson);
        }
        catch (Exception ex)
        {
            // Log any errors that occur during the save process.
            // Replace with appropriate logging mechanism if available.
            throw new IOException($"Failed to save chat history: {ex.Message}", ex);
        }
    }



    //TODO: Ajouter une option pour ne pas charger l'historique
    //TODO: Ajouter une fonction de sortir le resume de l'ancienne conversation
    public static void LoadHistory(this ChatHistory history, string historyDirectory)
    {
        if(!Directory.Exists(historyDirectory)) 
            throw new DirectoryNotFoundException("History directory not found.");
        
        var files = Directory.GetFiles(historyDirectory);
        
        if (files.Length == 0) 
            throw new FileNotFoundException("History directory is empty.");
        
        var file = IOmanager.WriteSelection("Choisir [green]une historique à charger[/]",files.ToList());
        
        try
        {
            var json = File.ReadAllText(file);
             var historyDeserialize = JsonSerializer.Deserialize<ChatMessageContent[]>(json);
            
             if (historyDeserialize != null)
             { 
                 history.Clear();
                 history.AddRange(historyDeserialize);
             }
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to load chat history: {ex.Message}", ex);
        }
    }
    
    //TODO: Fonction de Supprimer l'historique
    
}
