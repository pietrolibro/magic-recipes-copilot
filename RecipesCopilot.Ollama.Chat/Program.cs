using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Logging;

using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using RecipesCopilot.Ollama.Chat.Hubs;
using RecipesCopilot.Ollama.Chat.Services;

var builder = WebApplication.CreateBuilder(args);

// Add enterprise components as Logging and Configuration.
builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Debug));

builder.Services.AddHttpClient("Ollama", client =>
{
    // Get the base URL from the configuration from the Environment Variables.
    string baseUrl = Environment.GetEnvironmentVariable("OllamaProxy__BaseUrl") ?? "";

    if (string.IsNullOrEmpty(baseUrl))
    {
        throw new ArgumentNullException("OllamaProxy__BaseUrl");
    }

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new HttpClientHandler()
    {
        ServerCertificateCustomValidationCallback = (HttpRequestMessage msg,
            X509Certificate2 cert, X509Chain chain,
            SslPolicyErrors sslPolicyErrors) => true
    };
});

builder.Services.AddScoped<IVectorRecipesSearchService, VectorRecipesSearchService>();  

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add SignalR services
builder.Services.AddSignalR();

// Add session services
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddMicrosoftIdentityWebAppAuthentication(
    builder.Configuration)
    .EnableTokenAcquisitionToCallDownstreamApi(
        builder.Configuration.GetSection("OllamaProxy:Scopes").Get<string[]>())
    .AddInMemoryTokenCaches();

// The following flag can be used to get more descriptive errors in development environments
// Enable diagnostic logging to help with troubleshooting.  For more details, see https://aka.ms/IdentityModel/PII.
// You might not want to keep this following flag on for production
IdentityModelEventSource.ShowPII = false;

// builder.Services.AddDownstreamApi("OllamaProxy", builder.Configuration.GetSection("OllamaProxy"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    endpoints.MapHub<ChatHub>("/chathub");
});

app.Run();
