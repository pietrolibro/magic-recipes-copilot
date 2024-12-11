
using Microsoft.Identity.Web;
using Microsoft.Identity.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.AspNetCore.SignalR;

using System.Text;
using System.Text.Json.Nodes;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

using Qdrant.Client;
using Qdrant.Client.Grpc;

using RecipesCopilot.Ollama.Chat.Hubs;
using RecipesCopilot.Ollama.Chat.Models;
using RecipesCopilot.Ollama.Chat.Helpers;
using RecipesCopilot.Ollama.Chat.Services;

using RecipesCopilot.Ollama.Chat.DTO;
using RecipesCopilot.Ollama.Chat.DTO.Shared;
using RecipesCopilot.Ollama.Chat.DTO.Requests;
using RecipesCopilot.Ollama.Chat.DTO.Responses;


namespace RecipesCopilot.Ollama.Chat.Controllers;

/// <summary>
/// 
/// </summary>
[AuthorizeForScopes(ScopeKeySection = "OllamaProxy:Scopes")]
public class HomeController : Controller
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IVectorRecipesSearchService _vectorRecipesSearchService;

    public HomeController(IHubContext<ChatHub> hubContext, IHttpClientFactory httpClientFactory,
        IConfiguration configuration, ITokenAcquisition tokenAcquisition,
        ILogger<HomeController> logger, IVectorRecipesSearchService vectorRecipesSearchService)
    {
        _logger = logger;
        _hubContext = hubContext;
        _configuration = configuration;
        _tokenAcquisition = tokenAcquisition;
        _vectorRecipesSearchService = vectorRecipesSearchService;

        _httpClient = httpClientFactory.CreateClient("Ollama");
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Credits()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(string userMessage)
    {
        // Send user's message to the clients
        await _hubContext.Clients.All.SendAsync("ReceiveMessage", "User", userMessage, true);

        // Start streaming response from middleware
        await foreach (var (llmResponse, done) in GetLLMStreamingResponseAsync(userMessage))
        {
            // Send each chunk to the clients
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", "LLM", llmResponse, done);
        }

        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearChat()
    {
        // Remove the chat history from session
        HttpContext.Session.Remove("ChatHistory");

        return Ok();
    }

    /// <summary>
    /// Stream the response from the LLM middleware.
    /// </summary>
    /// <param name="userMessage"> </param>
    /// <returns></returns>
    private async IAsyncEnumerable<(string Content, bool Done)> GetLLMStreamingResponseAsync(string userQuestion)
    {
        var chatHistory = HttpContext.Session.GetObjectFromJson<List<Message>>("ChatHistory");

        #region Make chat history is not null.

        if (chatHistory == null)
        {
            chatHistory = new List<Message>();

            string systemMessage = @"
                You are a helpful AI assistant specializing in Italian recipes for Christmas. All responses need to be in Italian.

                Ask for more information as preferences, number of guests or special ingredients until a recipe is suggested.

                For each recipe, provide the following information in this order:

                - Name of the recipe in uppercase
                - List of ingredients (as a numbered list)
                - Preparation steps (as a numbered list)
                - Cooking time
                - Calories per portion

                If the user asks for another recipe, suggest a different recipe that fits the criteria. 

                Each piece of information should be separated by a new line.
                ";

            chatHistory.Add(new Message { Role = "system", Content = systemMessage });
        }

        #endregion

        var scopes = _configuration.GetSection("OllamaProxy:Scopes").Get<string[]>();
        var token = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

        // Set the Authorization header with the access token.
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Call the OllamaProxy\API to get the embeddings.
        string url = $"{Environment.GetEnvironmentVariable("OllamaProxy__BaseUrl")}/api/embed";

        EmbeddingResponse? embeddingResponse = await _httpClient.GenerateEmbeddingsAsync(
            _configuration.GetValue<string>("OllamaProxy:EmbeddingModel"), url, userQuestion);

        if (embeddingResponse == null)
        {
            throw new Exception("Embedding response is null.");
        }

        Recipe suggestedRecipe = await _vectorRecipesSearchService.QueryAsync(embeddingResponse, _logger, 0.6f);

        int contextToRemove = -1;

        if (suggestedRecipe != null)
        {
            StringBuilder contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("Here's some additional information: ");
            contextBuilder.AppendLine("Recipe Title:");
            contextBuilder.AppendLine(suggestedRecipe.Payload.RecipeTitle.Value);
            contextBuilder.AppendLine("Recipe Description:");
            contextBuilder.AppendLine(suggestedRecipe.Payload.RecipeDescription.Value);
            contextBuilder.AppendLine("Recipe Ingredients:");
            contextBuilder.AppendLine(suggestedRecipe.Payload.RecipeIngredients.Value);
            contextBuilder.AppendLine("Recipe Procedure:");
            contextBuilder.AppendLine(suggestedRecipe.Payload.RecipeProcedure.Value);

            contextToRemove = chatHistory.Count;

            chatHistory.Add(new Message { Role = "user", Content = contextBuilder.ToString() });

            _logger.LogInformation("==============================================");
            _logger.LogInformation("RAG Context:");
            _logger.LogInformation(contextBuilder.ToString());
            _logger.LogInformation("==============================================");
        }
        
        // Add user's message to chat history
        chatHistory.Add(new Message { Role = "user", Content = userQuestion });

        // Call the OllamaProxy\API to get the response.
        url = $"{Environment.GetEnvironmentVariable("OllamaProxy__BaseUrl")}/api/chat";

        RecipesCopilotRequest recipesCopilotRequest = new()
        {
            prompt = userQuestion,
            messages = chatHistory.ToArray()
        };

        string recipesCopilotRequestAsJson = JsonConvert.SerializeObject(recipesCopilotRequest);
        var recipesCopilotRequestAsContent = new StringContent(recipesCopilotRequestAsJson, Encoding.UTF8, "application/json");

        HttpRequestMessage chatHttpRequestMessage = new HttpRequestMessage(HttpMethod.Post, url) { Content = recipesCopilotRequestAsContent };
        HttpResponseMessage chatHttpResponseMessage = await _httpClient.SendAsync(chatHttpRequestMessage, HttpCompletionOption.ResponseHeadersRead);

        chatHttpResponseMessage.EnsureSuccessStatusCode();

        var stream = await chatHttpResponseMessage.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        StringBuilder llmResponse = new StringBuilder();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();

            if (!string.IsNullOrWhiteSpace(line))
            {
                // Parse the JSON line.
                var jsonResponse = JsonConvert.DeserializeObject<StreamingResponse>(line);

                // Extract the content and done flag from the message.
                var contentPart = jsonResponse.Message.Content;
                var done = jsonResponse.Done;

                if (!string.IsNullOrEmpty(contentPart))
                {
                    llmResponse.Append(contentPart);
                    yield return (contentPart, done);
                }

                if (done)
                {
                    break;  // Stop streaming                
                }
            }
        }

        // Add LLM's response to chat history
        chatHistory.Add(new Message
        {
            Role = "assistant",
            Content = llmResponse.ToString(),
        });

        if (contextToRemove >= 0)
        {
            _logger.LogInformation("==============================================");
            _logger.LogInformation("RAG Context to remove:");
            _logger.LogInformation(chatHistory[contextToRemove].Content);
            chatHistory.RemoveAt(contextToRemove);
            _logger.LogInformation(contextToRemove.ToString());
            _logger.LogInformation("==============================================");
        }

        // Save updated chat history in session
        HttpContext.Session.SetObjectAsJson("ChatHistory", chatHistory);
    }

    [HttpPost]
    public IActionResult Login()
    {
        // Redirect the user to Azure AD for login
        return Challenge(new AuthenticationProperties { RedirectUri = "/" },
        OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        // Sign the user out of OpenID Connect and clear cookies
        await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index");
    }
}