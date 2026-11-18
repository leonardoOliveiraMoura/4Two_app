using C4Two.Models;
using C4Two.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("novoCliente")]
public class NovoClienteController : Controller
{
    private readonly CadClienteStore _clientes;
    private readonly MockCredentialReader _credentials;

    public NovoClienteController(CadClienteStore clientes, MockCredentialReader credentials)
    {
        _clientes = clientes;
        _credentials = credentials;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Sucesso"] = TempData["CadastroSalvo"] as string;
        return View(new CadCliente());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public IActionResult Index(CadCliente model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.Login = model.Login.Trim();
        if (_clientes.Exists(model.Login) || _credentials.Exists("cliente.json", model.Login))
        {
            ModelState.AddModelError(nameof(model.Login), "Login já cadastrado.");
            return View(model);
        }

        try
        {
            _clientes.Add(model);
            _credentials.Add("cliente.json", model.Login, model.Senha);
        }
        catch (IOException)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível salvar o cadastro.");
            return View(model);
        }

        TempData["CadastroSalvo"] = "Cadastro realizado.";
        return RedirectToAction(nameof(Index));
    }
}
