# pragma warning disable SKEXP0001
# pragma warning disable SKEXP0020
# pragma warning disable SKEXP0050
# pragma warning disable SKEXP0070


using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.SemanticKernel.Connectors.Ollama;

using Microsoft.AspNetCore.Authentication.JwtBearer;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;

// Authentication & Authorization namespaces.
using Microsoft.Identity.Web;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Logging;

var builder = WebApplication.CreateBuilder(args);

// This is required to be instantiated before the OpenIdConnectOptions starts getting configured.
// By default, the claims mapping will map claim names in the old format to accommodate older SAML applications.
// 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role' instead of 'roles'
// This flag ensures that the ClaimsIdentity claims collection will be built from the claims in the token
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

// Adds Microsoft Identity platform (AAD v2.0) support to protect this Api
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IOllamaService, OllamaService>();

Uri ollammaEndpoint = new Uri(builder.Configuration["Ollama:BaseUrl"]);
string ollamaModelId = builder.Configuration["Ollama:ModelId"];
string ollamaEmbeddingModelId = builder.Configuration["Ollama:EmbeddingModelId"];

// Custom HttpClientHandler to ignore SSL certificate validation
HttpClientHandler handler = new HttpClientHandler()
{
    ServerCertificateCustomValidationCallback = (HttpRequestMessage msg,
        X509Certificate2 cert, X509Chain chain,
        SslPolicyErrors sslPolicyErrors) => true
};

HttpClient ollamaHttpClient = new HttpClient(handler);
ollamaHttpClient.BaseAddress = ollammaEndpoint;

builder.Services.AddScoped<IOllamaService, OllamaService>();

builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = ollammaEndpoint;
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

builder.Services.AddAuthorization(config =>
{
    config.AddPolicy("chat", policyBuilder =>
        policyBuilder.Requirements.Add(new 
        ScopeAuthorizationRequirement() { 
            RequiredScopesConfigurationKey = $"AzureAd:Scopes" }));
});

// The following flag can be used to get more descriptive errors in development environments
// Enable diagnostic logging to help with troubleshooting.  For more details, see https://aka.ms/IdentityModel/PII.
// You might not want to keep this following flag on for production
IdentityModelEventSource.ShowPII = false;

// Install Microsoft.SemanticKernel NuGet package and add Kernel to the Dependency Injection.
builder.Services
    .AddKernel()
    .AddOllamaChatCompletion(
        modelId: ollamaModelId,
        httpClient: ollamaHttpClient)
    .AddOllamaTextEmbeddingGeneration(
        modelId: ollamaEmbeddingModelId,
        httpClient: ollamaHttpClient);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable Authentication and Authorization capabilities.
app.UseAuthentication();
app.UseAuthorization();

// Register the Ollama Proxy API endpoints.
app.RegisterOllamaProxyApiEndpoints(builder.Configuration);

app.Run();