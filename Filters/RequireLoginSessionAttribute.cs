using C4Two.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace C4Two.Filters;

public sealed class RequireLoginSessionAttribute : ActionFilterAttribute
{
    private readonly string _sessionKey;
    private readonly string _loginController;

    public RequireLoginSessionAttribute(string sessionKey, string loginController)
    {
        _sessionKey = sessionKey;
        _loginController = loginController;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (LoginSession.IsAuthenticated(context.HttpContext.Session, _sessionKey))
        {
            return;
        }

        if (!LoginSession.IsNavigation(context.HttpContext.Request))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status401Unauthorized);
            return;
        }

        if (context.Controller is Controller controller)
        {
            controller.TempData[LoginSession.Aviso] = LoginSession.AcessoNegado;
        }

        context.Result = new RedirectToActionResult("Index", _loginController, null);
    }
}
