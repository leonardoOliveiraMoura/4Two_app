using C4Two.Filters;
using C4Two.Models;
using C4Two.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("cliente")]
[RequireLoginSession(LoginSession.Cliente, "LoginClientes")]
public class ClienteController : Controller
{
    private readonly CadClienteStore _clientes;
    private readonly CadGerenciamentoStore _cadastros;
    private readonly SessaoEstabelecimentoStore _sessoes;
    private readonly MockCredentialReader _credentials;

    public ClienteController(
        CadClienteStore clientes,
        CadGerenciamentoStore cadastros,
        SessaoEstabelecimentoStore sessoes,
        MockCredentialReader credentials)
    {
        _clientes = clientes;
        _cadastros = cadastros;
        _sessoes = sessoes;
        _credentials = credentials;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Sucesso"] = TempData["PerfilSalvo"] as string;
        CarregarEstabelecimentos();
        return View(ClienteAtual());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public IActionResult Index(CadCliente model)
    {
        if (!ModelState.IsValid)
        {
            CarregarEstabelecimentos();
            return View(model);
        }

        model.Login = model.Login.Trim();
        var loginAtual = HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        if (!string.Equals(loginAtual.Trim(), model.Login.Trim(), StringComparison.Ordinal)
            && (_clientes.Exists(model.Login) || _credentials.Exists("cliente.json", model.Login)))
        {
            ModelState.AddModelError(nameof(model.Login), "Login já cadastrado.");
            CarregarEstabelecimentos();
            return View(model);
        }

        try
        {
            _clientes.Update(loginAtual, model);
            _credentials.Update("cliente.json", loginAtual, model.Login, model.Senha);
            LoginSession.SignIn(HttpContext.Session, LoginSession.Cliente, model.Login);
        }
        catch (IOException)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível salvar os dados do perfil.");
            CarregarEstabelecimentos();
            return View(model);
        }

        TempData["PerfilSalvo"] = "Dados do perfil atualizados.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("sessao-estabelecimento/{id:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult AbrirSessaoEstabelecimento(int id)
    {
        var estabelecimento = _cadastros.Read().CadastroEstabelecimento.FirstOrDefault(item => item.Id == id);
        if (estabelecimento == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var cliente = ClienteAtual();
        _sessoes.Save(new SessaoEstabelecimento
        {
            Guid = Guid.NewGuid(),
            IdEstabelecimento = estabelecimento.Id,
            Nome = estabelecimento.Nome,
            Tipo = estabelecimento.Tipo,
            Login = HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty,
            NomeCliente = cliente.Nome,
            DataHoraAbertura = DateTime.Now
        });

        return RedirectToAction("Index", "SessaoEstabelecimento");
    }

    private CadCliente ClienteAtual()
    {
        var login = HttpContext.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        return _clientes.Find(login);
    }

    private void CarregarEstabelecimentos()
    {
        ViewData["Estabelecimentos"] = _cadastros.Read().CadastroEstabelecimento;
    }
}
