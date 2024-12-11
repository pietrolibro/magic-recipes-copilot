using RecipesCopilot.Ollama.Chat.DTO.Shared;

namespace RecipesCopilot.Ollama.Chat.DTO.Requests;

public class RecipesCopilotRequest
{
    public string prompt { get; set; }
    public Message[] messages { get; set; }
}