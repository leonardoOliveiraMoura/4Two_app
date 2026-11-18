using System.Text.Encodings.Web;
using System.Text.Json;
using C4Two.Models;

namespace C4Two.Services;

public class SessaoEstabelecimentoStore
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

    public SessaoEstabelecimentoStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "src", "mocks", "sessaoestabelecimento.json");
    }

    public SessaoEstabelecimento? Read()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SessaoEstabelecimento>(json, JsonOptions);
        }
    }

    public void Encerrar()
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, "{}" + Environment.NewLine);
        }
    }

    public void Save(SessaoEstabelecimento sessao)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(sessao, JsonOptions);
            File.WriteAllText(_path, json + Environment.NewLine);
        }
    }
}
