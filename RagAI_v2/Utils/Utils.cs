using System.Security.Cryptography;
using System.Diagnostics;

namespace RagAI_v2.Utils;


/// <summary>
/// Classe utilitaire pour les opérations diverses dans l'application.
/// </summary>
public static class Outils
{
    /// <summary>
    /// Vérifie si une chaîne de caractères est une commande.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static bool IsCommand(string? input)
    {
        if (input is null) { return false;}

        return input.StartsWith('/');
    }
    /// <summary>
    /// Calculer le hash SHA256 d'un fichier.
    /// </summary>
    /// <param name="binaryData"></param>
    /// <returns></returns>
    public static string CalculateSHA256(this BinaryData binaryData)
    {
        byte[] byteArray = SHA256.HashData(binaryData.ToMemory().Span);
        return Convert.ToHexString(byteArray).ToLowerInvariant();
    }

    /// <summary>
    /// Exécuter les commandes de la console via le système d'exploitation.
    /// Retourne un tuple (output, error).
    /// output : sortie standard (stdout)
    /// error : sortie d'erreur (stderr)
    /// </summary>
    /// <param name="command">Commande shell à exécuter</param>
    /// <returns>Tuple contenant la sortie standard et la sortie d'erreur</returns>
    public static (string output, string error) RunCommand(string command)
    {
        string fileName;
        string arguments;

        if (OperatingSystem.IsWindows())
        {
            fileName = "cmd.exe";
            arguments = $"/c {command}";
        }
        else
        {
            fileName = "/bin/zsh";
            arguments = $"-c \"{command}\"";
        }

        var processInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(processInfo);
        if (process != null)
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (output, error);
        }

        return ("", "Process not started");
    }



    #region Outils pour python
    /// <summary>
    /// Packages Python nécessaires pour l'application.
    /// </summary>
    public static readonly List<string> PythonPackages = new ()
    {
        "fastapi",
        "uvicorn",
        "pydantic",
        "docling",
        "transformers",
        "langchain-docling",
        "langchain-text-splitters",
        "langchain_community",
        
    };
    
    
    /// <summary>
    /// Met à jour pip et installe les packages Python nécessaires définis dans la liste <c>PythonPackages</c>.
    /// Vérifie d'abord la présence de Python dans l'environnement, met à jour pip, puis installe chaque package
    /// s'il n'est pas déjà présent. Affiche les messages de succès ou d'erreur pour chaque étape.
    /// </summary>
    public static void UpdatePipAndInstallPackages()
    {
        string pythonCmd = OperatingSystem.IsWindows() ? "python" : "python3";

        // Vérifier l'environnement python
        try
        {
            var (output, error) = RunCommand($"{pythonCmd} --version");
            if (!string.IsNullOrWhiteSpace(output) && output.StartsWith("Python"))
            {
                ConsoleIO.WriteSystem("Python existe.");
            }
            else
            {
             
                throw new Exception("Python n'existe pas !");
            }
        }
        catch
        {
            ConsoleIO.Error("Python n'est pas disponible dans l'environnement.");
            return;
        }

        // Mettre à jour pip
        ConsoleIO.WriteSystem("Mise à jour de pip...");
        var (pipOut, pipErr) = RunCommand($"{pythonCmd} -m pip install --upgrade pip");
        if (!string.IsNullOrWhiteSpace(pipErr))
        {
            ConsoleIO.Error($"Erreur lors de la mise à jour de pip: {pipErr}");
        }

        // Installer les packages python
        foreach (var package in PythonPackages)
        {
            try
            {
                var (checkOut, checkErr) = RunCommand($"{pythonCmd} -m pip show {package}");
                if(!string.IsNullOrEmpty(checkOut) && checkOut.Contains("Name:"))
                {
                    ConsoleIO.WriteSystem($"Package {package} déja installé");
                    continue;
                }

                string command = $"{pythonCmd} -m pip install {package}";
                var (installOut, installErr) = RunCommand(command);

                if (!string.IsNullOrWhiteSpace(installErr))
                {
                    ConsoleIO.Error($"Erreur lors de l'installation du package {package}: {installErr}");
                }
                else
                {
                    ConsoleIO.WriteSystem($"Package {package} installé avec succès.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                ConsoleIO.Error($"Erreur inattendue lors de l'installation du package {package}");
                throw;
            }
        }

        ConsoleIO.WriteSystem("Mise à jour des packages terminée.");
    }

    #endregion
}