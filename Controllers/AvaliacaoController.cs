using C4Two.Filters;
using C4Two.Models;
using C4Two.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("avaliacao")]
[RequireLoginSession(LoginSession.Cliente, "LoginClientes")]
public class AvaliacaoController : Controller
{
    private readonly SessaoEstabelecimentoStore _sessoes;
    private readonly SessaoMesaClienteStore _mesasCliente;
    private readonly CodigoLiberacaoStore _codigos;
    private readonly AvaliacaoClienteStore _avaliacoes;
    private readonly CadClienteStore _clientes;
    private readonly CompartilhamentoConsumoStore _compartilhamentos;
    private readonly MesaConsumoStore _consumo;
    private readonly ValidacaoPagamentoStore _validacoes;

    public AvaliacaoController(
        SessaoEstabelecimentoStore sessoes,
        SessaoMesaClienteStore mesasCliente,
        CodigoLiberacaoStore codigos,
        AvaliacaoClienteStore avaliacoes,
        CadClienteStore clientes,
        CompartilhamentoConsumoStore compartilhamentos,
        MesaConsumoStore consumo,
        ValidacaoPagamentoStore validacoes)
    {
        _sessoes = sessoes;
        _mesasCliente = mesasCliente;
        _codigos = codigos;
        _avaliacoes = avaliacoes;
        _clientes = clientes;
        _compartilhamentos = compartilhamentos;
        _consumo = consumo;
        _validacoes = validacoes;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        if (!SessaoLiberada(out var estabelecimento, out var mesa))
        {
            return RedirectToAction("Index", "Cliente");
        }

        ViewData["Nota"] = _avaliacoes.NotaDaSessao(estabelecimento, mesa, LoginAtual());
        return View();
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public IActionResult Index(string nota)
    {
        var login = LoginAtual();
        if (!SessaoLiberada(out var estabelecimento, out var mesa)
            || !_codigos.RegistrarAvaliacao(estabelecimento, mesa, nota ?? string.Empty, login))
        {
            return Json(new { ok = false });
        }

        var sessao = _sessoes.Read();
        var reserva = _mesasCliente.Read();
        SessaoMesaCliente? conta = null;
        if (sessao is not null && reserva is not null && reserva.SessaoEstabelecimento.Guid == sessao.Guid)
        {
            conta = ContaCliente.ComCliente(reserva, ContaCliente.Pessoa(_clientes, login, reserva.Cliente));
        }

        if (sessao is null || _avaliacoes.Registrar(sessao, conta, nota ?? string.Empty) <= 0)
        {
            return Json(new { ok = false });
        }

        return Json(new { ok = true });
    }

    [HttpPost("encerrar")]
    [ValidateAntiForgeryToken]
    public IActionResult Encerrar()
    {
        if (!SessaoLiberada(out _, out _))
        {
            return Json(new { ok = true, destino = "/cliente" });
        }

        if (MesaEncerrada())
        {
            _sessoes.Encerrar();
            _mesasCliente.Encerrar();
        }

        return Json(new { ok = true, destino = "/cliente" });
    }

    private bool SessaoLiberada(out Guid estabelecimento, out Guid mesa)
    {
        estabelecimento = Guid.Empty;
        mesa = Guid.Empty;
        var sessao = _sessoes.Read();
        if (sessao is null || sessao.Guid == Guid.Empty)
        {
            return false;
        }

        var reserva = _mesasCliente.Read();
        estabelecimento = sessao.Guid;
        mesa = reserva is not null && reserva.SessaoEstabelecimento.Guid == sessao.Guid
            ? reserva.Guid
            : Guid.Empty;
        return _codigos.ChecklistConfirmado(estabelecimento, mesa, LoginAtual());
    }

    private bool MesaEncerrada()
    {
        var sessao = _sessoes.Read();
        var reserva = _mesasCliente.Read();
        if (sessao is null || reserva is null || reserva.Mesa.Id <= 0)
        {
            return true;
        }

        var ocupantes = _compartilhamentos.DaMesa(sessao.Guid, reserva.Mesa.Id, reserva.Mesa.IdEstabelecimento);
        if (ocupantes.Count == 0)
        {
            return true;
        }

        var consumo = _consumo.Read();
        foreach (var ocupante in ocupantes)
        {
            var temConta = consumo.Any(item => ContaCliente.DaMesa(item, sessao, reserva) && ContaCliente.MesmoLogin(item, ocupante.Login))
                || !string.IsNullOrWhiteSpace(_validacoes.StatusDoCliente(sessao.Guid.ToString(), ocupante.Login, reserva.Guid.ToString()));
            if (!temConta)
            {
                continue;
            }

            var avaliou = !string.IsNullOrWhiteSpace(_avaliacoes.NotaDaSessao(sessao.Guid, reserva.Guid, ocupante.Login))
                || !string.IsNullOrWhiteSpace(_codigos.NotaDaSessao(sessao.Guid, reserva.Guid, ocupante.Login));
            if (!_codigos.ChecklistConfirmado(sessao.Guid, reserva.Guid, ocupante.Login) || !avaliou)
            {
                return false;
            }
        }

        return true;
    }

    private string LoginAtual()
    {
        return HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
    }
}
