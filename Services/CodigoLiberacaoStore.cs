using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using C4Two.Models;

namespace C4Two.Services;

public class CodigoLiberacaoStore
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

    private static readonly Regex CodigoValido = new("^4T-\\d{6}$", RegexOptions.CultureInvariant);

    public const string SaidaOk = "checked de saída ok";

    public static readonly string[] NotasAvaliacao = ["Ruim", "Insatisfeito", "Regular", "Satisfeito", "Muito satisfeito"];

    private readonly object _gate = new();
    private readonly string _path;

    public CodigoLiberacaoStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "src", "mocks", "codigosLiberados.json");
    }

    public string Gerar(ClienteSessao cliente, MesaItem mesa, IEnumerable<MesaConsumoItem> itens, string formaDePagamento)
    {
        lock (_gate)
        {
            var lista = Ler();
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in lista.OfType<JsonObject>())
            {
                var existente = item["codigo"]?.ToString();
                if (!string.IsNullOrWhiteSpace(existente))
                {
                    usados.Add(existente);
                }
            }

            string codigo;
            do
            {
                codigo = "4T-" + Random.Shared.Next(0, 1_000_000).ToString("D6");
            }
            while (!usados.Add(codigo));

            lista.Add(new JsonObject
            {
                ["codigo"] = codigo,
                ["dataHora"] = DateTimeOffset.Now.ToString("o"),
                ["formaDePagamento"] = formaDePagamento ?? string.Empty,
                ["cliente"] = JsonSerializer.SerializeToNode(cliente, JsonOptions),
                ["mesa"] = JsonSerializer.SerializeToNode(mesa, JsonOptions),
                ["itensConsumidos"] = JsonSerializer.SerializeToNode(itens?.ToList() ?? new List<MesaConsumoItem>(), JsonOptions)
            });
            Gravar(lista);
            return codigo;
        }
    }

    public List<CodigoLiberacaoLinha> LerLinhas()
    {
        lock (_gate)
        {
            return Ler().OfType<JsonObject>().Select(Linha).ToList();
        }
    }

    public string? MarcarChecklist(string codigo, bool marcado)
    {
        if (string.IsNullOrWhiteSpace(codigo) || (marcado && !CodigoValido.IsMatch(codigo)))
        {
            return null;
        }

        lock (_gate)
        {
            var lista = Ler();
            var alvo = lista.OfType<JsonObject>().FirstOrDefault(item =>
                string.Equals(item["codigo"]?.ToString(), codigo, StringComparison.OrdinalIgnoreCase));
            if (alvo is null || (marcado && !CodigoValido.IsMatch(alvo["codigo"]?.ToString() ?? string.Empty)))
            {
                return null;
            }

            var dataHora = DateTimeOffset.Now.ToString("o");
            alvo["checklistSaida"] = new JsonObject
            {
                ["checked"] = marcado,
                ["saidaOk"] = marcado ? SaidaOk : string.Empty,
                ["dataHora"] = dataHora
            };
            Gravar(lista);
            return dataHora;
        }
    }

    public HashSet<string> SessoesComCodigoValido()
    {
        lock (_gate)
        {
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var registro in Ler().OfType<JsonObject>())
            {
                if (!CodigoValido.IsMatch(registro["codigo"]?.ToString() ?? string.Empty))
                {
                    continue;
                }

                if (registro["itensConsumidos"] is not JsonArray itens)
                {
                    continue;
                }

                foreach (var item in itens.OfType<JsonObject>())
                {
                    var guid = item["sessaoMesaCliente"]?["guid"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(guid))
                    {
                        guids.Add(guid);
                    }
                }
            }

            return guids;
        }
    }

    public bool ContemSessao(Guid sessaoEstabelecimento, Guid sessaoMesa)
    {
        var alvos = Alvos(sessaoEstabelecimento, sessaoMesa);
        if (alvos.Count == 0)
        {
            return false;
        }

        lock (_gate)
        {
            return Ler().OfType<JsonObject>().Any(item => Combina(item, alvos));
        }
    }

    public bool ChecklistConfirmado(Guid sessaoEstabelecimento, Guid sessaoMesa, string? login)
    {
        var alvos = Alvos(sessaoEstabelecimento, sessaoMesa);
        if (alvos.Count == 0)
        {
            return false;
        }

        lock (_gate)
        {
            return Ler().OfType<JsonObject>().Any(item => MesmoCliente(item, login) && Combina(item, alvos) && ChecklistMarcado(item));
        }
    }

    public string NotaDaSessao(Guid sessaoEstabelecimento, Guid sessaoMesa, string? login)
    {
        var alvos = Alvos(sessaoEstabelecimento, sessaoMesa);
        if (alvos.Count == 0)
        {
            return string.Empty;
        }

        lock (_gate)
        {
            var lista = Ler();
            for (var indice = lista.Count - 1; indice >= 0; indice--)
            {
                if (lista[indice] is JsonObject item && MesmoCliente(item, login) && Combina(item, alvos))
                {
                    return item["avaliacao"]?["nota"]?.ToString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    public bool RegistrarAvaliacao(Guid sessaoEstabelecimento, Guid sessaoMesa, string nota, string? login)
    {
        if (Array.IndexOf(NotasAvaliacao, nota) < 0)
        {
            return false;
        }

        var alvos = Alvos(sessaoEstabelecimento, sessaoMesa);
        if (alvos.Count == 0)
        {
            return false;
        }

        lock (_gate)
        {
            var lista = Ler();
            for (var indice = lista.Count - 1; indice >= 0; indice--)
            {
                if (lista[indice] is not JsonObject item || !MesmoCliente(item, login) || !Combina(item, alvos))
                {
                    continue;
                }

                item["avaliacao"] = new JsonObject
                {
                    ["nota"] = nota,
                    ["dataHora"] = DateTimeOffset.Now.ToString("o")
                };
                Gravar(lista);
                return true;
            }
        }

        return false;
    }

    public string Ultimo(string login, int mesaId)
    {
        lock (_gate)
        {
            var lista = Ler();
            for (var indice = lista.Count - 1; indice >= 0; indice--)
            {
                if (lista[indice] is not JsonObject item)
                {
                    continue;
                }

                var loginSalvo = item["cliente"]?["login"]?.ToString() ?? string.Empty;
                var idSalvo = item["mesa"]?["id"]?.GetValue<int>() ?? 0;
                if (idSalvo == mesaId && string.Equals(loginSalvo, login, StringComparison.OrdinalIgnoreCase))
                {
                    return item["codigo"]?.ToString() ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }

    private static HashSet<string> Alvos(Guid sessaoEstabelecimento, Guid sessaoMesa)
    {
        var alvos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (sessaoEstabelecimento != Guid.Empty)
        {
            alvos.Add(sessaoEstabelecimento.ToString());
        }

        if (sessaoMesa != Guid.Empty)
        {
            alvos.Add(sessaoMesa.ToString());
        }

        return alvos;
    }

    private static bool MesmoCliente(JsonObject registro, string? login)
    {
        return string.IsNullOrWhiteSpace(login)
            || string.Equals(registro["cliente"]?["login"]?.ToString(), login, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ChecklistMarcado(JsonObject registro)
    {
        if (registro["checklistSaida"] is not JsonObject checklist)
        {
            return false;
        }

        if (checklist["checked"] is JsonValue valor && valor.TryGetValue<bool>(out var flag))
        {
            return flag;
        }

        return string.Equals(checklist["checked"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Combina(JsonObject registro, HashSet<string> alvos)
    {
        if (!CodigoValido.IsMatch(registro["codigo"]?.ToString() ?? string.Empty))
        {
            return false;
        }

        if (registro["itensConsumidos"] is not JsonArray itens)
        {
            return false;
        }

        foreach (var item in itens.OfType<JsonObject>())
        {
            if (alvos.Contains(item["sessaoEstabelecimento"]?["guid"]?.ToString() ?? string.Empty)
                || alvos.Contains(item["sessaoMesaCliente"]?["guid"]?.ToString() ?? string.Empty)
                || alvos.Contains(item["sessaoMesaCliente"]?["sessaoEstabelecimento"]?["guid"]?.ToString() ?? string.Empty))
            {
                return true;
            }
        }

        return false;
    }

    private static CodigoLiberacaoLinha Linha(JsonObject registro)
    {
        var codigo = registro["codigo"]?.ToString() ?? string.Empty;
        var cliente = registro["cliente"]?["nome"]?.ToString();
        if (string.IsNullOrWhiteSpace(cliente))
        {
            cliente = registro["cliente"]?["login"]?.ToString() ?? "Cliente";
        }

        var itens = new List<string>();
        if (registro["itensConsumidos"] is JsonArray consumo)
        {
            foreach (var nodo in consumo.OfType<JsonObject>())
            {
                var nome = nodo["item"]?["nome"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(nome))
                {
                    itens.Add(nome);
                }
            }
        }

        var marcado = ChecklistMarcado(registro);
        var checklist = registro["checklistSaida"] as JsonObject;

        return new CodigoLiberacaoLinha
        {
            Codigo = codigo,
            CodigoValido = CodigoValido.IsMatch(codigo),
            Cliente = cliente,
            Mesa = registro["mesa"]?["nome"]?.ToString() ?? "Mesa",
            FormaDePagamento = registro["formaDePagamento"]?.ToString() ?? string.Empty,
            Itens = string.Join(" · ", itens),
            ChecklistOk = marcado,
            ChecklistDataHora = checklist?["dataHora"]?.ToString() ?? string.Empty
        };
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
