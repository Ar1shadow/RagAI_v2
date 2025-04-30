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
                // /Users/lipengcheng/RiderProjects/RagAI_v2/RagAI_v2/Extensions/Python/run_server.py
                // Z:\Stagiaires\Pengcheng LI\Code\RagAI_v2\RagAI_v2\Extensions\Python\run_server.py
                await pythonService.StartAsync(@"Z:\Stagiaires\Pengcheng LI\Code\RagAI_v2\RagAI_v2\Extensions\Python\run_server.py");


                // Chemin du fichier à traiter
                ConsoleIO.WriteSystem("Demande de découpage du fichier");
                // Z:\Stagiaires\Pengcheng LI\Code\RagAI_v2\RagAI_v2\Assets\Chenyu SHAO - Rapport de stage GAMBA 2024.pdf
                string filePath = @"Z:\Stagiaires\Pengcheng LI\Code\RagAI_v2\RagAI_v2\Assets\file1-Wikipedia-Carbon.txt";
                

                // Obtenir les chunks du fichier
                List<string> chunks = await pythonService.GetChunksAsync(filePath);
                

                // Afficher les chunks obtenus
                Console.WriteLine("Chunks obtenus:");
                foreach (var chunk in chunks)
                {
                    Console.WriteLine("============");
                    Console.WriteLine(chunk);
                    Console.WriteLine("============");
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