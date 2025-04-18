using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace RagAI_v2.Extensions;

//TODO: Mettre limite de taille de l'historique, supprimer les plus anciennes historiques si la dépasser, 
public static class ChatHistoryExtensions
{
    //TODO: Ajouter une option de customisation du nom de sauvegarde <Complete> 
    //TODO: Ajouter une option pour generer un nom de fichier automatique par LLM
    public static void SaveHistory(this ChatHistory history, string savePath)
    {
        // Assurer l'historique non null
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
            var defaultFileName = $"ChatHistory-{timestamp}.json";
            var customFileName = ConsoleIO.Confirm($"Voulez-vous utiliser un nom de fichier personnalisé [Défaut : {defaultFileName}, Espace pour utiliser le défaut] :?");
            var fileName = customFileName? GetFileName(savePath) : defaultFileName;
            
            savePath = Path.Combine(savePath, fileName);
            var cleanedHistory = history.CleanHistory();
            // Serialize the ChatHistory to a text-based format.
            // Assuming ChatHistory has a meaningful .ToString() implementation or a method to serialize.
            var historyJson = JsonSerializer.Serialize(history,new JsonSerializerOptions {WriteIndented = true} );
            File.WriteAllText(savePath, historyJson);
            ConsoleIO.WriteSystem($"Sauvegarde de l'historique de conversation réussie dans {fileName}");
        }
        catch (Exception ex)
        {
            // Log any errors that occur during the save process.
            // Replace with appropriate logging mechanism if available.
            throw new IOException($"Failed to save chat history: {ex.Message}", ex);
        }
        
    }

    //TODO: Ajouter une option pour ne pas charger l'historique <complet>
    //TODO: Ajouter une fonction de sortir le resume de l'ancienne conversation
    public static void LoadHistory(this ChatHistory history, string historyDirectory)
    {
        const string option = "Annuler";
        if(!Directory.Exists(historyDirectory)) 
            throw new DirectoryNotFoundException("Répertoire d’historique introuvable.");
        
        var files = Directory.GetFiles(historyDirectory).Select(Path.GetFileNameWithoutExtension).ToList();
        
        if (files.Count == 0)
        {
            ConsoleIO.Warning("Répertoire d’historique vide.");
            return;
        }

        files.Add(option);
        
        var file = ConsoleIO.WriteSelection("Choisir [green]une historique à charger[/]",files.ToList());
        if (file is not option)
        {
            try
            {   
                file = file + ".json";
                file = Path.Combine(historyDirectory, file);
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
        }else 
            ConsoleIO.WriteSystem("Annuler charger l'historique");
        
    }
    /// <summary>
    /// Supprimer l'historique
    /// </summary>
    /// <param name="history"></param>
    /// <param name="historyDirectory"></param>
    /// <exception cref="IOException"></exception>
    public static void DeleteHistory(this ChatHistory history, string historyDirectory)
    {
        const string option = "Annuler";
        if(!Directory.Exists(historyDirectory)) 
            ConsoleIO.Warning("Répertoire d’historique introuvable.");

        var files = Directory.GetFiles(historyDirectory).ToList();

        if (files.Count == 0)
        {
            ConsoleIO.Warning("Répertoire d’historique Vide.");
            return;
        }

        files.Add(option);
        var file = ConsoleIO.WriteSelection("Choisir [green]une historique à supprimer[/]",files.ToList());
        if (file is not option)
        {
            try
            {
              File.Delete(file);
            }
            catch (Exception ex)
            {
                throw new IOException($"La suppression de l’historique des conversations a échoué: {ex.Message}", ex);
            }
        }else 
            ConsoleIO.WriteSystem("Annuler supprimer l'historique");
        
    }
    /// <summary>
    /// Supprimer les contents vide dans l'historique
    /// </summary>
    /// <param name="history"></param>
    /// <returns>ChatHistory</returns>
    public static ChatHistory CleanHistory(this ChatHistory history)
    {
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var message = history[i];
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                history.RemoveAt(i);
            }
        }
        return history;
    }


    #region Private Methods
    private static string GetFileName(string savepath)
    {
        var fileName = ConsoleIO.Ask($"Nom du fichier à charger :");
        if (Path.Exists(Path.Combine(savepath, fileName)))
        {
            ConsoleIO.Error($"Le fichier {fileName} existe déjà, veuillez choisir un autre nom.");
            return GetFileName(savepath);
        }

        if (!Path.HasExtension(fileName))
        {
            fileName += ".json";
        }
        return fileName;
    }


    #endregion


}