using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace RecipesCopilot.Ollama.Chat.Hubs;

    public class ChatHub : Hub
{
    public async Task SendMessage(string sender, string message,bool done)
    {
        await Clients.All.SendAsync("ReceiveMessage", sender, message,done);
    }
}

