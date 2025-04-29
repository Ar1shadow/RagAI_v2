// Emplacement recommandé :
// Placer ce fichier dans un dossier appelé `Helpers` ou `Infrastructure` à la racine de votre projet C#.
// Exemple : /RagAI_v2/Helpers/AppPaths.cs

using System;
using System.IO;

namespace RagAI_v2.Utils
{
    /// <summary>
    /// Classe utilitaire pour gérer tous les chemins d’accès relatifs du projet de manière centralisée.
    /// Cela permet de séparer la logique de résolution des chemins du reste de l’application.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>
        /// Détecte automatiquement le répertoire racine du projet, même en mode Debug (bin/Debug/...)
        /// En mode production, retourne simplement le répertoire courant.
        /// </summary>
#if DEBUG
        public static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
#else
        public static readonly string Root = AppContext.BaseDirectory;
#endif

        /// <summary>
        /// Chemin vers le dossier contenant l'historique des requêtes ou réponses utilisateur.
        /// Il est mieux de la mettre dans fichier de config.
        /// </summary>
        public static readonly string HistoryDir = Path.Combine(Root, "Assets", "ChatHistory");

        /// <summary>
        /// Chemin vers le répertoire local des modèles Huggingface (HF_HOME).
        /// </summary>
        public static readonly string HuggingFaceModels = Path.Combine(Root, "Assets", "Models");

        /// <summary>
        /// Chemin vers le script principal de lancement de l'API Python (run_server.py).
        /// </summary>
        public static readonly string PythonScript = Path.Combine(Root, "Extensions", "Python", "run_server.py");

        /// <summary>
        /// Initialise les variables d’environnement nécessaires (comme HF_HOME).
        /// À appeler une seule fois au démarrage de l’application.
        /// </summary>
        public static void ConfigureEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable("HF_HOME", HuggingFaceModels);
            Environment.SetEnvironmentVariable("TRANSFORMERS_OFFLINE", "1");
        }
    }
}
