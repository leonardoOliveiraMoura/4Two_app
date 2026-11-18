using System.Globalization;
using C4Two.Filters;
using C4Two.Models;
using C4Two.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("formaDePagamento")]
[RequireLoginSession(LoginSession.Cliente, "LoginClientes")]
public class FormaDePagamentoController : Controller
{
    private static readonly string[] Formas =
    {
        "PIX QR dinâmico ou copia e cola",
        "Cartão por aproximação",
        "Carteira digital",
        "Voucher-Ticket"
    };

    private readonly SessaoEstabelecimentoStore _sessoes;
    private readonly SessaoMesaClienteStore _mesasCliente;
    private readonly MesaConsumoStore _consumo;
    private readonly FormaDePagamentoStore _pagamentos;
    private readonly ValidacaoPagamentoStore _validacoes;
    private readonly CadClienteStore _clientes;
    private readonly CompartilhamentoConsumoStore _compartilhamentos;

    public FormaDePagamentoController(
        SessaoEstabelecimentoStore sessoes,
        SessaoMesaClienteStore mesasCliente,
        MesaConsumoStore consumo,
        FormaDePagamentoStore pagamentos,
        ValidacaoPagamentoStore validacoes,
        CadClienteStore clientes,
        CompartilhamentoConsumoStore compartilhamentos)
    {
        _sessoes = sessoes;
        _mesasCliente = mesasCliente;
        _consumo = consumo;
        _pagamentos = pagamentos;
        _validacoes = validacoes;
        _clientes = clientes;
        _compartilhamentos = compartilhamentos;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var pagina = MontarPagina();
        if (pagina is null)
        {
            return RedirectToAction("Index", "SessaoEstabelecimento");
        }

        return View(pagina);
    }

    [HttpPost("escolher")]
    [ValidateAntiForgeryToken]
    public IActionResult Escolher(string forma)
    {
        if (string.IsNullOrWhiteSpace(forma) || !Formas.Contains(forma))
        {
            return Json(new { ok = false, mensagem = "Escolha uma forma de pagamento." });
        }

        var sessao = _sessoes.Read();
        var reserva = _mesasCliente.Read();
        if (sessao is null || reserva is null || !ReservaValida(sessao, reserva))
        {
            return Json(new { ok = false, mensagem = "Não há sessão de mesa para pagar." });
        }

        var login = ContaCliente.Login(HttpContext, reserva);
        var conta = ContaCliente.ComCliente(reserva, ContaCliente.Pessoa(_clientes, login, reserva.Cliente));
        var json = _pagamentos.Save(new FormaDePagamentoRegistro
        {
            FormaDePagamento = forma,
            SessaoEstabelecimento = sessao,
            SessaoMesaCliente = conta,
            MesaConsumo = ContaCliente.Itens(_consumo.Read(), sessao, reserva, login)
        });
        _validacoes.CriarDePagamento(json);

        return Json(new { ok = true });
    }

    private FormaDePagamentoPagina? MontarPagina()
    {
        var sessao = _sessoes.Read();
        var reserva = _mesasCliente.Read();
        if (!ReservaValida(sessao, reserva))
        {
            return null;
        }

        var mesa = reserva!;
        var estabelecimento = sessao!;
        var login = ContaCliente.Login(HttpContext, mesa);
        var pessoa = ContaCliente.Pessoa(_clientes, login, mesa.Cliente);
        var nome = string.IsNullOrWhiteSpace(pessoa.Nome) ? estabelecimento.NomeCliente : pessoa.Nome;
        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        var total = ContaCliente.Total(ContaCliente.ItensDaConta(_compartilhamentos, _consumo.Read(), estabelecimento, mesa, login));
        var copiaCola = PixCopiaCola.Montar(
            pessoa.Email,
            string.IsNullOrWhiteSpace(estabelecimento.Nome) ? nome : estabelecimento.Nome,
            pessoa.Cidade,
            total);
        return new FormaDePagamentoPagina
        {
            Cliente = string.IsNullOrWhiteSpace(nome) ? "Nome do ciente" : nome,
            Valor = total.ToString("N2", cultura),
            CopiaCola = copiaCola,
            QrCode = PixCopiaCola.PngBase64(copiaCola)
        };
    }

    private static bool ReservaValida(SessaoEstabelecimento? sessao, SessaoMesaCliente? reserva)
    {
        return sessao is not null
            && reserva is not null
            && reserva.Guid != Guid.Empty
            && reserva.SessaoEstabelecimento.Guid == sessao.Guid
            && reserva.Mesa.Id > 0;
    }
}
