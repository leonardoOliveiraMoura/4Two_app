using C4Two.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace C4Two.Filters;

public sealed class AvaliacaoSessaoFilter : IActionFilter
{
    private static readonly HashSet<string> Controladores = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cliente",
        "SessaoEstabelecimento",
        "VisualizarConta",
        "FormaDePagamento",
        "ValidaPagamentos"
    };

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            return;
        }

        if (context.ActionDescriptor is not ControllerActionDescriptor acao
            || !string.Equals(acao.ActionName, "Index", StringComparison.OrdinalIgnoreCase)
            || !Controladores.Contains(acao.ControllerName))
        {
            return;
        }

        if (!LoginSession.IsAuthenticated(context.HttpContext.Session, LoginSession.Cliente))
        {
            return;
        }

        var sessoes = context.HttpContext.RequestServices.GetRequiredService<SessaoEstabelecimentoStore>();
        var mesas = context.HttpContext.RequestServices.GetRequiredService<SessaoMesaClienteStore>();
        var codigos = context.HttpContext.RequestServices.GetRequiredService<CodigoLiberacaoStore>();
        var sessao = sessoes.Read();
        if (sessao is null || sessao.Guid == Guid.Empty)
        {
            return;
        }

        var reserva = mesas.Read();
        var sessaoMesa = reserva is not null && reserva.SessaoEstabelecimento.Guid == sessao.Guid
            ? reserva.Guid
            : Guid.Empty;
        var login = context.HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        if (!codigos.ChecklistConfirmado(sessao.Guid, sessaoMesa, login))
        {
            return;
        }

        var avaliacoes = context.HttpContext.RequestServices.GetRequiredService<AvaliacaoClienteStore>();
        if (!string.IsNullOrWhiteSpace(avaliacoes.NotaDaSessao(sessao.Guid, sessaoMesa, login)))
        {
            return;
        }

        context.Result = new RedirectToActionResult("Index", "Avaliacao", null);
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
