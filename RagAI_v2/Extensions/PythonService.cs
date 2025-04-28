using System.Diagnostics;
using System.Net.Http.Json;

namespace RagAI_v2.Extensions;

public class PythonChunkService : IDisposable
{
    private Process? _process;
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    private bool _isStarted = false;
    private readonly string _pythonBasePort = "http://127.0.0.1:8000/";
    private bool _disposed = false;

    public async Task StartAsync(string pythonScriptPath)
    {
        if (_isStarted) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            Arguments = $"\"{pythonScriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _process = new Process { StartInfo = startInfo };

        _process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                ConsoleIO.WriteSystem($"[Python stdout] {e.Data}");
        };

        _process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                ConsoleIO.Error($"[Python stderr] {e.Data}");
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        var output = _process.StandardOutput.ReadToEndAsync();
        var error = _process.StandardError.ReadToEndAsync();
        if (output != null)
        {
            Console.WriteLine("Python Service Output: " + await output);
        }
        if (error != null)
        {
            throw new Exception("Python Service Error: " + await error);
        }

        // Attendre que FastAPI soit prêt (scruter /ping)
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var resp = await _httpClient.GetAsync(_pythonBasePort+"ping");
                if (resp.IsSuccessStatusCode)
                {
                    _isStarted = true;
                    return;
                }
            }
            catch
            {
                // reessayer
            }

            await Task.Delay(100);
        }

        if (!_isStarted)
        {
            ConsoleIO.Error("Impossible de démarrer le service Python dans le délai imparti.");

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.Dispose();
                }
            }
            catch { /* Ignorer */ }

            throw new TimeoutException("Le serveur Python n'a pas répondu à temps.");
        }
        throw new Exception("Python Service Failed！");
    }

    public async Task<List<string>> GetChunksAsync(string filePath)
    {
        if (!_isStarted) throw new InvalidOperationException("SLe service Python n'est pas démarré.");
        try
        {
            ConsoleIO.WriteSystem("Envoi du chemin du fichier au service Python...");
            ConsoleIO.WriteSystem("Le chemin du fichier : " + filePath);
            var response = await _httpClient.PostAsJsonAsync(_pythonBasePort+"chunk", new {path = filePath});

            ConsoleIO.WriteSystem("Réponse Http : " + response.StatusCode + response.ReasonPhrase);

            if(!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erreur lors de l'appel au service Python : {errorMessage}");
            }

            var chunks = await response.Content.ReadFromJsonAsync<List<string>>();

            if (chunks == null || chunks.Count == 0)
            {
                throw new Exception("Aucun chunk n'a été renvoyé par le service Python.");
            }
            else
            {
                ConsoleIO.WriteSystem("Le service Python a renvoyé " + chunks.Count + " chunks.");
            }
            return chunks ??new List<string>();
        }catch (TaskCanceledException)
        {
            throw new Exception("Le service Python a pris trop de temps à répondre.");
        }
        catch (Exception ex)
        {
            throw new Exception("Erreur lors de l'appel au service Python : " + ex.Message);
        }

    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_process != null && !_process.HasExited)
            {
                ConsoleIO.WriteSystem("Fermeture du service Python...");

                //_process.CloseMainWindow(); // D'abord essayer de fermer proprement (si GUI)
                if (!_process.WaitForExit(3000)) // Attendre 3 secondes maximum
                {
                    ConsoleIO.Warning("Service Python ne répond pas, tentative de terminaison forcée...");
                    _process.Kill(true); // true = tuer tous les processus enfants
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleIO.Error($"Erreur lors de la fermeture du processus Python: {ex.Message}");
        }
        finally
        {
            _process?.Dispose();
            _process = null;
            ConsoleIO.WriteSystem("Service Python fermé.");
        }
    }
}
