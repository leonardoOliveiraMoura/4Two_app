using System.Text.Encodings.Web;
using System.Text.Json;
using C4Two.Filters;
using C4Two.Models;
using C4Two.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("sessaoestabelecimento")]
[RequireLoginSession(LoginSession.Cliente, "LoginClientes")]
public class SessaoEstabelecimentoController : Controller
{
    private readonly SessaoEstabelecimentoStore _sessoes;
    private readonly SessaoMesaClienteStore _mesasCliente;
    private readonly CadGerenciamentoStore _cadastros;
    private readonly CadClienteStore _clientes;
    private readonly MesaConsumoStore _consumo;
    private readonly MesaStatusService _mesaStatus;
    private readonly ValidacaoPagamentoStore _validacoes;
    private readonly CompartilhamentoConsumoStore _compartilhamentos;

    private static readonly JsonSerializerOptions CestaScriptOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Default
    };

    public SessaoEstabelecimentoController(
        SessaoEstabelecimentoStore sessoes,
        SessaoMesaClienteStore mesasCliente,
        CadGerenciamentoStore cadastros,
        CadClienteStore clientes,
        MesaConsumoStore consumo,
        MesaStatusService mesaStatus,
        ValidacaoPagamentoStore validacoes,
        CompartilhamentoConsumoStore compartilhamentos)
    {
        _sessoes = sessoes;
        _mesasCliente = mesasCliente;
        _cadastros = cadastros;
        _clientes = clientes;
        _consumo = consumo;
        _mesaStatus = mesaStatus;
        _validacoes = validacoes;
        _compartilhamentos = compartilhamentos;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        _consumo.GarantirStatusEntrega();
        var sessao = _sessoes.Read();
        SincronizarStatus(sessao);
        CarregarMesas(sessao);
        GarantirOcupante(sessao, ViewData["SessaoMesa"] as SessaoMesaCliente);
        ViewData["ItensConsumo"] = sessao is null || sessao.IdEstabelecimento <= 0
            ? new List<ItemConsumoItem>()
            : _cadastros.Read().CadastroItensConsumo
                .Where(item => item.IdEstabelecimento == sessao.IdEstabelecimento)
                .ToList();
        var integrantes = MontarIntegrantes(_sessoes.Read(), ViewData["SessaoMesa"] as SessaoMesaCliente);
        ViewData["Integrantes"] = integrantes;
        var reservaPagina = ViewData["SessaoMesa"] as SessaoMesaCliente;
        var clienteVisivel = ClienteAtual(LoginAtual(reservaPagina), reservaPagina?.Cliente);
        ViewData["NomeClienteVisivel"] = string.IsNullOrWhiteSpace(clienteVisivel.Nome) ? clienteVisivel.Login : clienteVisivel.Nome;
        var cesta = integrantes.FirstOrDefault(pessoa => pessoa.Atual)?.Itens ?? new List<MesaConsumoItem>();
        ViewData["Cesta"] = cesta;
        ViewData["CestaJson"] = JsonSerializer.Serialize(cesta.Select(ItemCesta), CestaScriptOptions);
        ViewData["EntreguesConhecidos"] = JsonSerializer.Serialize(
            integrantes.SelectMany(pessoa => pessoa.Itens).Where(Entregue).Select(item => item.Id.ToString("D")).Distinct().ToList(),
            CestaScriptOptions);
        return View(sessao);
    }

    [HttpGet("notificacao-consumo")]
    public IActionResult NotificacaoConsumo()
    {
        var reserva = ReservaDaSessao(_sessoes.Read());
        if (reserva is null)
        {
            return Json(new { entregues = Array.Empty<string>(), itens = Array.Empty<object>(), compartilhamentos = Array.Empty<object>(), assentos = Array.Empty<object>() });
        }

        var sessao = _sessoes.Read();
        GarantirOcupante(sessao, reserva);
        var visiveis = ItensVisiveis(sessao, reserva).OrderBy(item => item.Item.DataHora).ToList();
        var entregues = visiveis
            .Where(Entregue)
            .Select(item => item.Id.ToString("D"))
            .Distinct()
            .ToArray();
        var login = HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        var compartilhamentos = _compartilhamentos
            .Pendentes(sessao?.Guid ?? Guid.Empty, reserva.Mesa.Id, reserva.Mesa.IdEstabelecimento, login)
            .Select(oferta =>
            {
                var consumo = visiveis.FirstOrDefault(item => item.Id == oferta.ConsumoId)
                    ?? _consumo.Read().FirstOrDefault(item => item.Id == oferta.ConsumoId);
                var nome = string.IsNullOrWhiteSpace(oferta.NomeItem) ? consumo?.Item?.Nome ?? string.Empty : oferta.NomeItem;
                var descricao = string.IsNullOrWhiteSpace(oferta.Descricao) ? consumo?.Item?.Descricao ?? string.Empty : oferta.Descricao;
                var categoria = string.IsNullOrWhiteSpace(oferta.Categoria) ? consumo?.Item?.Categoria ?? nome : oferta.Categoria;
                var preco = string.IsNullOrWhiteSpace(oferta.Preco) ? consumo?.Item?.Preco ?? "0,00" : oferta.Preco;
                var mensagem = (string.IsNullOrWhiteSpace(oferta.NomeCliente) ? oferta.LoginAutor : oferta.NomeCliente) + CompartilhamentoConsumoStore.Mensagem;
                if (!string.IsNullOrWhiteSpace(nome))
                {
                    mensagem += " Nome: " + nome.Trim() + ".";
                }

                if (!string.IsNullOrWhiteSpace(descricao))
                {
                    mensagem += " Descrição: " + descricao.Trim() + ".";
                }

                return new
                {
                    oferta.Id,
                    NomeItem = nome,
                    Descricao = descricao,
                    Categoria = categoria,
                    Preco = ValorEmReais(preco),
                    Mensagem = mensagem
                };
            });
        return Json(new { entregues, itens = visiveis.Select(ItemCesta), compartilhamentos, assentos = MontarAssentos(sessao, reserva) });
    }

    [HttpPost("reservar-mesa/{id:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult ReservarMesa(int id)
    {
        var sessao = _sessoes.Read();
        if (sessao == null || sessao.IdEstabelecimento <= 0)
        {
            return RedirectToAction(nameof(Index));
        }

        var reservaAtual = _mesasCliente.Read();
        if (reservaAtual is not null
            && reservaAtual.Guid != Guid.Empty
            && reservaAtual.SessaoEstabelecimento.Guid == sessao.Guid)
        {
            return RedirectToAction(nameof(Index));
        }

        var mesa = _cadastros.Read().CadastroMesa.FirstOrDefault(item =>
            item.Id == id
            && item.IdEstabelecimento == sessao.IdEstabelecimento
            && string.Equals(item.Status, "disponivel", StringComparison.OrdinalIgnoreCase));
        if (mesa == null)
        {
            return RedirectToAction(nameof(Index));
        }

        mesa.Status = StatusPorCapacidade(1, mesa.Capacidade);
        _cadastros.AtualizarStatusMesa(mesa.Id, mesa.Status);

        var login = HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        var cliente = _clientes.Find(login);
        _mesasCliente.Save(new SessaoMesaCliente
        {
            Guid = Guid.NewGuid(),
            SessaoEstabelecimento = sessao,
            Mesa = mesa,
            Cliente = new ClienteSessao
            {
                Login = cliente.Login,
                Id = cliente.Id,
                Cpf = cliente.Cpf,
                Nome = cliente.Nome,
                Email = cliente.Email,
                Telefone = cliente.Telefone,
                Endereco = cliente.Endereco,
                Cidade = cliente.Cidade,
                Estado = cliente.Estado,
                Cep = cliente.Cep,
                Pais = cliente.Pais
            },
            CapacidadeAtualStatus = new CapacidadeAtualStatus
            {
                Id = mesa.Id,
                CapacidadeAtual = "1"
            }
        });

        GarantirOcupante(sessao, _mesasCliente.Read());
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("abrir-sessao-mesa/{id:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult AbrirSessaoMesa(int id, int ocupantes)
    {
        var sessao = _sessoes.Read();
        if (sessao == null || sessao.IdEstabelecimento <= 0 || ocupantes < 1)
        {
            return RedirectToAction(nameof(Index));
        }

        var mesa = _cadastros.Read().CadastroMesa.FirstOrDefault(item =>
            item.Id == id
            && item.IdEstabelecimento == sessao.IdEstabelecimento
            && string.Equals(item.Status, "reservada", StringComparison.OrdinalIgnoreCase));
        if (mesa == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var registro = _mesasCliente.Read();
        var atuais = registro is not null && registro.Mesa.Id == mesa.Id
            ? Ocupacao(registro.CapacidadeAtualStatus?.CapacidadeAtual)
            : 1;
        var lugares = int.TryParse(mesa.Capacidade, out var total) ? total : 0;
        var ocupacao = atuais + ocupantes;
        if (lugares > 0 && ocupacao > lugares)
        {
            TempData["AvisoMesa"] = "A mesa " + mesa.Nome + " não comporta esse número de ocupantes.";
            return RedirectToAction(nameof(Index));
        }

        mesa.Status = StatusPorCapacidade(ocupacao, mesa.Capacidade);
        _cadastros.AtualizarStatusMesa(mesa.Id, mesa.Status);

        if (registro is not null
            && registro.Guid != Guid.Empty
            && registro.Mesa.Id == mesa.Id
            && registro.SessaoEstabelecimento.Guid == sessao.Guid)
        {
            GarantirOcupante(sessao, registro);
        }

        var login = HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        var cliente = _clientes.Find(login);
        _mesasCliente.Save(new SessaoMesaCliente
        {
            Guid = Guid.NewGuid(),
            SessaoEstabelecimento = sessao,
            Mesa = mesa,
            Cliente = new ClienteSessao
            {
                Login = cliente.Login,
                Id = cliente.Id,
                Cpf = cliente.Cpf,
                Nome = cliente.Nome,
                Email = cliente.Email,
                Telefone = cliente.Telefone,
                Endereco = cliente.Endereco,
                Cidade = cliente.Cidade,
                Estado = cliente.Estado,
                Cep = cliente.Cep,
                Pais = cliente.Pais
            },
            CapacidadeAtualStatus = new CapacidadeAtualStatus
            {
                Id = mesa.Id,
                CapacidadeAtual = ocupacao.ToString()
            }
        });

        GarantirOcupante(sessao, _mesasCliente.Read());
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("incluir-consumo/{id:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult IncluirConsumo(int id, string? compartilhado)
    {
        var sessao = _sessoes.Read();
        var reserva = ReservaDaSessao(sessao);
        if (sessao is null || reserva is null)
        {
            return Json(new { ok = false, mensagem = "Escolha uma mesa para iniciar seu consumo." });
        }

        var item = _cadastros.Read().CadastroItensConsumo.FirstOrDefault(atual =>
            atual.Id == id && atual.IdEstabelecimento == sessao.IdEstabelecimento);
        if (item is null)
        {
            return Json(new { ok = false, mensagem = "Item de consumo não encontrado." });
        }

        var compartilhadoSim = string.Equals(compartilhado, "sim", StringComparison.OrdinalIgnoreCase);
        var login = LoginAtual(reserva);
        var reservaCliente = ReservaDoCliente(reserva, login);
        GarantirOcupante(sessao, reservaCliente);
        var consumo = new MesaConsumoItem
        {
            Id = Guid.NewGuid(),
            SessaoEstabelecimento = sessao,
            SessaoMesaCliente = reservaCliente,
            Item = new ItemConsumoMesa
            {
                Id = item.Id,
                IdEstabelecimento = item.IdEstabelecimento,
                Nome = item.Nome,
                Categoria = item.Categoria,
                Descricao = item.Descricao,
                Preco = item.Preco,
                PreparoCozinha = item.PreparoCozinha,
                DataHora = DateTime.Now,
                StatusEntrega = "não entregue",
                StatusPedidoCozinha = MesaConsumoStore.StatusPedidoCozinhaInicial,
                Compartilhado = compartilhadoSim ? "sim" : "não"
            }
        };
        var cesta = _consumo.Read();
        cesta.Add(consumo);
        _consumo.Save(cesta);
        if (compartilhadoSim)
        {
            _compartilhamentos.Registrar(new CompartilhamentoConsumo
            {
                Id = Guid.NewGuid(),
                ConsumoId = consumo.Id,
                SessaoEstabelecimento = sessao.Guid,
                MesaId = reserva.Mesa.Id,
                IdEstabelecimento = reserva.Mesa.IdEstabelecimento,
                LoginAutor = reservaCliente.Cliente.Login,
                NomeCliente = reservaCliente.Cliente.Nome,
                NomeItem = item.Nome,
                Descricao = item.Descricao,
                Categoria = item.Categoria,
                Preco = item.Preco
            });
        }

        return Json(new { ok = true, itens = CestaDaSessao(sessao, reserva).Select(ItemCesta) });
    }

    [HttpPost("remover-consumo/{id:guid}")]
    [ValidateAntiForgeryToken]
    public IActionResult RemoverConsumo(Guid id)
    {
        var sessao = _sessoes.Read();
        var reserva = ReservaDaSessao(sessao);
        if (sessao is null || reserva is null)
        {
            return Json(new { ok = false, mensagem = "Escolha uma mesa para iniciar seu consumo." });
        }

        var cesta = _consumo.Read();
        var alvo = cesta.FirstOrDefault(item =>
            item.Id == id
            && DaSessaoAtiva(item, sessao, reserva)
            && MesmoCliente(item, LoginAtual(reserva)));
        if (alvo is not null)
        {
            cesta.Remove(alvo);
            _consumo.Save(cesta);
            _compartilhamentos.RemoverPorConsumo(alvo.Id);
        }

        return Json(new { ok = true, itens = CestaDaSessao(sessao, reserva).Select(ItemCesta) });
    }

    private void CarregarMesas(SessaoEstabelecimento? sessao)
    {
        var mesas = new List<MesaItem>();
        if (sessao is not null && sessao.IdEstabelecimento > 0)
        {
            mesas = _cadastros.Read().CadastroMesa
                .Where(item => item.IdEstabelecimento == sessao.IdEstabelecimento
                    && (string.Equals(item.Status, "disponivel", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.Status, "reservada", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        ViewData["MesasDisponiveis"] = mesas;
        ViewData["AvisoMesa"] = TempData["AvisoMesa"] as string;

        var ocupantesPorMesa = new Dictionary<int, int>();
        var registro = _mesasCliente.Read();
        if (registro is not null && registro.Mesa.Id > 0)
        {
            ocupantesPorMesa[registro.Mesa.Id] = Ocupacao(registro.CapacidadeAtualStatus?.CapacidadeAtual);
        }

        ViewData["OcupantesPorMesa"] = ocupantesPorMesa;

        var reserva = registro;
        var reservaDestaSessao = reserva is not null
            && sessao is not null
            && reserva.Guid != Guid.Empty
            && reserva.SessaoEstabelecimento.Guid == sessao.Guid;
        ViewData["SessaoMesa"] = reservaDestaSessao ? reserva : null;
    }

    private void SincronizarStatus(SessaoEstabelecimento? sessao)
    {
        var reserva = _mesasCliente.Read();
        if (reserva is null
            || sessao is null
            || reserva.Guid == Guid.Empty
            || reserva.SessaoEstabelecimento.Guid != sessao.Guid)
        {
            return;
        }

        var mesaAtual = _cadastros.Read().CadastroMesa.FirstOrDefault(item => item.Id == reserva.Mesa.Id);
        if (mesaAtual is not null
            && string.Equals(mesaAtual.Status, MesaStatusService.Disponivel, StringComparison.OrdinalIgnoreCase)
            && _mesaStatus.TodosOcupantesPagos(reserva.Mesa.Id, reserva.Mesa.IdEstabelecimento))
        {
            return;
        }

        var status = StatusPorCapacidade(
            Ocupacao(reserva.CapacidadeAtualStatus?.CapacidadeAtual),
            reserva.Mesa.Capacidade);
        if (!string.Equals(reserva.Mesa.Status, status, StringComparison.OrdinalIgnoreCase))
        {
            reserva.Mesa.Status = status;
            _mesasCliente.Save(reserva);
        }

        var mesa = _cadastros.Read().CadastroMesa.FirstOrDefault(item => item.Id == reserva.Mesa.Id);
        if (mesa is not null && !string.Equals(mesa.Status, status, StringComparison.OrdinalIgnoreCase))
        {
            _cadastros.AtualizarStatusMesa(mesa.Id, status);
        }
    }

    [HttpPost("aceitar-compartilhamento/{id:guid}")]
    [ValidateAntiForgeryToken]
    public IActionResult AceitarCompartilhamento(Guid id)
    {
        var login = HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        return Json(new { ok = _compartilhamentos.Aceitar(id, login) });
    }

    [HttpPost("recusar-compartilhamento/{id:guid}")]
    [ValidateAntiForgeryToken]
    public IActionResult RecusarCompartilhamento(Guid id)
    {
        var login = HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        return Json(new { ok = _compartilhamentos.Recusar(id, login) });
    }

    private void GarantirOcupante(SessaoEstabelecimento? sessao, SessaoMesaCliente? reserva)
    {
        if (sessao is null || reserva is null || sessao.Guid == Guid.Empty || reserva.Mesa.Id <= 0)
        {
            return;
        }

        var loginReserva = reserva.Cliente?.Login ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(loginReserva))
        {
            _compartilhamentos.RegistrarOcupante(
                sessao.Guid,
                reserva.Mesa.Id,
                reserva.Mesa.IdEstabelecimento,
                loginReserva,
                string.IsNullOrWhiteSpace(reserva.Cliente?.Nome) ? loginReserva : reserva.Cliente.Nome);
        }

        var login = HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(login) || string.Equals(login, loginReserva, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var cliente = _clientes.Find(login);
        _compartilhamentos.RegistrarOcupante(
            sessao.Guid,
            reserva.Mesa.Id,
            reserva.Mesa.IdEstabelecimento,
            login,
            string.IsNullOrWhiteSpace(cliente?.Nome) ? login : cliente.Nome);
    }

    private string LoginAtual(SessaoMesaCliente? reserva)
    {
        var login = HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(login))
        {
            return login;
        }

        return reserva?.Cliente?.Login ?? string.Empty;
    }

    private ClienteSessao ClienteAtual(string login, ClienteSessao? reservaCliente)
    {
        var cliente = _clientes.Find(login);
        if (string.IsNullOrWhiteSpace(cliente.Login))
        {
            return reservaCliente ?? new ClienteSessao { Login = login, Nome = login };
        }

        return new ClienteSessao
        {
            Login = cliente.Login,
            Id = cliente.Id,
            Cpf = cliente.Cpf,
            Nome = string.IsNullOrWhiteSpace(cliente.Nome) ? login : cliente.Nome,
            Email = cliente.Email,
            Telefone = cliente.Telefone,
            Endereco = cliente.Endereco,
            Cidade = cliente.Cidade,
            Estado = cliente.Estado,
            Cep = cliente.Cep,
            Pais = cliente.Pais
        };
    }

    private SessaoMesaCliente ReservaDoCliente(SessaoMesaCliente reserva, string login)
    {
        return new SessaoMesaCliente
        {
            Guid = reserva.Guid,
            SessaoEstabelecimento = reserva.SessaoEstabelecimento,
            Mesa = reserva.Mesa,
            CapacidadeAtualStatus = reserva.CapacidadeAtualStatus,
            Cliente = ClienteAtual(login, reserva.Cliente)
        };
    }

    private SessaoMesaCliente? ReservaDaSessao(SessaoEstabelecimento? sessao)
    {
        var reserva = _mesasCliente.Read();
        if (reserva is null
            || sessao is null
            || reserva.Guid == Guid.Empty
            || reserva.SessaoEstabelecimento.Guid != sessao.Guid)
        {
            return null;
        }

        return reserva;
    }

    private List<IntegranteMesa> MontarIntegrantes(SessaoEstabelecimento? sessao, SessaoMesaCliente? reserva)
    {
        if (reserva is null || sessao is null)
        {
            return new List<IntegranteMesa>();
        }

        var itens = ItensVisiveis(sessao, reserva)
            .OrderBy(item => item.Item.DataHora)
            .ToList();
        var loginAtual = LoginAtual(reserva);
        var grupos = itens
            .GroupBy(item => item.SessaoMesaCliente.Cliente?.Login ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var ocupantes = _compartilhamentos.DaMesa(sessao.Guid, reserva.Mesa.Id, reserva.Mesa.IdEstabelecimento);
        if (ocupantes.Count == 0 && !string.IsNullOrWhiteSpace(loginAtual))
        {
            var cliente = ClienteAtual(loginAtual, reserva.Cliente);
            ocupantes.Add(new OcupanteSentado { Login = cliente.Login, Nome = cliente.Nome });
        }

        var integrantes = new List<IntegranteMesa>();
        foreach (var ocupante in ocupantes)
        {
            var grupo = grupos.FirstOrDefault(item => string.Equals(item.Key, ocupante.Login, StringComparison.OrdinalIgnoreCase));
            integrantes.Add(new IntegranteMesa
            {
                Login = ocupante.Login,
                Nome = string.IsNullOrWhiteSpace(ocupante.Nome) ? ocupante.Login : ocupante.Nome,
                Atual = string.Equals(ocupante.Login, loginAtual, StringComparison.OrdinalIgnoreCase),
                Itens = grupo?.ToList() ?? new List<MesaConsumoItem>()
            });
        }

        foreach (var grupo in grupos
            .Where(grupo => integrantes.All(pessoa => !string.Equals(pessoa.Login, grupo.Key, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(grupo => grupo.Min(item => item.Item.DataHora)))
        {
            integrantes.Add(new IntegranteMesa
            {
                Login = grupo.Key,
                Nome = grupo.Select(item => item.SessaoMesaCliente.Cliente?.Nome).FirstOrDefault(nome => !string.IsNullOrWhiteSpace(nome)) ?? grupo.Key,
                Itens = grupo.ToList()
            });
        }

        return integrantes;
    }

    private object[] MontarAssentos(SessaoEstabelecimento? sessao, SessaoMesaCliente reserva)
    {
        int.TryParse(reserva.Mesa.Capacidade, out var lugares);
        if (lugares < 2)
        {
            lugares = 4;
        }

        var pessoas = MontarIntegrantes(sessao, reserva);
        if (pessoas.Count > lugares)
        {
            pessoas = pessoas.Take(lugares).ToList();
        }

        var assentos = new List<object>();
        for (var indice = 0; indice < lugares; indice++)
        {
            var pessoa = indice < pessoas.Count ? pessoas[indice] : null;
            var ocupado = pessoa is not null && !string.IsNullOrWhiteSpace(pessoa.Login);
            assentos.Add(new
            {
                nome = ocupado ? pessoa!.Nome : string.Empty,
                atual = pessoa?.Atual == true,
                ocupado,
                itens = (pessoa?.Itens ?? new List<MesaConsumoItem>()).Select(ItemCesta).ToArray()
            });
        }

        return assentos.ToArray();
    }

    private List<MesaConsumoItem> CestaDaSessao(SessaoEstabelecimento? sessao, SessaoMesaCliente? reserva)
    {
        if (reserva is null || sessao is null)
        {
            return new List<MesaConsumoItem>();
        }

        return ItensVisiveis(sessao, reserva)
            .Where(item => MesmoCliente(item, LoginAtual(reserva)))
            .OrderBy(item => item.Item.DataHora)
            .ToList();
    }

    private IEnumerable<MesaConsumoItem> ItensVisiveis(SessaoEstabelecimento? sessao, SessaoMesaCliente reserva)
    {
        var pagas = _validacoes.SessoesPagas();
        return _consumo.Read().Where(item => DaSessaoAtiva(item, sessao, reserva, pagas));
    }

    private static bool DaSessaoAtiva(MesaConsumoItem item, SessaoEstabelecimento? sessao, SessaoMesaCliente reserva, HashSet<string>? pagas = null)
    {
        if (sessao is null || !MesmaMesa(item, reserva) || item.SessaoEstabelecimento.Guid != sessao.Guid)
        {
            return false;
        }

        if (pagas is null)
        {
            return true;
        }

        var chave = ContaCliente.ChavePaga(item.SessaoEstabelecimento.Guid, item.SessaoMesaCliente?.Cliente?.Login);
        return !pagas.Contains(chave);
    }

    private static bool MesmaMesa(MesaConsumoItem item, SessaoMesaCliente reserva)
    {
        return item.SessaoMesaCliente?.Mesa is not null
            && item.SessaoMesaCliente.Mesa.Id == reserva.Mesa.Id
            && item.SessaoMesaCliente.Mesa.IdEstabelecimento == reserva.Mesa.IdEstabelecimento;
    }

    private static bool Entregue(MesaConsumoItem item)
    {
        return item.SessaoMesaCliente is not null
            && item.Item is not null
            && item.Item.Id > 0
            && string.Equals(item.Item.StatusEntrega, "entregue", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MesmoCliente(MesaConsumoItem item, string? login)
    {
        return string.Equals(item.SessaoMesaCliente?.Cliente?.Login, login, StringComparison.OrdinalIgnoreCase);
    }

    private static object ItemCesta(MesaConsumoItem item)
    {
        var produto = item.Item ?? new ItemConsumoMesa();
        return new
        {
            item.Id,
            Nome = produto.Nome ?? string.Empty,
            Categoria = produto.Categoria ?? string.Empty,
            Preco = produto.Preco ?? string.Empty,
            Descricao = produto.Descricao ?? string.Empty,
            DataHora = produto.DataHora.ToString("dd/MM/yyyy HH:mm"),
            StatusEntrega = string.IsNullOrWhiteSpace(produto.StatusEntrega) ? "não entregue" : produto.StatusEntrega,
            PreparoCozinha = produto.PreparoCozinha ?? string.Empty,
            StatusPedidoCozinha = string.IsNullOrWhiteSpace(produto.StatusPedidoCozinha)
                ? MesaConsumoStore.StatusPedidoCozinhaInicial
                : produto.StatusPedidoCozinha
        };
    }

    private static string ValorEmReais(string? preco)
    {
        var texto = (preco ?? string.Empty).Trim();
        if (texto.Length == 0)
        {
            texto = "0,00";
        }

        if (texto.StartsWith("R$", StringComparison.OrdinalIgnoreCase))
        {
            return texto;
        }

        return "R$ " + texto;
    }

    private static int Ocupacao(string? capacidadeAtual)
    {
        return int.TryParse(capacidadeAtual, out var ocupados) && ocupados > 0 ? ocupados : 1;
    }

    private static string StatusPorCapacidade(int ocupados, string capacidade)
    {
        var lugares = int.TryParse(capacidade, out var total) ? total : 0;
        return lugares > 0 && ocupados >= lugares ? "indisponivel" : "reservada";
    }
}
