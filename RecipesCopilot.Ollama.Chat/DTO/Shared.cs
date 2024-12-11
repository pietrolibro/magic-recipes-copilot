using System;
using Newtonsoft.Json;

namespace RecipesCopilot.Ollama.Chat.DTO.Shared;

public class Message
{
    [JsonProperty("role")]
    public string Role { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }
}
