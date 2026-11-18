using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using C4Two.Models;

namespace C4Two.Services;

public class ValidacaoPagamentoStore
{
    public static readonly string[] StatusPermitidos =
    {
        "pix ok",
        "recebido em moeda Real ok",
        "recebido via debito ok",
        "recebido via credito ok",
        "voucher ticket ok"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _gate = new();
    private readonly string _path;

    public ValidacaoPagamentoStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "src", "mocks", "validacaoPagamentos.json");
    }

    public static bool EstaAprovado(string? statusPagamento) =>
        StatusPermitidos.Any(status => string.Equals(status, statusPagamento?.Trim(), StringComparison.OrdinalIgnoreCase));

    public string StatusDoCliente(string guidMesaCliente)
    {
        return StatusDoCliente(string.Empty, string.Empty, guidMesaCliente);
    }

    public string StatusDoCliente(string sessao, string login, string guidMesaCliente)
    {
        lock (_gate)
        {
            var registros = Registros(LerNo()).ToList();
            JsonObject? alvo = null;
            if (!string.IsNullOrWhiteSpace(sessao) && !string.IsNullOrWhiteSpace(login))
            {
                var chave = ContaCliente.ChavePaga(Guid.TryParse(sessao, out var guid) ? guid : Guid.Empty, login);
                alvo = registros.FirstOrDefault(registro =>
                    string.Equals(Chave(registro), chave, StringComparison.OrdinalIgnoreCase));
            }

            if (alvo is null && !string.IsNullOrWhiteSpace(guidMesaCliente))
            {
                var doGuid = registros.Where(registro =>
                    string.Equals(registro["sessaoMesaCliente"]?["guid"]?.ToString(), guidMesaCliente, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (!string.IsNullOrWhiteSpace(login))
                {
                    alvo = doGuid.FirstOrDefault(registro =>
                        string.Equals(registro["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString(), login, StringComparison.OrdinalIgnoreCase));
                }

                if (alvo is null && string.IsNullOrWhiteSpace(login) && doGuid.Count == 1)
                {
                    alvo = doGuid[0];
                }
            }

            if (alvo is null && string.IsNullOrWhiteSpace(login) && registros.Count == 1)
            {
                alvo = registros[0];
            }

            return alvo?["statusPagamento"]?.ToString() ?? string.Empty;
        }
    }

    public void CriarDePagamento(string jsonPagamento)
    {
        if (string.IsNullOrWhiteSpace(jsonPagamento))
        {
            return;
        }

        var origem = JsonNode.Parse(jsonPagamento) as JsonObject;
        if (origem is null)
        {
            return;
        }

        var novo = new JsonObject
        {
            ["formaDePagamento"] = origem["formaDePagamento"]?.DeepClone() ?? string.Empty,
            ["statusPagamento"] = string.Empty,
            ["tokenPago"] = string.Empty
        };
        foreach (var propriedade in origem)
        {
            if (propriedade.Key is "formaDePagamento" or "statusPagamento" or "tokenPago")
            {
                continue;
            }

            novo[propriedade.Key] = propriedade.Value?.DeepClone();
        }

        ManterItensDoCliente(novo);

        lock (_gate)
        {
            var registros = Registros(LerNo()).Select(item => item.DeepClone().AsObject()).ToList();
            var chave = Chave(novo);
            var indice = registros.FindIndex(item => string.Equals(Chave(item), chave, StringComparison.OrdinalIgnoreCase));
            if (indice >= 0)
            {
                novo["statusPagamento"] = registros[indice]["statusPagamento"]?.DeepClone() ?? string.Empty;
                novo["tokenPago"] = registros[indice]["tokenPago"]?.DeepClone() ?? string.Empty;
                registros[indice] = novo;
            }
            else
            {
                registros.Add(novo);
            }

            Gravar(registros);
        }
    }

    public HashSet<string> SessoesPagas()
    {
        lock (_gate)
        {
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var registro in Registros(LerNo()))
            {
                if (!EstaAprovado(registro["statusPagamento"]?.ToString()))
                {
                    continue;
                }

                var sessao = registro["sessaoEstabelecimento"]?["guid"]?.ToString()
                    ?? registro["sessaoMesaCliente"]?["sessaoEstabelecimento"]?["guid"]?.ToString();
                var login = registro["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString();
                if (Guid.TryParse(sessao, out var guidSessao) && !string.IsNullOrWhiteSpace(login))
                {
                    guids.Add(ContaCliente.ChavePaga(guidSessao, login));
                }
            }

            return guids;
        }
    }

    public List<PagamentoMesa> ListarPagamentos()
    {
        lock (_gate)
        {
            return Registros(LerNo()).Select(Pagamento).Where(item => item.MesaId > 0).ToList();
        }
    }

    public List<ValidacaoPagamentoLinha> LerLinhas()
    {
        lock (_gate)
        {
            return Registros(LerNo()).Select(Linha).Where(linha => linha.TokenPago.Length > 0 || linha.Cliente.Length > 0).ToList();
        }
    }

    public string? AtualizarStatus(string tokenPago, string statusPagamento)
    {
        var status = StatusPermitidos.FirstOrDefault(item =>
            string.Equals(item, statusPagamento?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(tokenPago) || status is null)
        {
            return null;
        }

        lock (_gate)
        {
            var raiz = LerNo();
            var registros = Registros(raiz).ToList();
            var alvo = registros.FirstOrDefault(registro =>
                string.Equals(Token(registro), tokenPago, StringComparison.Ordinal)
                || string.Equals(MontarToken(registro), tokenPago, StringComparison.Ordinal)
                || string.Equals(registro["tokenPago"]?.ToString(), tokenPago, StringComparison.Ordinal));
            if (alvo is null)
            {
                var peloGuid = registros.Where(registro =>
                    string.Equals(registro["sessaoMesaCliente"]?["guid"]?.ToString(), tokenPago, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (peloGuid.Count == 1)
                {
                    alvo = peloGuid[0];
                }
            }

            if (alvo is null && registros.Count == 1)
            {
                alvo = registros[0];
            }
            if (alvo is null || raiz is null)
            {
                return null;
            }

            var token = MontarToken(alvo);
            alvo["statusPagamento"] = status;
            if (!string.IsNullOrWhiteSpace(token))
            {
                alvo["tokenPago"] = token;
            }

            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, raiz.ToJsonString(JsonOptions) + Environment.NewLine);
            return string.IsNullOrWhiteSpace(token) ? Token(alvo) : token;
        }
    }

    private static string MontarToken(JsonObject registro)
    {
        var sessao = registro["sessaoEstabelecimento"]?["guid"]?.ToString()
            ?? registro["sessaoMesaCliente"]?["sessaoEstabelecimento"]?["guid"]?.ToString()
            ?? string.Empty;
        var mesaCliente = registro["sessaoMesaCliente"]?["guid"]?.ToString() ?? string.Empty;
        var login = registro["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString() ?? string.Empty;
        var qrCode = registro["sessaoMesaCliente"]?["mesa"]?["qrCode"]?.ToString() ?? string.Empty;
        var abertura = registro["sessaoMesaCliente"]?["sessaoEstabelecimento"]?["dataHoraAbertura"]?.ToString()
            ?? registro["sessaoEstabelecimento"]?["dataHoraAbertura"]?.ToString()
            ?? string.Empty;
        if (sessao.Length == 0 || mesaCliente.Length == 0 || login.Length == 0 || qrCode.Length == 0 || abertura.Length == 0)
        {
            return string.Empty;
        }

        return sessao + "|" + mesaCliente + "|" + login + "|" + qrCode + "|" + abertura;
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

    private static IEnumerable<JsonObject> Registros(JsonNode? raiz)
    {
        if (raiz is JsonArray lista)
        {
            return lista.OfType<JsonObject>();
        }

        if (raiz is JsonObject objeto && objeto["validacaoPagamentos"] is JsonArray agrupados)
        {
            return agrupados.OfType<JsonObject>();
        }

        if (raiz is JsonObject unico)
        {
            return new[] { unico };
        }

        return Array.Empty<JsonObject>();
    }

    private static PagamentoMesa Pagamento(JsonObject registro)
    {
        var mesa = registro["sessaoMesaCliente"]?["mesa"] as JsonObject;
        return new PagamentoMesa
        {
            GuidSessao = registro["sessaoMesaCliente"]?["guid"]?.ToString() ?? string.Empty,
            MesaId = Inteiro(mesa?["id"]),
            IdEstabelecimento = Inteiro(mesa?["idEstabelecimento"]),
            Login = registro["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString() ?? string.Empty,
            Aprovado = EstaAprovado(registro["statusPagamento"]?.ToString())
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

    private static ValidacaoPagamentoLinha Linha(JsonObject registro)
    {
        var mesa = registro["sessaoMesaCliente"]?["mesa"]?["nome"]?.ToString() ?? string.Empty;
        var cliente = registro["sessaoMesaCliente"]?["cliente"]?["nome"]?.ToString();
        if (string.IsNullOrWhiteSpace(cliente))
        {
            cliente = registro["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString() ?? string.Empty;
        }

        var sessaoAtual = registro["sessaoEstabelecimento"]?["guid"]?.ToString();
        if (string.IsNullOrWhiteSpace(sessaoAtual))
        {
            sessaoAtual = registro["sessaoMesaCliente"]?["sessaoEstabelecimento"]?["guid"]?.ToString() ?? string.Empty;
        }

        var itens = new List<string>();
        if (registro["mesaConsumo"] is JsonArray consumo)
        {
            var loginConta = registro["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString() ?? string.Empty;
            foreach (var nodo in consumo.OfType<JsonObject>())
            {
                var loginItem = nodo["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString() ?? string.Empty;
                if (loginConta.Length > 0 && !string.Equals(loginItem, loginConta, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var status = nodo["item"]?["statusEntrega"]?.ToString();
                if (string.IsNullOrWhiteSpace(status))
                {
                    status = nodo["statusEntrega"]?.ToString() ?? string.Empty;
                }

                if (!string.Equals(status, "entregue", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var sessaoItem = nodo["sessaoEstabelecimento"]?["guid"]?.ToString();
                if (string.IsNullOrWhiteSpace(sessaoItem))
                {
                    sessaoItem = nodo["sessaoMesaCliente"]?["sessaoEstabelecimento"]?["guid"]?.ToString() ?? string.Empty;
                }

                if (!string.Equals(sessaoItem, sessaoAtual, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var nome = nodo["item"]?["nome"]?.ToString() ?? string.Empty;
                var preco = nodo["item"]?["preco"]?.ToString() ?? string.Empty;
                itens.Add(string.IsNullOrWhiteSpace(preco) ? nome : nome + " · " + preco);
            }
        }

        return new ValidacaoPagamentoLinha
        {
            TokenPago = Token(registro),
            Mesa = string.IsNullOrWhiteSpace(mesa) ? "Mesa" : mesa,
            Cliente = string.IsNullOrWhiteSpace(cliente) ? "Cliente" : cliente,
            StatusPagamento = registro["statusPagamento"]?.ToString() ?? string.Empty,
            FormaDePagamento = registro["formaDePagamento"]?.ToString() ?? string.Empty,
            ItensEntregues = itens
        };
    }

    private static string Token(JsonObject registro)
    {
        var montado = MontarToken(registro);
        if (!string.IsNullOrWhiteSpace(montado))
        {
            return montado;
        }

        var token = registro["tokenPago"]?.ToString();
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        return registro["sessaoMesaCliente"]?["guid"]?.ToString() ?? string.Empty;
    }

    private static string Chave(JsonObject registro)
    {
        var sessao = registro["sessaoEstabelecimento"]?["guid"]?.ToString()
            ?? registro["sessaoMesaCliente"]?["sessaoEstabelecimento"]?["guid"]?.ToString()
            ?? string.Empty;
        var login = registro["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString() ?? string.Empty;
        return Guid.TryParse(sessao, out var guid)
            ? ContaCliente.ChavePaga(guid, login)
            : sessao + "|" + login.Trim().ToLowerInvariant();
    }

    private static void ManterItensDoCliente(JsonObject registro)
    {
        var login = registro["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString() ?? string.Empty;
        if (login.Length == 0 || registro["mesaConsumo"] is not JsonArray lista)
        {
            return;
        }

        var proprios = new JsonArray();
        foreach (var nodo in lista.OfType<JsonObject>())
        {
            var loginItem = nodo["sessaoMesaCliente"]?["cliente"]?["login"]?.ToString() ?? string.Empty;
            if (string.Equals(loginItem, login, StringComparison.OrdinalIgnoreCase))
            {
                proprios.Add(nodo.DeepClone());
            }
        }

        registro["mesaConsumo"] = proprios;
    }

    private void Gravar(List<JsonObject> registros)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        JsonNode saida = registros.Count == 1
            ? registros[0].DeepClone()
            : new JsonObject
            {
                ["validacaoPagamentos"] = new JsonArray(registros.Select(item => (JsonNode)item.DeepClone()).ToArray())
            };
        File.WriteAllText(_path, saida.ToJsonString(JsonOptions) + Environment.NewLine);
    }
}
