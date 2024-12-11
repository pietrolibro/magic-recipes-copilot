using System;
using Newtonsoft.Json;

using RecipesCopilot.Ollama.Chat.DTO.Shared;

namespace RecipesCopilot.Ollama.Chat.DTO.Responses;

public class StreamingResponse
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("message")]
    public Message Message { get; set; }

    [JsonProperty("done")]
    public bool Done { get; set; }
}


public class EmbeddingResponse
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("embeddings")]
    public float [][] Embeddings { get; set; }

    [JsonProperty("total_duration")]
    public long TotalDuration { get; set; }

    [JsonProperty("load_duration")]
    public long LoadDuration { get; set; }

    [JsonProperty("prompt_eval_count")]
    public int PromptEvalCount { get; set; }
}