using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using C4Two.Models;

namespace C4Two.Services;

public class MesaConsumoStore
{
    public const string StatusPedidoCozinhaInicial = "Não Iniciado";
    public const string StatusIniciadoEmPreparo = "Iniciado em preparo";
    public const string StatusPreparadoAguardandoEntrega = "Preparado aguardando entrega";

    public static readonly string[] StatusPedidoCozinhaPermitidos =
    {
        StatusPedidoCozinhaInicial,
        StatusIniciadoEmPreparo,
        StatusPreparadoAguardandoEntrega,
        "Iniciado em preparação",
        "Preparado pronto para entrega"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _gate = new();
    private readonly string _path;

    public MesaConsumoStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "src", "mocks", "mesaconsumo.json");
    }

    public List<MesaConsumoItem> Read()
    {
        lock (_gate)
        {
            return Ler();
        }
    }

    public void Save(List<MesaConsumoItem> itens)
    {
        lock (_gate)
        {
            Gravar(itens);
        }
    }

    public List<MesaConsumoItem> GarantirStatusEntrega()
    {
        lock (_gate)
        {
            var itens = Ler();
            var alterou = false;
            foreach (var item in itens)
            {
                if (item.Item is null)
                {
                    item.Item = new ItemConsumoMesa();
                }

                if (string.IsNullOrWhiteSpace(item.Item.StatusEntrega))
                {
                    item.Item.StatusEntrega = "não entregue";
                    alterou = true;
                }

                if (StatusPedidoCozinhaPermitidos.All(status => !string.Equals(status, item.Item.StatusPedidoCozinha, StringComparison.OrdinalIgnoreCase)))
                {
                    item.Item.StatusPedidoCozinha = StatusPedidoCozinhaInicial;
                    alterou = true;
                }
            }

            if (alterou)
            {
                Gravar(itens);
            }

            return itens;
        }
    }

    public MesaConsumoItem? AtualizarStatusEntrega(Guid id, bool entregue)
    {
        lock (_gate)
        {
            var itens = Ler();
            var alvo = itens.FirstOrDefault(item => item.Id == id);
            if (alvo is null)
            {
                return null;
            }

            alvo.Item ??= new ItemConsumoMesa();
            alvo.Item.StatusEntrega = entregue ? "entregue" : "não entregue";
            Gravar(itens);
            return alvo;
        }
    }

    public bool DefinirStatusPedidoCozinha(Guid id, string status)
    {
        var texto = string.IsNullOrWhiteSpace(status) ? StatusPedidoCozinhaInicial : status.Trim();
        var canonico = StatusPedidoCozinhaPermitidos.FirstOrDefault(item =>
            string.Equals(item, texto, StringComparison.OrdinalIgnoreCase));
        if (canonico is null || id == Guid.Empty)
        {
            return false;
        }

        lock (_gate)
        {
            var itens = Ler();
            var alvo = itens.FirstOrDefault(item => item.Id == id);
            if (alvo is null)
            {
                return false;
            }

            alvo.Item ??= new ItemConsumoMesa();
            alvo.Item.StatusPedidoCozinha = canonico;
            return Gravar(itens);
        }
    }

    private List<MesaConsumoItem> Ler()
    {
        if (!File.Exists(_path))
        {
            return new List<MesaConsumoItem>();
        }

        var json = NormalizarConsumo(File.ReadAllText(_path));
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<MesaConsumoItem>();
        }

        using var documento = JsonDocument.Parse(json);
        if (documento.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<MesaConsumoItem>>(json, JsonOptions) ?? new List<MesaConsumoItem>();
        }

        if (documento.RootElement.TryGetProperty("mesaConsumo", out var lista))
        {
            return JsonSerializer.Deserialize<List<MesaConsumoItem>>(lista.GetRawText(), JsonOptions) ?? new List<MesaConsumoItem>();
        }

        return new List<MesaConsumoItem>();
    }

    private static string NormalizarConsumo(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        var raiz = JsonNode.Parse(json);
        var lista = raiz switch
        {
            JsonArray avulsa => avulsa,
            JsonObject objeto => objeto["mesaConsumo"] as JsonArray,
            _ => null
        };
        if (lista is null || raiz is null)
        {
            return json;
        }

        foreach (var nodo in lista)
        {
            if (nodo is not JsonObject consumo || consumo["item"] is not JsonObject produto)
            {
                continue;
            }

            foreach (var campo in new[] { "dataHora", "statusEntrega", "statusPedidoCozinha", "compartilhado" })
            {
                if (consumo[campo] is not JsonNode valor)
                {
                    continue;
                }

                consumo.Remove(campo);
                if (produto[campo] is null)
                {
                    produto[campo] = valor;
                }
            }
        }

        return raiz.ToJsonString();
    }

    private bool Gravar(List<MesaConsumoItem> itens)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(new MesaConsumoArquivo
        {
            MesaConsumo = itens ?? new List<MesaConsumoItem>()
        }, JsonOptions) + Environment.NewLine;
        for (var tentativa = 0; tentativa < 6; tentativa++)
        {
            try
            {
                File.WriteAllText(_path, json);
                return true;
            }
            catch (IOException) when (tentativa < 5)
            {
                Thread.Sleep(150);
            }
            catch (UnauthorizedAccessException) when (tentativa < 5)
            {
                Thread.Sleep(150);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        return false;
    }

    private sealed class MesaConsumoArquivo
    {
        public List<MesaConsumoItem> MesaConsumo { get; set; } = new();
    }
}
