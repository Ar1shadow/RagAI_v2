using System.Diagnostics;
using System.Net.Http.Json;

namespace RagAI_v2.Extensions;

public class PythonChunkService : IDisposable
{
    private Process? _process;
    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    private bool _isStarted = false;


    public async Task StartAsync(string pythonScriptPath)
    {
        if (_isStarted) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = pythonScriptPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _process = new Process { StartInfo = startInfo };
        _process.Start();

        // 等待 FastAPI 就绪（轮询 /ping）
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var resp = await _httpClient.GetAsync("http://127.0.0.1:8000/ping");
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

        var response = await _httpClient.PostAsync("http://127.0.0.1:8000/chunk", content);
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
