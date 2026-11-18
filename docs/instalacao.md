# Instalação e execução do 4two

O 4two é uma aplicação web ASP.NET Core. A interface usa views Razor e o CSS é gerado com Tailwind. Os dados de demonstração ficam em arquivos JSON em `src/mocks`. Não há banco de dados para instalar.

## Tecnologias

| Peça | Uso no aplicativo |
| --- | --- |
| .NET 9 (`net9.0`) | Runtime e SDK do projeto `C4Two.csproj` |
| ASP.NET Core MVC | Controllers, views Razor, sessão e rotas |
| Razor | Telas de gerenciamento, sessão do estabelecimento, conta, pagamento e avaliação |
| Sessão em memória | Login de gerenciamento e login de cliente |
| JSON em `src/mocks` | Cadastros, sessões, consumo, pagamentos, códigos de saída e avaliações |
| QRCoder 1.8.1 | QR code da mesa e QR code do PIX copia e cola |
| Node.js e npm | Ferramentas para gerar o CSS |
| Tailwind CSS 3.4 | Estilo das telas, a partir de `src/input.css` |
| PostCSS 8 e Autoprefixer 10 | Processamento do CSS no build do Tailwind |
| Bootstrap, jQuery e jQuery Validation | Bibliotecas estáticas em `wwwroot/lib` |

Endereços locais definidos em `Properties/launchSettings.json`:

- HTTP: `http://localhost:5001`
- HTTPS: `https://localhost:7217` e `http://localhost:5001`

## O que instalar antes

1. [.NET SDK 9](https://dotnet.microsoft.com/download/dotnet/9.0)
2. [Node.js](https://nodejs.org/) com npm, para o comando de CSS

Confira as versões no terminal, na pasta do projeto:

```powershell
dotnet --version
node --version
npm --version
```

O `dotnet --version` precisa ser da linha 9.

## Sequência de comandos

Execute na raiz do projeto, a pasta que contém `C4Two.csproj` e `package.json`.

1. Restaurar os pacotes .NET, inclusive o QRCoder:

```powershell
dotnet restore
```

2. Instalar Tailwind, PostCSS e Autoprefixer:

```powershell
npm install
```

3. Gerar o CSS usado pelas telas:

```powershell
npm run build:css
```

Esse comando lê `src/input.css` e grava `wwwroot/css/tailwind.css`.

4. Subir a aplicação em HTTPS e HTTP:

```powershell
dotnet run --launch-profile https
```

Para usar só HTTP:

```powershell
dotnet run --launch-profile http
```

5. Abrir no navegador:

- `https://localhost:7217/main`
- ou `http://localhost:5001/main`

Na primeira execução em HTTPS, o certificado de desenvolvimento do .NET pode pedir confiança. Se o navegador bloquear o endereço, rode uma vez:

```powershell
dotnet dev-certs https --trust
```

## Durante o desenvolvimento do CSS

Para recompilar o Tailwind sempre que uma classe mudar:

```powershell
npm run watch:css
```

Deixe esse comando em um terminal e o `dotnet run` em outro.

## Entrar no aplicativo

A tela inicial oferece **Gerenciamento** e **Cliente**. Os logins de demonstração estão nos mocks:

| Caminho | Arquivo | Login | Senha |
| --- | --- | --- | --- |
| Gerenciamento | `src/mocks/gerenciamento.json` | gerenciamento | gerencia123 |
| Cliente | `src/mocks/cliente.json` | cliente | cliente123 |
| Cliente | `src/mocks/cliente.json` | cliente2 | cliente244 |
| Cliente | `src/mocks/cliente.json` | cliente3 | cliente3 |

Dois navegadores, ou uma janela normal e uma anônima, permitem sentar dois clientes na mesma mesa ao mesmo tempo. O gerenciamento fica em uma terceira janela.

## Parar a aplicação

No terminal em que o `dotnet run` está ativo, use `Ctrl+C`.
