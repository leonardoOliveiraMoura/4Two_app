using System.Text.Encodings.Web;
using System.Text.Json;
using C4Two.Models;

namespace C4Two.Services;

public class CadClienteStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _gate = new();
    private readonly string _path;

    public CadClienteStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "src", "mocks", "cadcliente.json");
    }

    public CadCliente Find(string login)
    {
        lock (_gate)
        {
            return ReadAll().FirstOrDefault(item => LoginIgual(item.Login, login)) ?? new CadCliente();
        }
    }

    public bool Exists(string login)
    {
        lock (_gate)
        {
            return ReadAll().Any(item => LoginIgual(item.Login, login));
        }
    }

    public void Add(CadCliente cliente)
    {
        lock (_gate)
        {
            var clientes = ReadAll();
            clientes.Add(cliente);
            WriteAll(clientes);
        }
    }

    public void Update(string loginAtual, CadCliente cliente)
    {
        lock (_gate)
        {
            var clientes = ReadAll();
            var indice = clientes.FindIndex(item => LoginIgual(item.Login, loginAtual));
            if (indice < 0)
            {
                throw new IOException("Cliente não encontrado.");
            }

            clientes[indice] = cliente;
            WriteAll(clientes);
        }
    }

    private List<CadCliente> ReadAll()
    {
        if (!File.Exists(_path))
        {
            return new List<CadCliente>();
        }

        var json = File.ReadAllText(_path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<CadCliente>();
        }

        using var documento = JsonDocument.Parse(json);
        if (documento.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<CadCliente>>(json, JsonOptions) ?? new List<CadCliente>();
        }

        var unico = JsonSerializer.Deserialize<CadCliente>(json, JsonOptions);
        return unico is null ? new List<CadCliente>() : new List<CadCliente> { unico };
    }

    private void WriteAll(List<CadCliente> clientes)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(clientes, JsonOptions);
        File.WriteAllText(_path, json + Environment.NewLine);
    }

    private static bool LoginIgual(string armazenado, string informado)
    {
        return string.Equals(armazenado.Trim(), informado.Trim(), StringComparison.Ordinal);
    }
}
