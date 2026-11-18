namespace C4Two.Models;

public class ValidacaoPagamentoLinha
{
    public string TokenPago { get; set; } = string.Empty;

    public string Mesa { get; set; } = string.Empty;

    public string Cliente { get; set; } = string.Empty;

    public string StatusPagamento { get; set; } = string.Empty;

    public string FormaDePagamento { get; set; } = string.Empty;

    public List<string> ItensEntregues { get; set; } = new();
}
