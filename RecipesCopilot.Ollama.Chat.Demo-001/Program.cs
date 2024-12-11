
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;

namespace RecipesCopilot.Ollama.Chat.Demo_001;

internal class Program
{
    static async Task Main(string[] args)
    {
        const string OLLAMA_MODEL_ID = "llama3.2:latest";
        const string OLLAMA_ENDPOINT = "http://localhost:11434";

        // Get Semantic Kernel Packages.
        // dotnet add package Microsoft.SemanticKernel --version 1.31.0
        // dotnet add package Microsoft.SemanticKernel.Connectors.Chroma --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Connectors.Ollama --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Plugins.Core --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Plugins.Memory --version 1.31.0-alpha

        //Add AI Services (Experimental).
        #pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, SKEXP0050, SKEXP0070

        //Initialize Semantic Kernel.
        var builder = Kernel.CreateBuilder();

        // Add AI Services (Ollama Connectors).
        HttpClient ollamaHttpClient = new()
        {
            BaseAddress = new Uri(OLLAMA_ENDPOINT)
        };

        builder.AddOllamaChatCompletion(
                    modelId: OLLAMA_MODEL_ID,
                    httpClient: ollamaHttpClient);

        // Add Enterprise Components (e.g. Logging).
        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Build the Kernel.
        var kernel = builder.Build();

        //Create new chat.
        IChatCompletionService ollamaChatService = kernel.GetRequiredService<IChatCompletionService>();

        string systemMessage = @"
            You are an AI assistant that helps people. 
            Please be polite and reply in Italian language.
        ";

        // The chat history object is used to maintain a record of messages in a chat session. 
        // It is used to store messages from different authors, such as users, assistants, tools, or the system. 
        // As the primary mechanism for sending and receiving messages, the chat history object is essential for 
        // maintaining context and continuity in a conversation.
        ChatHistory chatHistory = new ();
        chatHistory.AddSystemMessage(systemMessage);

        string userQuestion="";
        var responseBuilder = new StringBuilder();

        //Chat loop.
        while (true)
        {
            Console.Write("User:> ");
            userQuestion = Console.ReadLine()!;

            if (userQuestion == "/exit") { break; }

            chatHistory.AddUserMessage(userQuestion);

            responseBuilder.Clear();
            Console.WriteLine();

            Console.Write("Copilot: ");
            await foreach (var message in ollamaChatService.GetStreamingChatMessageContentsAsync(chatHistory, null, kernel))
            {
                Console.Write(message);
                responseBuilder.Append(message.Content);
            }

            chatHistory.AddAssistantMessage(responseBuilder.ToString());
            Console.WriteLine();
        }
    }
}