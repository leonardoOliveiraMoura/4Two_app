using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using C4Two.Models;

namespace C4Two.Services;

public class AvaliacaoClienteStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions ArquivoOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _gate = new();
    private readonly string _path;

    public AvaliacaoClienteStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "src", "mocks", "avaliacaoCliente.json");
    }

    public int Registrar(SessaoEstabelecimento sessao, SessaoMesaCliente? reserva, string avaliacaoFeita)
    {
        if (sessao.Guid == Guid.Empty || string.IsNullOrWhiteSpace(avaliacaoFeita))
        {
            return 0;
        }

        lock (_gate)
        {
            var lista = Ler();
            var id = 1;
            foreach (var item in lista.OfType<JsonObject>())
            {
                if (item["id"] is JsonValue valor && valor.TryGetValue<int>(out var atual) && atual >= id)
                {
                    id = atual + 1;
                }
            }

            lista.Add(new JsonObject
            {
                ["id"] = id,
                ["sessaoCliente"] = JsonSerializer.SerializeToNode(sessao, JsonOptions),
                ["sessaoMesaCliente"] = reserva is null ? null : JsonSerializer.SerializeToNode(reserva, JsonOptions),
                ["avaliacaoFeita"] = avaliacaoFeita
            });
            Gravar(lista);
            return id;
        }
    }

    public string NotaDaSessao(Guid sessaoEstabelecimento, Guid sessaoMesa, string? login)
    {
        if (sessaoEstabelecimento == Guid.Empty && sessaoMesa == Guid.Empty)
        {
            return string.Empty;
        }

        lock (_gate)
        {
            var lista = Ler();
            for (var indice = lista.Count - 1; indice >= 0; indice--)
            {
                if (lista[indice] is not JsonObject item || !Combina(item, sessaoEstabelecimento, sessaoMesa, login))
                {
                    continue;
                }

                return item["avaliacaoFeita"]?.ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool Combina(JsonObject item, Guid sessaoEstabelecimento, Guid sessaoMesa, string? login)
    {
        var loginSalvo = item["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(login)
            && !string.Equals(loginSalvo, login, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var estabelecimento = item["sessaoCliente"]?["guid"]?.ToString() ?? string.Empty;
        var mesa = item["sessaoMesaCliente"]?["guid"]?.ToString() ?? string.Empty;
        return (sessaoEstabelecimento != Guid.Empty && string.Equals(estabelecimento, sessaoEstabelecimento.ToString(), StringComparison.OrdinalIgnoreCase))
            || (sessaoMesa != Guid.Empty && string.Equals(mesa, sessaoMesa.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private JsonArray Ler()
    {
        if (!File.Exists(_path))
        {
            return new JsonArray();
        }

        var json = File.ReadAllText(_path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonArray();
        }

        var raiz = JsonNode.Parse(json);
        if (raiz is JsonArray lista)
        {
            return lista;
        }

        if (raiz is JsonObject unico)
        {
            return new JsonArray(unico);
        }

        return new JsonArray();
    }

    private void Gravar(JsonArray lista)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, lista.ToJsonString(ArquivoOptions) + Environment.NewLine);
    }
}
