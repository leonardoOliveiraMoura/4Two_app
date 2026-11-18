using System.Globalization;
using C4Two.Models;

namespace C4Two.Services;

public static class ContaCliente
{
    public static string Login(HttpContext http, SessaoMesaCliente? reserva)
    {
        var login = http.Session.GetString(LoginSession.Cliente) ?? string.Empty;
        return string.IsNullOrWhiteSpace(login) ? reserva?.Cliente?.Login ?? string.Empty : login;
    }

    public static ClienteSessao Pessoa(CadClienteStore clientes, string login, ClienteSessao? reserva)
    {
        var cliente = clientes.Find(login);
        if (string.IsNullOrWhiteSpace(cliente.Login))
        {
            if (reserva is not null && string.Equals(reserva.Login, login, StringComparison.OrdinalIgnoreCase))
            {
                return reserva;
            }

            return new ClienteSessao { Login = login, Nome = login };
        }

        return new ClienteSessao
        {
            Login = cliente.Login,
            Id = cliente.Id,
            Cpf = cliente.Cpf,
            Nome = string.IsNullOrWhiteSpace(cliente.Nome) ? login : cliente.Nome,
            Email = cliente.Email,
            Telefone = cliente.Telefone,
            Endereco = cliente.Endereco,
            Cidade = cliente.Cidade,
            Estado = cliente.Estado,
            Cep = cliente.Cep,
            Pais = cliente.Pais
        };
    }

    public static SessaoMesaCliente ComCliente(SessaoMesaCliente reserva, ClienteSessao cliente)
    {
        return new SessaoMesaCliente
        {
            Guid = reserva.Guid,
            SessaoEstabelecimento = reserva.SessaoEstabelecimento,
            Mesa = reserva.Mesa,
            CapacidadeAtualStatus = reserva.CapacidadeAtualStatus,
            Cliente = cliente
        };
    }

    public static List<VisualizarContaItem> ItensDaConta(
        CompartilhamentoConsumoStore compartilhamentos,
        IEnumerable<MesaConsumoItem> consumo,
        SessaoEstabelecimento sessao,
        SessaoMesaCliente reserva,
        string login)
    {
        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        var linhas = new List<VisualizarContaItem>();
        foreach (var item in consumo.Where(atual => DaMesa(atual, sessao, reserva)).OrderBy(atual => atual.Item.DataHora))
        {
            var cheio = LerPreco(item.Item?.Preco, cultura);
            var parte = compartilhamentos.ParteIndividual(
                item.Id,
                login,
                sessao.Guid,
                reserva.Mesa.Id,
                reserva.Mesa.IdEstabelecimento,
                cheio);
            if (parte.Compartilhado)
            {
                if (!parte.DoCliente)
                {
                    continue;
                }

                cheio = parte.Valor;
            }
            else if (!MesmoLogin(item, login))
            {
                continue;
            }

            linhas.Add(new VisualizarContaItem
            {
                Nome = item.Item?.Nome ?? string.Empty,
                Preco = cheio.ToString("N2", cultura),
                DataHora = item.Item?.DataHora ?? default,
                StatusEntrega = string.IsNullOrWhiteSpace(item.Item?.StatusEntrega) ? "não entregue" : item.Item.StatusEntrega
            });
        }

        return linhas;
    }

    public static decimal Total(IEnumerable<VisualizarContaItem> itens)
    {
        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        return itens.Sum(item => LerPreco(item.Preco, cultura));
    }

    private static decimal LerPreco(string? texto, CultureInfo cultura)
    {
        return decimal.TryParse(texto, NumberStyles.Number, cultura, out var valor) ? valor : 0m;
    }

    public static List<MesaConsumoItem> Itens(IEnumerable<MesaConsumoItem> todos, SessaoEstabelecimento sessao, SessaoMesaCliente reserva, string login)
    {
        return todos.Where(item => DaMesa(item, sessao, reserva) && MesmoLogin(item, login)).ToList();
    }

    public static bool DaMesa(MesaConsumoItem item, SessaoEstabelecimento sessao, SessaoMesaCliente reserva)
    {
        return item.SessaoMesaCliente?.Mesa is not null
            && item.SessaoEstabelecimento.Guid == sessao.Guid
            && item.SessaoMesaCliente.Mesa.Id == reserva.Mesa.Id
            && item.SessaoMesaCliente.Mesa.IdEstabelecimento == reserva.Mesa.IdEstabelecimento;
    }

    public static bool MesmoLogin(MesaConsumoItem item, string login)
    {
        return string.Equals(item.SessaoMesaCliente?.Cliente?.Login, login, StringComparison.OrdinalIgnoreCase);
    }

    public static string ChavePaga(Guid sessao, string? login)
    {
        return sessao.ToString() + "|" + (login ?? string.Empty).Trim().ToLowerInvariant();
    }
}
