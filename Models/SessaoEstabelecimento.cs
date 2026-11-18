namespace C4Two.Models;

public class SessaoEstabelecimento
{
    public Guid Guid { get; set; }

    public int IdEstabelecimento { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty;

    public string Login { get; set; } = string.Empty;

    public string NomeCliente { get; set; } = string.Empty;

    public DateTime DataHoraAbertura { get; set; }
}
