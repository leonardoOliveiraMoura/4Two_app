using C4Two.Filters;
using C4Two.Models;
using C4Two.Services;
using Microsoft.AspNetCore.Mvc;

namespace C4Two.Controllers;

[Route("gerenciamento")]
[RequireLoginSession(LoginSession.Gerenciamento, "LoginGerenciamento")]
public class GerenciamentoController : Controller
{
    private readonly CadGerenciamentoStore _cadastros;
    private readonly MesaConsumoStore _consumo;
    private readonly ValidacaoPagamentoStore _validacoes;
    private readonly CodigoLiberacaoStore _codigos;
    private readonly MesaStatusService _mesaStatus;

    public GerenciamentoController(CadGerenciamentoStore cadastros, MesaConsumoStore consumo, ValidacaoPagamentoStore validacoes, CodigoLiberacaoStore codigos, MesaStatusService mesaStatus)
    {
        _cadastros = cadastros;
        _consumo = consumo;
        _validacoes = validacoes;
        _codigos = codigos;
        _mesaStatus = mesaStatus;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Sucesso"] = TempData["CadastroSalvo"] as string;
        return Pagina(new GerenciamentoPage
        {
            Cadastros = _cadastros.Read()
        });
    }

    [HttpGet("editar-estabelecimento/{indice:int}")]
    public IActionResult EditarEstabelecimento(int indice)
    {
        var atual = _cadastros.Read();
        if (indice < 0 || indice >= atual.CadastroEstabelecimento.Count)
        {
            return RedirectToAction(nameof(Index));
        }

        var item = atual.CadastroEstabelecimento[indice];
        return Pagina(new GerenciamentoPage
        {
            Cadastros = atual,
            IndiceEstabelecimento = indice,
            NovoEstabelecimento = new EstabelecimentoItem
            {
                Id = item.Id,
                Nome = item.Nome,
                Tipo = item.Tipo,
                Telefone = item.Telefone,
                Endereco = item.Endereco,
                Cidade = item.Cidade
            }
        });
    }

    [HttpGet("editar-itens-consumo/{indice:int}")]
    public IActionResult EditarItensConsumo(int indice)
    {
        var atual = _cadastros.Read();
        if (indice < 0 || indice >= atual.CadastroItensConsumo.Count)
        {
            return RedirectToAction(nameof(Index));
        }

        var item = atual.CadastroItensConsumo[indice];
        return Pagina(new GerenciamentoPage
        {
            Cadastros = atual,
            IndiceItemConsumo = indice,
            NovoItemConsumo = new ItemConsumoItem
            {
                Id = item.Id,
                IdEstabelecimento = item.IdEstabelecimento,
                Nome = item.Nome,
                Categoria = item.Categoria,
                Descricao = item.Descricao,
                Preco = item.Preco,
                PreparoCozinha = string.Equals(item.PreparoCozinha, "sim", StringComparison.OrdinalIgnoreCase) ? "sim" : "não"
            }
        });
    }

    [HttpGet("editar-mesa/{indice:int}")]
    public IActionResult EditarMesa(int indice)
    {
        var atual = _cadastros.Read();
        if (indice < 0 || indice >= atual.CadastroMesa.Count)
        {
            return RedirectToAction(nameof(Index));
        }

        var item = atual.CadastroMesa[indice];
        return Pagina(new GerenciamentoPage
        {
            Cadastros = atual,
            IndiceMesa = indice,
            NovaMesa = new MesaItem
            {
                Id = item.Id,
                IdEstabelecimento = item.IdEstabelecimento,
                Nome = item.Nome,
                Tipo = item.Tipo,
                Capacidade = item.Capacidade
            }
        });
    }

