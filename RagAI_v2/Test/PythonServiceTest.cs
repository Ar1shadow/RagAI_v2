using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RagAI_v2.Extensions;
using RagAI_v2.Utils;

namespace RagAI_v2.Test
{
    public class PythonServiceTest
    {
        public static async Task Run()
        {
            // Créer une instance du service Python
            using var pythonService = new PythonChunkService();
            Outils.UpdatePipAndInstallPackages();
            try
            {
                // Démarrer le service Python
                ConsoleIO.WriteSystem("Démarrage du Service Python");
                await pythonService.StartAsync(@"Z:\Stagiaires\Pengcheng LI\Code\RagAI_v2\RagAI_v2\Extensions\Python\run_server.py");


                // Chemin du fichier à traiter
                ConsoleIO.WriteSystem("Demande de découpage du fichier");
                string filePath = "/Users/lipengcheng/Downloads/OCR-free.pdf";
                

                // Obtenir les chunks du fichier
                List<string> chunks = await pythonService.GetChunksAsync(filePath);
                

                // Afficher les chunks obtenus
                Console.WriteLine("Chunks obtenus:");
                foreach (var chunk in chunks)
                {
                    Console.WriteLine(chunk);
                }
            }
            catch (Exception ex)
            {
                // Gérer les erreurs
                Console.WriteLine($"Erreur: {ex.Message}");
            }
        }
    }
}