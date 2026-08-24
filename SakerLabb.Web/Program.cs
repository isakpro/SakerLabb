using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using SakerLabb.Web.Components;
using SakerLabb.Web.Data;
using SakerLabb.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ImportService>();

builder.Services.AddSingleton<Db>();
builder.Services.AddScoped<TicketRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<FileService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

app.Services.GetRequiredService<Db>().Initialize();

app.UseDeveloperExceptionPage();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; "
        + "img-src 'self' data:; connect-src 'self'; font-src 'self'; "
        + "form-action 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'";
    headers["X-Frame-Options"] = "DENY";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors();

app.UseStaticFiles();
app.UseDirectoryBrowser(new DirectoryBrowserOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.WebRootPath, "files")),
    RequestPath = "/files"
});

app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>();

app.Run();