    [HttpPost("estabelecimento")]
    [ValidateAntiForgeryToken]
    public IActionResult Estabelecimento(EstabelecimentoItem novoEstabelecimento, int indiceEstabelecimento = -1)
    {
        var atual = _cadastros.Read();
        if (!ModelState.IsValid)
        {
            return Pagina(new GerenciamentoPage
            {
                Cadastros = atual,
                NovoEstabelecimento = novoEstabelecimento,
                IndiceEstabelecimento = indiceEstabelecimento
            });
        }

        if (indiceEstabelecimento >= 0)
        {
            if (indiceEstabelecimento >= atual.CadastroEstabelecimento.Count)
            {
                ModelState.AddModelError(string.Empty, "Item não encontrado para edição.");
                return Pagina(new GerenciamentoPage
                {
                    Cadastros = atual,
                    NovoEstabelecimento = novoEstabelecimento,
                    IndiceEstabelecimento = indiceEstabelecimento
                });
            }

            novoEstabelecimento.Id = atual.CadastroEstabelecimento[indiceEstabelecimento].Id;
            atual.CadastroEstabelecimento[indiceEstabelecimento] = novoEstabelecimento;
            return Salvar(atual, "Estabelecimento atualizado.");
        }

        novoEstabelecimento.Id = ProximoId(atual.CadastroEstabelecimento.Select(item => item.Id));
        atual.CadastroEstabelecimento.Add(novoEstabelecimento);
        return Salvar(atual, "Estabelecimento incluído.");
    }

    [HttpPost("itens-consumo")]
    [ValidateAntiForgeryToken]
    public IActionResult ItensConsumo(ItemConsumoItem novoItemConsumo, int indiceItemConsumo = -1)
    {
        novoItemConsumo.PreparoCozinha = string.Equals(novoItemConsumo.PreparoCozinha?.Trim(), "sim", StringComparison.OrdinalIgnoreCase)
            ? "sim"
            : "não";
        var atual = _cadastros.Read();
        if (!ModelState.IsValid)
        {
            return Pagina(new GerenciamentoPage
            {
                Cadastros = atual,
                NovoItemConsumo = novoItemConsumo,
                IndiceItemConsumo = indiceItemConsumo
            });
        }

        if (!EstabelecimentoExiste(atual, novoItemConsumo.IdEstabelecimento))
        {
            ModelState.AddModelError(string.Empty, "Selecione um estabelecimento válido.");
            return Pagina(new GerenciamentoPage
            {
                Cadastros = atual,
                NovoItemConsumo = novoItemConsumo,
                IndiceItemConsumo = indiceItemConsumo
            });
        }

        if (indiceItemConsumo >= 0)
        {
            if (indiceItemConsumo >= atual.CadastroItensConsumo.Count)
            {
                ModelState.AddModelError(string.Empty, "Item não encontrado para edição.");
                return Pagina(new GerenciamentoPage
                {
                    Cadastros = atual,
                    NovoItemConsumo = novoItemConsumo,
                    IndiceItemConsumo = indiceItemConsumo
                });
            }

            novoItemConsumo.Id = atual.CadastroItensConsumo[indiceItemConsumo].Id;
            atual.CadastroItensConsumo[indiceItemConsumo] = novoItemConsumo;
            return Salvar(atual, "Item de consumo atualizado.");
        }

        novoItemConsumo.Id = ProximoId(atual.CadastroItensConsumo.Select(item => item.Id));
        atual.CadastroItensConsumo.Add(novoItemConsumo);
        return Salvar(atual, "Item de consumo incluído.");
    }

