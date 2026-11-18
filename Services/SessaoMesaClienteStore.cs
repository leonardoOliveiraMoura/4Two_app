using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using C4Two.Models;

namespace C4Two.Services;

public class SessaoMesaClienteStore
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

    public SessaoMesaClienteStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "src", "mocks", "sessaomesacliente.json");
    }

    public SessaoMesaCliente? Read()
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

            return JsonSerializer.Deserialize<SessaoMesaCliente>(json, JsonOptions);
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

    public void Save(SessaoMesaCliente sessao)
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

    public List<OcupanteMesa> Listar()
    {
        lock (_gate)
        {
            return Sessoes(LerNo()).Select(Ocupante).Where(item => item.MesaId > 0).ToList();
        }
    }

    public void AtualizarStatusMesa(int mesaId, int idEstabelecimento, string status)
    {
        lock (_gate)
        {
            var raiz = LerNo();
            if (raiz is null)
            {
                return;
            }

            var alterou = false;
            foreach (var sessao in Sessoes(raiz))
            {
                if (sessao["mesa"] is not JsonObject mesa)
                {
                    continue;
                }

                if (Inteiro(mesa["id"]) != mesaId || Inteiro(mesa["idEstabelecimento"]) != idEstabelecimento)
                {
                    continue;
                }

                if (string.Equals(mesa["status"]?.ToString(), status, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                mesa["status"] = status;
                alterou = true;
            }

            if (alterou)
            {
                File.WriteAllText(_path, raiz.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }) + Environment.NewLine);
            }
        }
    }

    private JsonNode? LerNo()
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

        return JsonNode.Parse(json);
    }

    private static IEnumerable<JsonObject> Sessoes(JsonNode? raiz)
    {
        if (raiz is JsonArray lista)
        {
            return lista.OfType<JsonObject>();
        }

        if (raiz is JsonObject unico)
        {
            return new[] { unico };
        }

        return Array.Empty<JsonObject>();
    }

    private static OcupanteMesa Ocupante(JsonObject sessao)
    {
        var mesa = sessao["mesa"] as JsonObject;
        var cliente = sessao["cliente"] as JsonObject;
        return new OcupanteMesa
        {
            GuidSessao = sessao["guid"]?.ToString() ?? string.Empty,
            MesaId = Inteiro(mesa?["id"]),
            IdEstabelecimento = Inteiro(mesa?["idEstabelecimento"]),
            Login = cliente?["login"]?.ToString() ?? string.Empty
        };
    }

    private static int Inteiro(JsonNode? nodo)
    {
        if (nodo is JsonValue valor && valor.TryGetValue<int>(out var numero))
        {
            return numero;
        }

        return int.TryParse(nodo?.ToString(), out var texto) ? texto : 0;
    }
}
