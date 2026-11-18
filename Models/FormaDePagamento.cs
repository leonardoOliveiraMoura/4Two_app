namespace C4Two.Models;

public class FormaDePagamentoPagina
{
    public string Cliente { get; set; } = string.Empty;

    public string Valor { get; set; } = "0,00";

    public string CopiaCola { get; set; } = string.Empty;

    public string QrCode { get; set; } = string.Empty;

    public string FormaEscolhida { get; set; } = string.Empty;

    public bool PagamentoAprovado { get; set; }

    public string CodigoLiberacao { get; set; } = string.Empty;
}

public class FormaDePagamentoRegistro
{
    public string FormaDePagamento { get; set; } = string.Empty;

    public SessaoEstabelecimento? SessaoEstabelecimento { get; set; }

    public SessaoMesaCliente? SessaoMesaCliente { get; set; }

    public List<MesaConsumoItem> MesaConsumo { get; set; } = new();
}