    [HttpPost("excluir-itens-consumo/{indice:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult ExcluirItensConsumo(int indice)
    {
        var atual = _cadastros.Read();
        if (indice < 0 || indice >= atual.CadastroItensConsumo.Count)
        {
            return RedirectToAction(nameof(Index));
        }

        atual.CadastroItensConsumo.RemoveAt(indice);
        return Salvar(atual, "Item de consumo excluído.");
    }

    [HttpPost("mesa")]
    [ValidateAntiForgeryToken]
    public IActionResult Mesa(MesaItem novaMesa, int indiceMesa = -1)
    {
        var atual = _cadastros.Read();
        if (!ModelState.IsValid)
        {
            return Pagina(new GerenciamentoPage
            {
                Cadastros = atual,
                NovaMesa = novaMesa,
                IndiceMesa = indiceMesa
            });
        }

        if (!EstabelecimentoExiste(atual, novaMesa.IdEstabelecimento))
        {
            ModelState.AddModelError(string.Empty, "Selecione um estabelecimento válido.");
            return Pagina(new GerenciamentoPage
            {
                Cadastros = atual,
                NovaMesa = novaMesa,
                IndiceMesa = indiceMesa
            });
        }

        if (indiceMesa >= 0)
        {
            if (indiceMesa >= atual.CadastroMesa.Count)
            {
                ModelState.AddModelError(string.Empty, "Item não encontrado para edição.");
                return Pagina(new GerenciamentoPage
                {
                    Cadastros = atual,
                    NovaMesa = novaMesa,
                    IndiceMesa = indiceMesa
                });
            }

            var mesaAtual = atual.CadastroMesa[indiceMesa];
            novaMesa.Id = mesaAtual.Id;
            novaMesa.Status = string.IsNullOrWhiteSpace(mesaAtual.Status)
                ? "disponivel"
                : mesaAtual.Status;
            novaMesa.QrCode = string.IsNullOrWhiteSpace(mesaAtual.QrCode)
                ? Guid.NewGuid().ToString("N")
                : mesaAtual.QrCode;
            atual.CadastroMesa[indiceMesa] = novaMesa;
            return Salvar(atual, "Mesa atualizada.");
        }

        novaMesa.Id = ProximoId(atual.CadastroMesa.Select(item => item.Id));
        if (string.IsNullOrWhiteSpace(novaMesa.Status))
        {
            novaMesa.Status = "disponivel";
        }

        novaMesa.QrCode = Guid.NewGuid().ToString("N");
        atual.CadastroMesa.Add(novaMesa);
        return Salvar(atual, "Mesa incluída.");
    }

    [HttpPost("excluir-mesa/{indice:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult ExcluirMesa(int indice)
    {
        var atual = _cadastros.Read();
        if (indice < 0 || indice >= atual.CadastroMesa.Count)
        {
            return RedirectToAction(nameof(Index));
        }

        atual.CadastroMesa.RemoveAt(indice);
        return Salvar(atual, "Mesa excluída.");
    }

    [HttpPost("entrega-consumo/{id:guid}")]
    [ValidateAntiForgeryToken]
    public IActionResult AtualizarEntrega(Guid id, bool entregue)
    {
        var item = _consumo.AtualizarStatusEntrega(id, entregue);
        if (item is null)
        {
            return Json(new { ok = false, mensagem = "Item do pedido não encontrado." });
        }

        return Json(new { ok = true, statusEntrega = entregue ? "entregue" : "não entregue" });
    }

    [HttpPost("status-pagamento")]
    [ValidateAntiForgeryToken]
    public IActionResult AtualizarStatusPagamento(string tokenPago, string statusPagamento)
    {
        var token = _validacoes.AtualizarStatus(tokenPago, statusPagamento);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Json(new { ok = false, mensagem = "Não foi possível atualizar o status do pagamento." });
        }

        return Json(new { ok = true, statusPagamento, tokenPago = token });
    }

    [HttpPost("checklist-saida")]
    [ValidateAntiForgeryToken]
    public IActionResult AtualizarChecklistSaida(string codigo, bool marcado)
    {
        var dataHora = _codigos.MarcarChecklist(codigo, marcado);
        if (string.IsNullOrWhiteSpace(dataHora))
        {
            return Json(new { ok = false, mensagem = "Não foi possível registrar o checklist de saída." });
        }

        return Json(new { ok = true, marcado, saidaOk = marcado ? CodigoLiberacaoStore.SaidaOk : string.Empty, dataHora });
    }

    [HttpPost("atualizar-status-mesas")]
    [ValidateAntiForgeryToken]
    public IActionResult AtualizarStatusMesas()
    {
        var mesas = _mesaStatus.Atualizar();
        var estabelecimentos = _cadastros.Read().CadastroEstabelecimento
            .OrderBy(item => item.Nome)
            .Select(item => new { id = item.Id, nome = item.Nome })
            .ToList();
        return Json(new { ok = true, estabelecimentos, mesas });
    }

    private static int ProximoId(IEnumerable<int> ids)
    {
        var existentes = ids.ToList();
        return existentes.Count == 0 ? 1 : existentes.Max() + 1;
    }

    private static bool EstabelecimentoExiste(CadGerenciamento dados, int idEstabelecimento)
    {
        return dados.CadastroEstabelecimento.Any(item => item.Id == idEstabelecimento);
    }

    private static bool Atualizar<T>(List<T> itens, int indice, T item)
    {
        if (indice < 0 || indice >= itens.Count)
        {
            return false;
        }

        itens[indice] = item;
        return true;
    }

    private IActionResult Pagina(GerenciamentoPage pagina)
    {
        _cadastros.GarantirQrCodes(pagina.Cadastros.CadastroMesa);
        var consumo = _consumo.GarantirStatusEntrega();
        ViewData["Pedidos"] = PedidosPorCliente(consumo);
        ViewData["Validacoes"] = _validacoes.LerLinhas();
        ViewData["CodigosLiberados"] = _codigos.LerLinhas();
        return View("Index", pagina);
    }

    private static List<PedidoMonitoramento> PedidosPorCliente(List<MesaConsumoItem> itens)
    {
        return itens
            .Where(item => item.SessaoMesaCliente?.Mesa is not null && item.Item is not null)
            .GroupBy(item => (
                Estabelecimento: item.SessaoEstabelecimento?.Nome ?? string.Empty,
                Mesa: item.SessaoMesaCliente.Mesa.Nome ?? string.Empty,
                Cliente: item.SessaoMesaCliente.Cliente?.Nome ?? item.SessaoMesaCliente.Cliente?.Login ?? string.Empty))
            .OrderBy(grupo => grupo.Key.Estabelecimento)
            .ThenBy(grupo => grupo.Key.Mesa)
            .ThenBy(grupo => grupo.Key.Cliente)
            .Select(grupo =>
            {
                var detalhes = grupo
                    .OrderBy(item => item.Item.DataHora)
                    .Select(item => new PedidoItemDetalhe
                    {
                        Id = item.Id,
                        Nome = item.Item.Nome,
                        Preco = item.Item.Preco,
                        DataHora = item.Item.DataHora,
                        StatusEntrega = string.IsNullOrWhiteSpace(item.Item.StatusEntrega) ? "não entregue" : item.Item.StatusEntrega
                    })
                    .ToList();
                var cultura = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
                var soma = detalhes.Sum(item => decimal.TryParse(item.Preco, System.Globalization.NumberStyles.Number, cultura, out var valor) ? valor : 0m);
                return new PedidoMonitoramento
                {
                    Estabelecimento = grupo.Key.Estabelecimento,
                    Mesa = grupo.Key.Mesa,
                    Cliente = grupo.Key.Cliente,
                    Itens = detalhes,
                    Total = soma.ToString("N2", cultura)
                };
            })
            .ToList();
    }

    private IActionResult Salvar(CadGerenciamento dados, string mensagem)
    {
        try
        {
            _cadastros.Save(dados);
        }
        catch (IOException)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível salvar o cadastro.");
            return Pagina(new GerenciamentoPage
            {
                Cadastros = dados
            });
        }

        TempData["CadastroSalvo"] = mensagem;
        return RedirectToAction(nameof(Index));
    }
}
