using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using RagAI_v2.Utils;


namespace RagAI_v2.Extensions
{
    /// <summary>
    /// Méthode d’extension pour JsonConfigurationExtensions
    /// </summary>
    public static class JsonConfigurationExtensions
    {


        public static IConfigurationBuilder ConfigureInteractiveSettings(this IConfigurationBuilder builder, string setteingPath)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            var appSettingsPath = Path.Combine(AppPaths.Root, setteingPath);
            if (!File.Exists(appSettingsPath))
            {
                throw new FileNotFoundException($"Le fichier de configuration {appSettingsPath} n'existe pas !");
            }
            try
            {
                string json = File.ReadAllText(appSettingsPath);
                var jsonObject = JsonNode.Parse(json) ?? new JsonObject();

                if (jsonObject["ConfigStatus"] is JsonValue jsonNode && jsonNode.ToString() == "Configured")
                {
                    ConsoleIO.WriteSystem("La configuration a déjà été effectuée./nUtiliez la command -[/config] pour la modifier.");
                    return builder;
                }



                //Configurer le chemin de stockage de l'historique
                string? historyPath = ConsoleIO.Ask("Entrez le chemin pour le stockage de l'historique pendant votre conversation[Espace pour utiliser le défaut] ").Trim();
                historyPath = string.IsNullOrWhiteSpace(historyPath) ? historyPath = AppPaths.HistoryDir : historyPath.Trim();
                while (!Path.Exists(historyPath))
                {
                    ConsoleIO.Error($"Le chemin {historyPath} n'existe pas. Veuillez entrer un chemin valide.");
                    historyPath = ConsoleIO.Ask("Entrez le chemin pour le stockage de l'historique pendant votre conversation[Espace pour utiliser le défaut]: ");
                    historyPath = string.IsNullOrWhiteSpace(historyPath) ? historyPath = AppPaths.HistoryDir : historyPath.Trim();
                }
                jsonObject["ChatHistory"] ??= new JsonObject();
                jsonObject["ChatHistory"]!["Directory"] = historyPath;

                //Configurer le chemin de stockage des fichiers
                string? localFileStorage = ConsoleIO.Ask("Entrez le chemin vers le dossier contenant les fichiers à importer dans la base de données: ").Trim();
                while (!Path.Exists(localFileStorage))
                {
                    ConsoleIO.Error($"Le chemin {localFileStorage} n'existe pas. Veuillez entrer un chemin valide.");
                    localFileStorage = ConsoleIO.Ask("Entrez le chemin vers le dossier contenant les fichiers à importer dans la base de données: ").Trim();
                }
                jsonObject["MemoryDB"] ??= new JsonObject();
                jsonObject["MemoryDB"]!["LocalFileStorage"] = localFileStorage;

                //Configurer la connexion à la base de données
                ConsoleIO.WriteSystem("Entrez la configuration de la chaîne de connexion pour PostgresSQL: ");
                string? Host = ConsoleIO.Ask("Entrez le nom d'hôte de la base de données [Espace pour utiliser le défaut : localhost]: ");
                string? Port = ConsoleIO.Ask("Entrez le port de la base de données [Espace pour utiliser le défaut : 5432]: ");
                string? User = ConsoleIO.Ask("Entrez le nom d'utilisateur de la base de données [Espace pour utiliser le défaut : postgres]: ");
                string? Password = ConsoleIO.Ask("Entrez le mot de passe de la base de données [Espace pour utiliser le défaut : null]: ");
                string? Database = ConsoleIO.Ask("Entrez le nom de la base de données [Espace pour utiliser le défaut : postgres]: ");
                Host = string.IsNullOrWhiteSpace(Host) ? Host = "localhost" : Host.Trim();
                Port = string.IsNullOrWhiteSpace(Port) ? Port = "5432" : Port.Trim();
                User = string.IsNullOrWhiteSpace(User) ? User = "postgres" : User.Trim();
                Password = string.IsNullOrWhiteSpace(Password) ? Password = "" : Password.Trim();
                Database = string.IsNullOrWhiteSpace(Database) ? Database = "postgres" : Database.Trim();


                string? ConnectionString = $"Host={Host};Port={Port};Username={User};Password={Password};Database={Database}";
                jsonObject["MemoryDB"]!["Postgres"] ??= new JsonObject();
                jsonObject["MemoryDB"]!["Postgres"]!["ConnectString"] = ConnectionString;

                // Sauvegarder le fichier JSON mis à jour
                jsonObject["ConfigStatus"] = "Configured";
                File.WriteAllText(appSettingsPath, jsonObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                ConsoleIO.WriteSystem($"Configuration interactive terminée. Le fichier de configuration a été mis à jour avec succès dans {appSettingsPath}");

                return builder;
            }
            catch (Exception ex)
            {
                // Log any errors that occur during the save process.
                
                throw new Exception($"Échec de la mise à jour de appsettings.json : {ex.Message}");
            }
            
        }



        /// <summary>
        /// Méthode d'extension : met automatiquement à jour "ChatModel.modelId" dans appsettings.json
        /// </summary>
        /// <param name="builder"> Configuration builder</param>
        /// <param name="appSettings"> Nom du fichier appsettings.json</param>
        public static IConfigurationBuilder UpdateChatModelConfig(this IConfigurationBuilder builder, string appSettings)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            
            var appSettingsPath = Path.Combine(AppPaths.Root, appSettings);
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
            UpdateAppSettingsOllama(appSettingsPath, models);
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
        private static void UpdateAppSettingsOllama(string filePath, List<string> models)
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