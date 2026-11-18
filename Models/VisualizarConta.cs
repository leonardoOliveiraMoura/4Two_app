namespace C4Two.Models;

public class VisualizarConta
{
    public string Cliente { get; set; } = string.Empty;

    public string Mesa { get; set; } = string.Empty;

    public string Total { get; set; } = "0,00";

    public List<VisualizarContaItem> Itens { get; set; } = new();
}

public class VisualizarContaItem
{
    public string Nome { get; set; } = string.Empty;

    public string Preco { get; set; } = string.Empty;

    public DateTime DataHora { get; set; }

    public string StatusEntrega { get; set; } = "não entregue";
}
