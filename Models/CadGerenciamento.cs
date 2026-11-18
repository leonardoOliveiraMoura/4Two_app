using System.ComponentModel.DataAnnotations;

namespace C4Two.Models;

public class GerenciamentoPage
{
    public CadGerenciamento Cadastros { get; set; } = new();

    public EstabelecimentoItem NovoEstabelecimento { get; set; } = new();

    public ItemConsumoItem NovoItemConsumo { get; set; } = new();

    public MesaItem NovaMesa { get; set; } = new();

    public int IndiceEstabelecimento { get; set; } = -1;

    public int IndiceItemConsumo { get; set; } = -1;

    public int IndiceMesa { get; set; } = -1;
}

public class CadGerenciamento
{
    public List<EstabelecimentoItem> CadastroEstabelecimento { get; set; } = new();

    public List<ItemConsumoItem> CadastroItensConsumo { get; set; } = new();

    public List<MesaItem> CadastroMesa { get; set; } = new();
}

public class EstabelecimentoItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o tipo.")]
    [Display(Name = "Tipo")]
    public string Tipo { get; set; } = string.Empty;

    [Display(Name = "Telefone")]
    public string Telefone { get; set; } = string.Empty;

    [Display(Name = "Endereço")]
    public string Endereco { get; set; } = string.Empty;

    [Display(Name = "Cidade")]
    public string Cidade { get; set; } = string.Empty;
}

public class ItemConsumoItem
{
    public int Id { get; set; }

    [Display(Name = "Estabelecimento")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecione o estabelecimento.")]
    public int IdEstabelecimento { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a categoria.")]
    [Display(Name = "Categoria")]
    public string Categoria { get; set; } = string.Empty;

    [Display(Name = "Descrição")]
    public string Descricao { get; set; } = string.Empty;

    [Display(Name = "Preço")]
    public string Preco { get; set; } = string.Empty;

    [Display(Name = "item de preparo na cozinha?")]
    public string PreparoCozinha { get; set; } = "não";
}

public class MesaItem
{
    public int Id { get; set; }

    [Display(Name = "Estabelecimento")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecione o estabelecimento.")]
    public int IdEstabelecimento { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o tipo.")]
    [Display(Name = "Tipo")]
    public string Tipo { get; set; } = string.Empty;

    [Display(Name = "Capacidade")]
    public string Capacidade { get; set; } = string.Empty;

    public string Status { get; set; } = "disponivel";

    public string QrCode { get; set; } = string.Empty;
}
