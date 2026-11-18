namespace C4Two.Models;

public class SessaoMesaCliente
{
    public Guid Guid { get; set; }

    public SessaoEstabelecimento SessaoEstabelecimento { get; set; } = new();

    public MesaItem Mesa { get; set; } = new();

    public ClienteSessao Cliente { get; set; } = new();

    public CapacidadeAtualStatus CapacidadeAtualStatus { get; set; } = new();
}

public class CapacidadeAtualStatus
{
    public int Id { get; set; }

    public string CapacidadeAtual { get; set; } = "1";
}

public class ClienteSessao
{
    public string Login { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string Cpf { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Telefone { get; set; } = string.Empty;

    public string Endereco { get; set; } = string.Empty;

    public string Cidade { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public string Cep { get; set; } = string.Empty;

    public string Pais { get; set; } = string.Empty;
}
