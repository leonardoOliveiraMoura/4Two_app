namespace C4Two.Models;

public class OcupanteMesa
{
    public string GuidSessao { get; set; } = string.Empty;

    public int MesaId { get; set; }

    public int IdEstabelecimento { get; set; }

    public string Login { get; set; } = string.Empty;
}

public class PagamentoMesa
{
    public string GuidSessao { get; set; } = string.Empty;

    public int MesaId { get; set; }

    public int IdEstabelecimento { get; set; }

    public string Login { get; set; } = string.Empty;

    public bool Aprovado { get; set; }
}

public class MesaStatusLinha
{
    public int Id { get; set; }

    public int IdEstabelecimento { get; set; }

    public string Estabelecimento { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool Atualizada { get; set; }
}

public class CodigoLiberacaoLinha
{
    public string Codigo { get; set; } = string.Empty;

    public bool CodigoValido { get; set; }

    public string Cliente { get; set; } = string.Empty;

    public string Mesa { get; set; } = string.Empty;

    public string FormaDePagamento { get; set; } = string.Empty;

    public string Itens { get; set; } = string.Empty;

    public bool ChecklistOk { get; set; }

    public string ChecklistDataHora { get; set; } = string.Empty;
}
