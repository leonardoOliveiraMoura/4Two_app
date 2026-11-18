using System.Globalization;
using C4Two.Filters;
using C4Two.Models;
using C4Two.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("visualizarConta")]
[Route("vizualizarConta")]
[RequireLoginSession(LoginSession.Cliente, "LoginClientes")]
public class VisualizarContaController : Controller
{
    private readonly SessaoEstabelecimentoStore _sessoes;
    private readonly SessaoMesaClienteStore _mesasCliente;
    private readonly MesaConsumoStore _consumo;
    private readonly VisualizarContaStore _contas;
    private readonly ValidacaoPagamentoStore _validacoes;
    private readonly CadClienteStore _clientes;
    private readonly CompartilhamentoConsumoStore _compartilhamentos;

    public VisualizarContaController(
        SessaoEstabelecimentoStore sessoes,
        SessaoMesaClienteStore mesasCliente,
        MesaConsumoStore consumo,
        VisualizarContaStore contas,
        ValidacaoPagamentoStore validacoes,
        CadClienteStore clientes,
        CompartilhamentoConsumoStore compartilhamentos)
    {
        _sessoes = sessoes;
        _mesasCliente = mesasCliente;
        _consumo = consumo;
        _contas = contas;
        _validacoes = validacoes;
        _clientes = clientes;
        _compartilhamentos = compartilhamentos;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var conta = Montar();
        if (conta is null)
        {
            return RedirectToAction("Index", "SessaoEstabelecimento");
        }

        _contas.Save(conta);
        return View(conta);
    }

    [HttpPost("fechar")]
    [ValidateAntiForgeryToken]
    public IActionResult Fechar()
    {
        var conta = Montar();
        if (conta is not null)
        {
            _contas.Save(conta);
        }

        return RedirectToAction("Index", "FormaDePagamento");
    }

    private VisualizarConta? Montar()
    {
        var sessao = _sessoes.Read();
        var reserva = _mesasCliente.Read();
        if (sessao is null
            || reserva is null
            || reserva.Guid == Guid.Empty
            || reserva.SessaoEstabelecimento.Guid != sessao.Guid
            || reserva.Mesa.Id <= 0)
        {
            return null;
        }

        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        var login = ContaCliente.Login(HttpContext, reserva);
        var pagas = _validacoes.SessoesPagas();
        var chave = ContaCliente.ChavePaga(sessao.Guid, login);
        var itens = pagas.Contains(chave)
            ? new List<VisualizarContaItem>()
            : ContaCliente.ItensDaConta(_compartilhamentos, _consumo.Read(), sessao, reserva, login);
        var soma = ContaCliente.Total(itens);
        var pessoa = ContaCliente.Pessoa(_clientes, login, reserva.Cliente);
        var nome = string.IsNullOrWhiteSpace(pessoa.Nome) ? sessao.NomeCliente : pessoa.Nome;
        return new VisualizarConta
        {
            Cliente = nome,
            Mesa = TituloMesa(reserva.Mesa.Nome),
            Itens = itens,
            Total = soma.ToString("N2", cultura)
        };
    }

    private static string TituloMesa(string? nome)
    {
        var texto = string.IsNullOrWhiteSpace(nome) ? "Mesa" : nome.Trim();
        var indice = texto.Length;
        while (indice > 0 && char.IsDigit(texto[indice - 1]))
        {
            indice--;
        }

        if (indice > 0 && indice < texto.Length && texto[indice - 1] != ' ')
        {
            return texto[..indice] + " " + texto[indice..];
        }

        return texto;
    }
}
