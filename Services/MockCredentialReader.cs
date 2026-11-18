using System.Text.Encodings.Web;
using System.Text.Json;

namespace C4Two.Services;

public class MockCredentialReader
{
    private static readonly HashSet<string> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "gerenciamento.json",
        "cliente.json"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _gate = new();
    private readonly IWebHostEnvironment _environment;

    public MockCredentialReader(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public bool TryValidate(string fileName, string login, string senha, out string error)
    {
        if (!TryRead(fileName, out var credentials, out error))
        {
            return false;
        }

        foreach (var credential in credentials)
        {
            if (string.IsNullOrWhiteSpace(credential.Login))
            {
                continue;
            }

            if (!string.Equals(credential.Login.Trim(), login.Trim(), StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(credential.Senha, senha, StringComparison.Ordinal))
            {
                error = string.Empty;
                return true;
            }

            error = "Login ou senha inválidos.";
            return false;
        }

        error = "Login ou senha inválidos.";
        return false;
    }

    public bool Exists(string fileName, string login)
    {
        return TryRead(fileName, out var credentials, out _)
            && credentials.Any(item => string.Equals(item.Login.Trim(), login.Trim(), StringComparison.Ordinal));
    }

    public void Add(string fileName, string login, string senha)
    {
        lock (_gate)
        {
            if (!TryRead(fileName, out var credentials, out var error))
            {
                throw new IOException(error);
            }

            credentials.Add(new MockCredential
            {
                Login = login.Trim(),
                Senha = senha
            });
            Write(fileName, credentials);
        }
    }

    public void Update(string fileName, string loginAtual, string login, string senha)
    {
        lock (_gate)
        {
            if (!TryRead(fileName, out var credentials, out var error))
            {
                throw new IOException(error);
            }

            var indice = credentials.FindIndex(item =>
                string.Equals(item.Login.Trim(), loginAtual.Trim(), StringComparison.Ordinal));
            if (indice < 0)
            {
                throw new IOException("Login não encontrado.");
            }

            credentials[indice] = new MockCredential
            {
                Login = login.Trim(),
                Senha = senha
            };
            Write(fileName, credentials);
        }
    }

    private bool TryRead(string fileName, out List<MockCredential> credentials, out string error)
    {
        credentials = new List<MockCredential>();
        if (!AllowedFiles.Contains(fileName))
        {
            error = "Não foi possível ler os dados de acesso.";
            return false;
        }

        var path = Path.Combine(_environment.ContentRootPath, "src", "mocks", fileName);
        if (!File.Exists(path))
        {
            error = "Não foi possível ler os dados de acesso.";
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind == JsonValueKind.Array)
            {
                credentials = JsonSerializer.Deserialize<List<MockCredential>>(json, JsonOptions) ?? new List<MockCredential>();
            }
            else
            {
                var unico = JsonSerializer.Deserialize<MockCredential>(json, JsonOptions);
                if (unico is not null)
                {
                    credentials.Add(unico);
                }
            }

            if (credentials.Count == 0
                || credentials.All(item => string.IsNullOrWhiteSpace(item.Login) || string.IsNullOrWhiteSpace(item.Senha)))
            {
                error = "Não foi possível ler os dados de acesso.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (JsonException)
        {
            error = "Não foi possível ler os dados de acesso.";
            return false;
        }
        catch (IOException)
        {
            error = "Não foi possível ler os dados de acesso.";
            return false;
        }
    }

    private void Write(string fileName, List<MockCredential> credentials)
    {
        var path = Path.Combine(_environment.ContentRootPath, "src", "mocks", fileName);
        var json = JsonSerializer.Serialize(credentials, JsonOptions);
        File.WriteAllText(path, json + Environment.NewLine);
    }

    private sealed class MockCredential
    {
        public string Login { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
    }
}
