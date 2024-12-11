using System.Text;
using System.Text.Encodings;
using System.Net.Http;
using System.Threading.Tasks;

using Newtonsoft.Json;

using RecipesCopilot.Ollama.Chat.DTO.Responses;

public static class HttpClientExtensions
{
    public static async Task<EmbeddingResponse> GenerateEmbeddingsAsync(this HttpClient httpClient, 
        string model, string url, string input)
    {
        HttpRequestMessage embeddingsHttpRequestMessage = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonConvert.SerializeObject(new 
            { 
                model = model, 
                input = input 
            }), Encoding.UTF8, "application/json")
        };
        
        HttpResponseMessage embeddingsHttpResponseMessage = await httpClient.SendAsync(embeddingsHttpRequestMessage);

        embeddingsHttpResponseMessage.EnsureSuccessStatusCode();

        var embeddingsResponseAsJson = await embeddingsHttpResponseMessage.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<EmbeddingResponse>(embeddingsResponseAsJson);
    }
}