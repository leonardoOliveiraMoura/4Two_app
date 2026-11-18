using System.Text.Encodings.Web;
using System.Text.Json;
using C4Two.Models;

namespace C4Two.Services;

public class VisualizarContaStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _gate = new();
    private readonly string _path;

    public VisualizarContaStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "src", "mocks", "visualizarConta.json");
    }

    public void Save(VisualizarConta conta)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(conta ?? new VisualizarConta(), JsonOptions);
            File.WriteAllText(_path, json + Environment.NewLine);
        }
    }
}
