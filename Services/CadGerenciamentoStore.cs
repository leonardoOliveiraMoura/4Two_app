using System.Text.Encodings.Web;
using System.Text.Json;
using C4Two.Models;

namespace C4Two.Services;

public class CadGerenciamentoStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _gate = new();
    private readonly string _estabelecimentosPath;
    private readonly string _itensPath;
    private readonly string _mesasPath;

    public CadGerenciamentoStore(IWebHostEnvironment environment)
    {
        var pasta = Path.Combine(environment.ContentRootPath, "src", "mocks");
        _estabelecimentosPath = Path.Combine(pasta, "cadGerenciamento.json");
        _itensPath = Path.Combine(pasta, "cadastroItensConsumo.json");
        _mesasPath = Path.Combine(pasta, "cadastroMesa.json");
    }

    public CadGerenciamento Read()
    {
        lock (_gate)
        {
            return new CadGerenciamento
            {
                CadastroEstabelecimento = Ler<EstabelecimentoArquivo>(_estabelecimentosPath)?.CadastroEstabelecimento ?? new List<EstabelecimentoItem>(),
                CadastroItensConsumo = Ler<ItensConsumoArquivo>(_itensPath)?.CadastroItensConsumo ?? new List<ItemConsumoItem>(),
                CadastroMesa = Ler<MesaArquivo>(_mesasPath)?.CadastroMesa ?? new List<MesaItem>()
            };
        }
    }

    public void GarantirQrCodes(IList<MesaItem> mesas)
    {
        lock (_gate)
        {
            var alterou = false;
            foreach (var mesa in mesas)
            {
                if (!string.IsNullOrWhiteSpace(mesa.QrCode))
                {
                    continue;
                }

                mesa.QrCode = Guid.NewGuid().ToString("N");
                alterou = true;
            }

            if (alterou)
            {
                Gravar(_mesasPath, new MesaArquivo
                {
                    CadastroMesa = mesas.ToList()
                });
            }
        }
    }

    public bool AtualizarStatusMesa(int id, string status)
    {
        lock (_gate)
        {
            var arquivo = Ler<MesaArquivo>(_mesasPath) ?? new MesaArquivo();
            var mesa = arquivo.CadastroMesa.FirstOrDefault(item => item.Id == id);
            if (mesa == null)
            {
                return false;
            }

            mesa.Status = status;
            Gravar(_mesasPath, arquivo);
            return true;
        }
    }

    public void Save(CadGerenciamento dados)
    {
        lock (_gate)
        {
            Gravar(_estabelecimentosPath, new EstabelecimentoArquivo
            {
                CadastroEstabelecimento = dados.CadastroEstabelecimento ?? new List<EstabelecimentoItem>()
            });
            Gravar(_itensPath, new ItensConsumoArquivo
            {
                CadastroItensConsumo = dados.CadastroItensConsumo ?? new List<ItemConsumoItem>()
            });
            Gravar(_mesasPath, new MesaArquivo
            {
                CadastroMesa = dados.CadastroMesa ?? new List<MesaItem>()
            });
        }
    }

    private static T? Ler<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static void Gravar<T>(string path, T dados)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(dados, JsonOptions);
        File.WriteAllText(path, json + Environment.NewLine);
    }

    private sealed class EstabelecimentoArquivo
    {
        public List<EstabelecimentoItem> CadastroEstabelecimento { get; set; } = new();
    }

    private sealed class ItensConsumoArquivo
    {
        public List<ItemConsumoItem> CadastroItensConsumo { get; set; } = new();
    }

    private sealed class MesaArquivo
    {
        public List<MesaItem> CadastroMesa { get; set; } = new();
    }
}
