using System.Diagnostics;
using System.Net.Http.Json;

namespace RagAI_v2.Extensions;

public class PythonChunkService : IDisposable
{
    private Process? _process;
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    private bool _isStarted = false;
    private readonly string _pythonBasePort = "http://127.0.0.1:8000/";


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
        _process.Start();

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

            await Task.Delay(500);
        }

        throw new Exception("Python Service Failed！");
    }

    public async Task<List<string>> GetChunksAsync(string filePath)
    {
        if (!_isStarted) throw new InvalidOperationException("Service not started yet");

        var content = new MultipartFormDataContent
        {
            { new StringContent(filePath), "file_path" }
        };

        var response = await _httpClient.PostAsync(_pythonBasePort+"chunk", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<string>>();
        return result ?? new List<string>();
    }

    public void Dispose()
    {
        if (_process != null && !_process.HasExited)
        {
            _process.Kill();
            _process.Dispose();
        }
    }
}
