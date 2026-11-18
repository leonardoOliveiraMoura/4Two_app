using System.Text.Encodings.Web;
using System.Text.Json;
using C4Two.Models;

namespace C4Two.Services;

public class CompartilhamentoConsumoStore
{
    public const string Mensagem =
        " incluiu um item na cesta e deseja compartilhar o item, ao compartilhar o item você também podera consumir o item na mesa. O valor do item passa a ser proporcionalmente dividido entre as ocupantes da mesa que aceitaram a divisão do item de consumo.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _gate = new();
    private readonly string _path;

    public CompartilhamentoConsumoStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "src", "mocks", "compartilhamentoConsumo.json");
    }

    public void RegistrarOcupante(Guid sessaoEstabelecimento, int mesaId, int idEstabelecimento, string login, string nome)
    {
        if (sessaoEstabelecimento == Guid.Empty || mesaId <= 0 || string.IsNullOrWhiteSpace(login))
        {
            return;
        }

        lock (_gate)
        {
            var arquivo = Ler();
            var existe = arquivo.Ocupantes.Any(item =>
                item.SessaoEstabelecimento == sessaoEstabelecimento
                && item.MesaId == mesaId
                && item.IdEstabelecimento == idEstabelecimento
                && string.Equals(item.Login, login, StringComparison.OrdinalIgnoreCase));
            if (existe)
            {
                return;
            }

            arquivo.Ocupantes.Add(new OcupanteSentado
            {
                SessaoEstabelecimento = sessaoEstabelecimento,
                MesaId = mesaId,
                IdEstabelecimento = idEstabelecimento,
                Login = login,
                Nome = nome ?? string.Empty
            });
            Gravar(arquivo);
        }
    }

    public void Registrar(CompartilhamentoConsumo oferta)
    {
        if (oferta.Id == Guid.Empty || oferta.ConsumoId == Guid.Empty)
        {
            return;
        }

        lock (_gate)
        {
            var arquivo = Ler();
            arquivo.Compartilhamentos.Add(oferta);
            Gravar(arquivo);
        }
    }

    public List<CompartilhamentoConsumo> Pendentes(Guid sessaoEstabelecimento, int mesaId, int idEstabelecimento, string login)
    {
        if (sessaoEstabelecimento == Guid.Empty || mesaId <= 0 || string.IsNullOrWhiteSpace(login))
        {
            return new List<CompartilhamentoConsumo>();
        }

        lock (_gate)
        {
            var arquivo = Ler();
            var sentado = arquivo.Ocupantes.Any(item =>
                item.SessaoEstabelecimento == sessaoEstabelecimento
                && item.MesaId == mesaId
                && item.IdEstabelecimento == idEstabelecimento
                && string.Equals(item.Login, login, StringComparison.OrdinalIgnoreCase));
            if (!sentado)
            {
                return new List<CompartilhamentoConsumo>();
            }

            return arquivo.Compartilhamentos
                .Where(item => item.SessaoEstabelecimento == sessaoEstabelecimento
                    && item.MesaId == mesaId
                    && item.IdEstabelecimento == idEstabelecimento
                    && !string.Equals(item.LoginAutor, login, StringComparison.OrdinalIgnoreCase)
                    && !Contem(item.Aceites, login)
                    && !Contem(item.Recusas, login))
                .ToList();
        }
    }

    public bool Aceitar(Guid id, string login)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(login))
        {
            return false;
        }

        lock (_gate)
        {
            var arquivo = Ler();
            var oferta = arquivo.Compartilhamentos.FirstOrDefault(item => item.Id == id);
            if (oferta is null || string.Equals(oferta.LoginAutor, login, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            oferta.Aceites ??= new List<string>();
            if (oferta.Aceites.All(aceite => !string.Equals(aceite, login, StringComparison.OrdinalIgnoreCase)))
            {
                oferta.Aceites.Add(login);
            }

            oferta.Recusas = (oferta.Recusas ?? new List<string>())
                .Where(recusa => !string.Equals(recusa, login, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Gravar(arquivo);

            return true;
        }
    }

    public bool Recusar(Guid id, string login)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(login))
        {
            return false;
        }

        lock (_gate)
        {
            var arquivo = Ler();
            var oferta = arquivo.Compartilhamentos.FirstOrDefault(item => item.Id == id);
            if (oferta is null || string.Equals(oferta.LoginAutor, login, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            oferta.Aceites = (oferta.Aceites ?? new List<string>())
                .Where(aceite => !string.Equals(aceite, login, StringComparison.OrdinalIgnoreCase))
                .ToList();
            oferta.Recusas ??= new List<string>();
            if (!Contem(oferta.Recusas, login))
            {
                oferta.Recusas.Add(login);
            }

            Gravar(arquivo);
            return true;
        }
    }

    public ParteIndividual ParteIndividual(Guid consumoId, string login, Guid sessaoEstabelecimento, int mesaId, int idEstabelecimento, decimal precoCheio)
    {
        if (consumoId == Guid.Empty || sessaoEstabelecimento == Guid.Empty || mesaId <= 0 || precoCheio <= 0)
        {
            return new ParteIndividual(false, false, precoCheio);
        }

        lock (_gate)
        {
            var arquivo = Ler();
            var oferta = arquivo.Compartilhamentos.FirstOrDefault(item =>
                item.ConsumoId == consumoId
                && item.SessaoEstabelecimento == sessaoEstabelecimento
                && item.MesaId == mesaId
                && item.IdEstabelecimento == idEstabelecimento);
            if (oferta is null)
            {
                return new ParteIndividual(false, false, precoCheio);
            }

            var sentados = arquivo.Ocupantes
                .Where(item => item.SessaoEstabelecimento == sessaoEstabelecimento
                    && item.MesaId == mesaId
                    && item.IdEstabelecimento == idEstabelecimento)
                .Select(item => item.Login)
                .ToList();
            var participantes = new List<string>();
            if (Sentado(sentados, oferta.LoginAutor))
            {
                participantes.Add(oferta.LoginAutor);
            }

            foreach (var aceite in oferta.Aceites ?? new List<string>())
            {
                if (Sentado(sentados, aceite) && !Contem(participantes, aceite))
                {
                    participantes.Add(aceite);
                }
            }

            if (participantes.Count == 0)
            {
                return new ParteIndividual(true, false, precoCheio);
            }

            var valor = Math.Round(precoCheio / participantes.Count, 2, MidpointRounding.AwayFromZero);
            return new ParteIndividual(true, Contem(participantes, login), valor);
        }
    }

    public List<OcupanteSentado> Ocupantes()
    {
        lock (_gate)
        {
            return Ler().Ocupantes.ToList();
        }
    }

    public List<OcupanteSentado> DaMesa(Guid sessaoEstabelecimento, int mesaId, int idEstabelecimento)
    {
        if (sessaoEstabelecimento == Guid.Empty || mesaId <= 0)
        {
            return new List<OcupanteSentado>();
        }

        lock (_gate)
        {
            return Ler().Ocupantes
                .Where(item => item.SessaoEstabelecimento == sessaoEstabelecimento
                    && item.MesaId == mesaId
                    && item.IdEstabelecimento == idEstabelecimento)
                .ToList();
        }
    }

    public void RemoverPorConsumo(Guid consumoId)
    {
        if (consumoId == Guid.Empty)
        {
            return;
        }

        lock (_gate)
        {
            var arquivo = Ler();
            var restantes = arquivo.Compartilhamentos.Where(item => item.ConsumoId != consumoId).ToList();
            if (restantes.Count == arquivo.Compartilhamentos.Count)
            {
                return;
            }

            arquivo.Compartilhamentos = restantes;
            Gravar(arquivo);
        }
    }

    private CompartilhamentoConsumoArquivo Ler()
    {
        if (!File.Exists(_path))
        {
            return new CompartilhamentoConsumoArquivo();
        }

        var json = File.ReadAllText(_path);
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
        {
            return new CompartilhamentoConsumoArquivo();
        }

        return JsonSerializer.Deserialize<CompartilhamentoConsumoArquivo>(json, JsonOptions) ?? new CompartilhamentoConsumoArquivo();
    }

    private static bool Sentado(IEnumerable<string> sentados, string? login)
    {
        return !string.IsNullOrWhiteSpace(login) && Contem(sentados, login);
    }

    private static bool Contem(IEnumerable<string>? lista, string login)
    {
        return lista?.Any(item => string.Equals(item, login, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private void Gravar(CompartilhamentoConsumoArquivo arquivo)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(arquivo, JsonOptions) + Environment.NewLine);
    }
}
