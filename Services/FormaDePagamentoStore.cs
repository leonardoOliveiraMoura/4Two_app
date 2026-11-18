using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using C4Two.Models;

namespace C4Two.Services;

public class FormaDePagamentoStore
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

    public FormaDePagamentoStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "src", "mocks", "formaDePagamento.json");
    }

    public FormaDePagamentoRegistro? Read()
    {
        var registros = Ler();
        return registros.Count == 1 ? registros[0] : null;
    }

    public FormaDePagamentoRegistro? Read(Guid sessao, string login)
    {
        return Ler().FirstOrDefault(item =>
            item.SessaoEstabelecimento?.Guid == sessao
            && string.Equals(item.SessaoMesaCliente?.Cliente?.Login, login, StringComparison.OrdinalIgnoreCase));
    }

    public string Save(FormaDePagamentoRegistro registro)
    {
        lock (_gate)
        {
            var registros = Ler();
            var login = registro.SessaoMesaCliente?.Cliente?.Login ?? string.Empty;
            var sessao = registro.SessaoEstabelecimento?.Guid ?? Guid.Empty;
            registro.MesaConsumo = registro.MesaConsumo
                .Where(item => string.Equals(item.SessaoMesaCliente?.Cliente?.Login, login, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var indice = registros.FindIndex(item =>
                item.SessaoEstabelecimento?.Guid == sessao
                && string.Equals(item.SessaoMesaCliente?.Cliente?.Login, login, StringComparison.OrdinalIgnoreCase));
            if (indice >= 0)
            {
                registros[indice] = registro;
            }
            else
            {
                registros.Add(registro);
            }

            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            JsonNode saida = registros.Count == 1
                ? JsonSerializer.SerializeToNode(registros[0], JsonOptions)!
                : new JsonObject
                {
                    ["formaDePagamentoRegistros"] = new JsonArray(registros.Select(item => (JsonNode)JsonSerializer.SerializeToNode(item, JsonOptions)!).ToArray())
                };
            var json = saida.ToJsonString(JsonOptions);
            File.WriteAllText(_path, json + Environment.NewLine);
            return JsonSerializer.Serialize(registro, JsonOptions);
        }
    }

    private List<FormaDePagamentoRegistro> Ler()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return new List<FormaDePagamentoRegistro>();
            }

            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<FormaDePagamentoRegistro>();
            }

            var raiz = JsonNode.Parse(json);
            if (raiz is JsonObject objeto && objeto["formaDePagamentoRegistros"] is JsonArray lista)
            {
                return lista.OfType<JsonObject>()
                    .Select(item => item.Deserialize<FormaDePagamentoRegistro>(JsonOptions))
                    .Where(item => item is not null)
                    .Cast<FormaDePagamentoRegistro>()
                    .ToList();
            }

            var unico = JsonSerializer.Deserialize<FormaDePagamentoRegistro>(json, JsonOptions);
            return unico is null ? new List<FormaDePagamentoRegistro>() : new List<FormaDePagamentoRegistro> { unico };
        }
    }
}
