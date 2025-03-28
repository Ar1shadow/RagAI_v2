using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;


namespace RagAI_v2.Extensions
{
    /// <summary>
    /// Méthode d’extension pour JsonConfigurationExtensions
    /// </summary>
    public static class JsonConfigurationExtensions
    {
        /// <summary>
        /// Méthode d'extension : met automatiquement à jour "ChatModel.modelId" dans appsettings.json
        /// </summary>
        /// <param name="builder"> Configuration builder</param>
        /// <param name="appSettings"> Nom du fichier appsettings.json</param>
        public static IConfigurationBuilder UpdateChatModelConfig(this IConfigurationBuilder builder, string appSettings)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, appSettings);
            //Console.WriteLine(appSettingsPath); 
            if (!File.Exists(appSettingsPath))
            {
                throw new FileNotFoundException($"Le fichier de configuration {appSettingsPath} n'existe pas !");
            }

            // 1. Récupérer tous les modèles installés via Ollama
            List<string> models = GetOllamaModels();

            if (models.Count == 0)
            {
                Console.WriteLine("Aucun modèle trouvé via Ollama, mise à jour annulée.");
                return builder;
            }

            // 2. Mettre à jour le fichier appsettings.json
            UpdateAppSettings(appSettingsPath, models);
            return builder;
        }

        /// <summary>
        /// Exécute `ollama list` pour récupérer tous les noms des modèles installés
        /// </summary>
        private static List<string> GetOllamaModels()
        {
            List<string> modelList = new();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "list",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        Console.WriteLine("Process is null, cannot start ollama list.");
                        return modelList;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(500);

                    string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            string modelName = parts[0]; // Récupérer le champ `NAME`
                            modelList.Add(modelName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la récupération de la liste des modèles Ollama : {ex.Message}");
            }
            
            modelList.RemoveAt(0);
            return modelList;
        }

        /// <summary>
        /// Lit et met à jour appsettings.json avec les modèles récupérés
        /// </summary>
        private static void UpdateAppSettings(string filePath, List<string> models)
        {
            try
            {
                // Lire le fichier JSON
                string json = File.ReadAllText(filePath);
                var jsonObject = JsonNode.Parse(json) ?? new JsonObject();
                
                // Vérifier l'existence de "ChatModel"
                jsonObject["ChatModel"] ??= new JsonObject();
                
                // Créer un JsonArray pour les modèles
                var modelArray = new JsonArray();
                foreach (var model in models)
                {
                    modelArray.Add(model);
                }
                
                // Mettre à jour "modelId" avec le JsonArray
                jsonObject["ChatModel"]!["modelId"] = modelArray;
                
                // S'assurer que le champ "endpoint" reste inchangé
                jsonObject["ChatModel"]!["endpoint"] ??= "http://localhost:11434";

                // Sauvegarder le fichier JSON mis à jour
                File.WriteAllText(filePath, jsonObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Échec de la mise à jour de appsettings.json : {ex.Message}");
            }
        }
    }
}