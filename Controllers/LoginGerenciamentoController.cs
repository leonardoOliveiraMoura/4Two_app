using C4Two.Models;
using C4Two.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("loginGerenciamento")]
public class LoginGerenciamentoController : Controller
{
    private readonly MockCredentialReader _credentials;

    public LoginGerenciamentoController(MockCredentialReader credentials)
    {
        _credentials = credentials;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        if (LoginSession.IsNavigation(Request))
        {
            LoginSession.End(HttpContext);
        }

        var model = new LoginViewModel();
        if (TempData[LoginSession.Aviso] is string aviso)
        {
            ModelState.AddModelError(string.Empty, aviso);
        }

        return View(model);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public IActionResult Index(LoginViewModel model)
    {
        LoginSession.End(HttpContext);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!_credentials.TryValidate("gerenciamento.json", model.Login, model.Senha, out var error))
        {
            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        LoginSession.SignIn(HttpContext.Session, LoginSession.Gerenciamento, model.Login);
        return RedirectToAction("Index", "Gerenciamento");
    }
}
