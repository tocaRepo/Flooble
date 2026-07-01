using Flooble.Core;
using Flooble.Web.Components;
using Flooble.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var cliConfigurationPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "Flooble.Cli", "configuration.json"));
builder.Configuration.AddJsonFile(cliConfigurationPath, optional: true, reloadOnChange: true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<AgentChatService>();
builder.Services.AddScoped<ChatSessionState>();
builder.Services.AddScoped<FloobleRuntime>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
