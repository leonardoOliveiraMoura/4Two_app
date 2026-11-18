using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("main")]
public class MainController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }
}
