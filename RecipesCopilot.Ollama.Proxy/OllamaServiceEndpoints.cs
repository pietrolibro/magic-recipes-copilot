
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;

using System.Text;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Identity.Web;
using Microsoft.Identity.Web.Resource;

using Microsoft.AspNetCore.Authorization;

public static class OllamaProxyEndpoints
{
    public static void RegisterOllamaProxyApiEndpoints(this IEndpointRouteBuilder builder, IConfiguration configuration)
    {
        /// <summary>
        /// Get the list of tags from the Ollama service.
        /// </summary>
        builder.MapGet("/api/tags",
        [Authorize(Policy = "chat")]
        async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
        {
            var ollamaAddress= configuration["Ollama:BaseUrl"];
            var ollamaHttpClient = httpClientFactory.CreateClient("OllamaClient");

            var ollamaVersionResponse = await ollamaHttpClient.GetAsync($"{ollamaAddress}/api/tags");
            var ollamaVersionContent = await ollamaVersionResponse.Content.ReadAsStringAsync();

            // Set the response content type to application/json
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsync(ollamaVersionContent);
        }).WithName("Tags")
        .RequireAuthorization("chat");

        /// <summary>
        /// Get the version of the Ollama service.
        /// </summary>
        builder.MapGet("/api/version", async (HttpContext httpContext, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
        {
            var ollamaAddress= configuration["Ollama:BaseUrl"];
            var ollamaHttpClient = httpClientFactory.CreateClient("OllamaClient");

            var ollamaVersionResponse = await ollamaHttpClient.GetAsync($"{ollamaAddress}/api/version");
            var ollamaVersionContent = await ollamaVersionResponse.Content.ReadAsStringAsync();

            // Set the response content type to application/json
            httpContext.Response.ContentType = "application/json";

            await httpContext.Response.WriteAsync(ollamaVersionContent);
        }).WithName("Version")
        .RequireAuthorization("chat");

        /// <summary>
        /// Generate a chat message content from the Ollama service.
        /// </summar>
        builder.MapPost("/api/generate", async (HttpContext httpContext, IOllamaService ollamaService) =>
        {
            string requestBody = string.Empty;
            CancellationToken cancellationToken = new();

            using (var streamReader = new StreamReader(httpContext.Request.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            var request = JsonSerializer.Deserialize<GenerateRequest>(requestBody);

            if (!request.stream)
            {
                httpContext.Response.ContentType = "application/json";

                var response = await ollamaService.GetChatMessageContentAsync(request.prompt, cancellationToken);

                var sseMessage = $"{JsonSerializer.Serialize(response)}\n";
                await httpContext.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseMessage));
                await httpContext.Response.Body.FlushAsync();
            }

            if (request.stream)
            {
                httpContext.Response.Headers.Add("Content-Type", "text/event-stream");

                await foreach (var response in ollamaService.GetStreamingChatMessageContentsAsync(request.prompt, cancellationToken))
                {
                    var sseMessage = $"{JsonSerializer.Serialize(response)}\n";
                    await httpContext.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseMessage));
                    await httpContext.Response.Body.FlushAsync();
                }
            }
        }).WithName("Generate")
        .RequireAuthorization("chat");

        /// <summary>
        /// Get a streaming chat message content from the Ollama service.
        /// <summary>
        builder.MapPost("/api/chat", async Task (HttpContext httpContext, IOllamaService ollamaService, CancellationToken ct) =>
        {
            httpContext.Response.Headers.Add("Content-Type", "text/event-stream");

            var requestBody = string.Empty;
            using (var streamReader = new StreamReader(httpContext.Request.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            var request = JsonSerializer.Deserialize<ChatRequest>(requestBody);

            await foreach (var response in ollamaService.GetStreamingChatMessageContentsAsync(request))
            {
                var sseMessage = $"{JsonSerializer.Serialize(response)}\n";
                await httpContext.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseMessage));
                await httpContext.Response.Body.FlushAsync();
            }

        }).WithName("ChatCompletion")
        .RequireAuthorization("chat");

        /// <summary>
        /// Generate embeddings from the Ollama service.
        /// </summary>
        builder.MapPost("/api/embed", async (HttpContext context, IOllamaService ollamaService, CancellationToken ct) =>
        {
            var requestBody = string.Empty;
            using (var streamReader = new StreamReader(context.Request.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            // Set the response content type to application/json
            context.Response.ContentType = "application/json";

            var request = JsonSerializer.Deserialize<GenerateEmbedRequest>(requestBody);
            var response = await ollamaService.GenerateEmbedAsync(request);

            return new
            {
                model = request.model,
                embeddings = response
            };

        }).WithName("Embed")
        .RequireAuthorization("chat");

        /// <summary>
        /// Generate embeddings from the Ollama service.
        /// </summary>
        builder.MapPost("/api/embeddings", async (HttpContext context, IOllamaService ollamaService, CancellationToken ct) =>
        {
            var requestBody = string.Empty;
            using (var streamReader = new StreamReader(context.Request.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            // Set the response content type to application/json
            context.Response.ContentType = "application/json";

            var request = JsonSerializer.Deserialize<GenerateEmbeddingRequest>(requestBody);

            var response = await ollamaService.GenerateEmbeddingsAsync(request);

            Console.WriteLine(JsonSerializer.Serialize(response));

            return new
            {
                embeddings = response.FirstOrDefault()
            };

        }).WithName("Embeddings")
        .RequireAuthorization("chat");
    }
}