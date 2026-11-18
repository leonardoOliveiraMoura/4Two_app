namespace C4Two.Models;

public class CompartilhamentoConsumo
{
    public Guid Id { get; set; }

    public Guid ConsumoId { get; set; }

    public Guid SessaoEstabelecimento { get; set; }

    public int MesaId { get; set; }

    public int IdEstabelecimento { get; set; }

    public string LoginAutor { get; set; } = string.Empty;

    public string NomeCliente { get; set; } = string.Empty;

    public string NomeItem { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public string Categoria { get; set; } = string.Empty;

    public string Preco { get; set; } = string.Empty;

    public List<string> Aceites { get; set; } = new();

    public List<string> Recusas { get; set; } = new();
}

public class OcupanteSentado
{
    public Guid SessaoEstabelecimento { get; set; }

    public int MesaId { get; set; }

    public int IdEstabelecimento { get; set; }

    public string Login { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;
}

public readonly record struct ParteIndividual(bool Compartilhado, bool DoCliente, decimal Valor);

public class CompartilhamentoConsumoArquivo
{
    public List<OcupanteSentado> Ocupantes { get; set; } = new();

    public List<CompartilhamentoConsumo> Compartilhamentos { get; set; } = new();
}
