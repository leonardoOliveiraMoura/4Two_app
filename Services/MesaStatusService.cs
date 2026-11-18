using C4Two.Models;

namespace C4Two.Services;

public class MesaStatusService
{
    public const string Disponivel = "disponivel";

    private readonly CadGerenciamentoStore _cadastros;
    private readonly SessaoMesaClienteStore _sessoes;
    private readonly ValidacaoPagamentoStore _pagamentos;
    private readonly CodigoLiberacaoStore _codigos;
    private readonly CompartilhamentoConsumoStore _compartilhamentos;

    public MesaStatusService(
        CadGerenciamentoStore cadastros,
        SessaoMesaClienteStore sessoes,
        ValidacaoPagamentoStore pagamentos,
        CodigoLiberacaoStore codigos,
        CompartilhamentoConsumoStore compartilhamentos)
    {
        _cadastros = cadastros;
        _sessoes = sessoes;
        _pagamentos = pagamentos;
        _codigos = codigos;
        _compartilhamentos = compartilhamentos;
    }

    public bool TodosOcupantesPagos(int mesaId, int idEstabelecimento)
    {
        var ocupantes = Ocupantes()
            .Where(item => item.MesaId == mesaId && item.IdEstabelecimento == idEstabelecimento)
            .ToList();
        if (ocupantes.Count == 0)
        {
            return false;
        }

        var pagos = Pagos(ocupantes);
        return ocupantes.All(pagos.Contains);
    }

    public List<MesaStatusLinha> Atualizar()
    {
        var ocupantes = Ocupantes();
        var pagos = Pagos(ocupantes);
        var atualizadas = new HashSet<int>();
        foreach (var grupo in ocupantes.GroupBy(item => (item.MesaId, item.IdEstabelecimento)))
        {
            if (grupo.Any(item => !pagos.Contains(item)))
            {
                continue;
            }

            _cadastros.AtualizarStatusMesa(grupo.Key.MesaId, Disponivel);
            _sessoes.AtualizarStatusMesa(grupo.Key.MesaId, grupo.Key.IdEstabelecimento, Disponivel);
            atualizadas.Add(grupo.Key.MesaId);
        }

        var cadastros = _cadastros.Read();
        var nomes = cadastros.CadastroEstabelecimento.ToDictionary(item => item.Id, item => item.Nome);
        return cadastros.CadastroMesa
            .OrderBy(item => item.IdEstabelecimento)
            .ThenBy(item => item.Nome)
            .Select(item => new MesaStatusLinha
            {
                Id = item.Id,
                IdEstabelecimento = item.IdEstabelecimento,
                Estabelecimento = nomes.TryGetValue(item.IdEstabelecimento, out var nome) ? nome : "Estabelecimento",
                Nome = item.Nome,
                Tipo = item.Tipo,
                Status = Texto(item.Status),
                Atualizada = atualizadas.Contains(item.Id)
            })
            .ToList();
    }

    private HashSet<OcupanteMesa> Pagos(List<OcupanteMesa> ocupantes)
    {
        var pagamentos = _pagamentos.ListarPagamentos();
        return ocupantes
            .Where(ocupante => pagamentos.Any(pagamento => pagamento.Aprovado && MesmaPessoa(pagamento, ocupante))
                || !string.IsNullOrWhiteSpace(_codigos.Ultimo(ocupante.Login, ocupante.MesaId)))
            .ToHashSet();
    }

    private List<OcupanteMesa> Ocupantes()
    {
        var sentados = _compartilhamentos.Ocupantes()
            .Select(item => new OcupanteMesa
            {
                MesaId = item.MesaId,
                IdEstabelecimento = item.IdEstabelecimento,
                Login = item.Login
            });
        return _sessoes.Listar()
            .Concat(sentados)
            .GroupBy(item => (item.MesaId, item.IdEstabelecimento, Login: (item.Login ?? string.Empty).Trim().ToLowerInvariant()))
            .Select(grupo => grupo.First())
            .ToList();
    }

    private static bool MesmaPessoa(PagamentoMesa pagamento, OcupanteMesa ocupante)
    {
        return pagamento.MesaId == ocupante.MesaId
            && pagamento.IdEstabelecimento == ocupante.IdEstabelecimento
            && !string.IsNullOrWhiteSpace(pagamento.Login)
            && string.Equals(pagamento.Login, ocupante.Login, StringComparison.OrdinalIgnoreCase);
    }

    public static string Texto(string? status)
    {
        return (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "disponivel" => "disponível",
            "indisponivel" => "indisponível",
            "reservada" => "reservada",
            "" => "disponível",
            var atual => atual
        };
    }
}
