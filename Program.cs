using C4Two.Filters;
using C4Two.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = LoginSession.CookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AvaliacaoSessaoFilter());
});
builder.Services.AddSingleton<MockCredentialReader>();
builder.Services.AddSingleton<CadClienteStore>();
builder.Services.AddSingleton<CadGerenciamentoStore>();
builder.Services.AddSingleton<SessaoEstabelecimentoStore>();
builder.Services.AddSingleton<SessaoMesaClienteStore>();
builder.Services.AddSingleton<MesaConsumoStore>();
builder.Services.AddSingleton<CompartilhamentoConsumoStore>();
builder.Services.AddSingleton<VisualizarContaStore>();
builder.Services.AddSingleton<FormaDePagamentoStore>();
builder.Services.AddSingleton<ValidacaoPagamentoStore>();
builder.Services.AddSingleton<CodigoLiberacaoStore>();
builder.Services.AddSingleton<AvaliacaoClienteStore>();
builder.Services.AddSingleton<MesaStatusService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/src/mocks"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

var srcPath = Path.Combine(builder.Environment.ContentRootPath, "src");
if (Directory.Exists(srcPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(srcPath),
        RequestPath = "/src"
    });
}

app.UseRouting();
app.UseSession();

app.MapControllers();

app.Run();
