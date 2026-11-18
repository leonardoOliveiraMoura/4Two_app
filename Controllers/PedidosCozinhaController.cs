using C4Two.Filters;
using C4Two.Models;
using C4Two.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("pedidosCozinha")]
[RequireLoginSession(LoginSession.Gerenciamento, "LoginGerenciamento")]
public class PedidosCozinhaController : Controller
{
    private readonly MesaConsumoStore _consumo;

    public PedidosCozinhaController(MesaConsumoStore consumo)
    {
        _consumo = consumo;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var pedidos = _consumo.Read()
            .Where(item => item.Item is not null)
            .OrderBy(item => item.Item.DataHora)
            .ToList();
        return View(pedidos);
    }

    [HttpPost("status")]
    [ValidateAntiForgeryToken]
    public IActionResult AtualizarStatus(Guid id, string statusPedidoCozinha)
    {
        _consumo.DefinirStatusPedidoCozinha(id, statusPedidoCozinha);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("entrega")]
    [ValidateAntiForgeryToken]
    public IActionResult AtualizarEntrega(Guid id, string statusEntrega)
    {
        _consumo.AtualizarStatusEntrega(id, string.Equals(statusEntrega, "entregue", StringComparison.OrdinalIgnoreCase));
        return RedirectToAction(nameof(Index));
    }
}
