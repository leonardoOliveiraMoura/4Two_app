using System.Globalization;
using C4Two.Filters;
using C4Two.Models;
using C4Two.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("validaPagamentos")]
[RequireLoginSession(LoginSession.Cliente, "LoginClientes")]
public class ValidaPagamentosController : Controller
{
    private readonly SessaoEstabelecimentoStore _sessoes;
    private readonly SessaoMesaClienteStore _mesasCliente;
    private readonly MesaConsumoStore _consumo;
    private readonly FormaDePagamentoStore _pagamentos;
    private readonly ValidacaoPagamentoStore _validacoes;
    private readonly CodigoLiberacaoStore _codigos;
    private readonly CadClienteStore _clientes;
    private readonly CompartilhamentoConsumoStore _compartilhamentos;

    public ValidaPagamentosController(
        SessaoEstabelecimentoStore sessoes,
        SessaoMesaClienteStore mesasCliente,
        MesaConsumoStore consumo,
        FormaDePagamentoStore pagamentos,
        ValidacaoPagamentoStore validacoes,
        CodigoLiberacaoStore codigos,
        CadClienteStore clientes,
        CompartilhamentoConsumoStore compartilhamentos)
    {
        _sessoes = sessoes;
        _mesasCliente = mesasCliente;
        _consumo = consumo;
        _pagamentos = pagamentos;
        _validacoes = validacoes;
        _codigos = codigos;
        _clientes = clientes;
        _compartilhamentos = compartilhamentos;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var sessao = _sessoes.Read();
        var reserva = _mesasCliente.Read();
        if (sessao is null
            || reserva is null
            || reserva.Guid == Guid.Empty
            || reserva.SessaoEstabelecimento.Guid != sessao.Guid
            || reserva.Mesa.Id <= 0)
        {
            return RedirectToAction("Index", "SessaoEstabelecimento");
        }

        var login = ContaCliente.Login(HttpContext, reserva);
        var pessoa = ContaCliente.Pessoa(_clientes, login, reserva.Cliente);
        var nome = string.IsNullOrWhiteSpace(pessoa.Nome) ? sessao.NomeCliente : pessoa.Nome;
        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        var total = ContaCliente.Total(ContaCliente.ItensDaConta(_compartilhamentos, _consumo.Read(), sessao, reserva, login));
        var copiaCola = PixCopiaCola.Montar(
            pessoa.Email,
            string.IsNullOrWhiteSpace(sessao.Nome) ? nome : sessao.Nome,
            pessoa.Cidade,
            total);
        var registro = _pagamentos.Read(sessao.Guid, login);
        var aprovado = ValidacaoPagamentoStore.EstaAprovado(_validacoes.StatusDoCliente(sessao.Guid.ToString(), login, reserva.Guid.ToString()));

        return View(new FormaDePagamentoPagina
        {
            Cliente = string.IsNullOrWhiteSpace(nome) ? "Nome do ciente" : nome,
            Valor = total.ToString("N2", cultura),
            CopiaCola = copiaCola,
            QrCode = PixCopiaCola.PngBase64(copiaCola),
            FormaEscolhida = registro?.FormaDePagamento ?? string.Empty,
            PagamentoAprovado = aprovado,
            CodigoLiberacao = aprovado ? _codigos.Ultimo(login, reserva.Mesa.Id) : string.Empty
        });
    }

    [HttpPost("confirmacao-saida")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmacaoSaida()
    {
        var sessao = _sessoes.Read();
        var reserva = _mesasCliente.Read();
        if (sessao is null
            || reserva is null
            || reserva.Guid == Guid.Empty
            || reserva.SessaoEstabelecimento.Guid != sessao.Guid
            || reserva.Mesa.Id <= 0)
        {
            return Json(new { ok = false, mensagem = "Não há sessão de mesa para liberar." });
        }

        var login = ContaCliente.Login(HttpContext, reserva);
        if (!ValidacaoPagamentoStore.EstaAprovado(_validacoes.StatusDoCliente(sessao.Guid.ToString(), login, reserva.Guid.ToString())))
        {
            return Json(new { ok = false, mensagem = "O pagamento ainda não foi confirmado." });
        }

        var pessoa = ContaCliente.Pessoa(_clientes, login, reserva.Cliente);
        var itens = ContaCliente.Itens(_consumo.Read(), sessao, reserva, login);
        var codigo = _codigos.Gerar(pessoa, reserva.Mesa, itens, _pagamentos.Read(sessao.Guid, login)?.FormaDePagamento ?? string.Empty);
        return Json(new { ok = true, codigo });
    }

    [HttpGet("checklist-saida")]
    public IActionResult ChecklistSaida()
    {
        var sessao = _sessoes.Read();
        var reserva = _mesasCliente.Read();
        var mesa = reserva is not null && sessao is not null && reserva.SessaoEstabelecimento.Guid == sessao.Guid
            ? reserva.Guid
            : Guid.Empty;
        var login = ContaCliente.Login(HttpContext, reserva);
        var pronto = sessao is not null && sessao.Guid != Guid.Empty && _codigos.ChecklistConfirmado(sessao.Guid, mesa, login);
        return Json(new { pronto });
    }
}
