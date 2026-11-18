namespace C4Two.Models;

public class PedidoItemDetalhe
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Preco { get; set; } = string.Empty;

    public DateTime DataHora { get; set; }

    public string StatusEntrega { get; set; } = "não entregue";
}

public class PedidoMonitoramento
{
    public string Estabelecimento { get; set; } = string.Empty;

    public string Mesa { get; set; } = string.Empty;

    public string Cliente { get; set; } = string.Empty;

    public string Total { get; set; } = string.Empty;

    public List<PedidoItemDetalhe> Itens { get; set; } = new();
}

public class IntegranteMesa
{
    public string Login { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public bool Atual { get; set; }

    public List<MesaConsumoItem> Itens { get; set; } = new();
}

public class MesaConsumoItem
{
    public Guid Id { get; set; }

    public SessaoEstabelecimento SessaoEstabelecimento { get; set; } = new();

    public SessaoMesaCliente SessaoMesaCliente { get; set; } = new();

    public ItemConsumoMesa Item { get; set; } = new();
}

public class ItemConsumoMesa
{
    public int Id { get; set; }

    public int IdEstabelecimento { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Categoria { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public string Preco { get; set; } = string.Empty;

    public string PreparoCozinha { get; set; } = "não";

    public DateTime DataHora { get; set; }

    public string StatusEntrega { get; set; } = "não entregue";

    public string StatusPedidoCozinha { get; set; } = "Não Iniciado";

    public string Compartilhado { get; set; } = "não";
}
