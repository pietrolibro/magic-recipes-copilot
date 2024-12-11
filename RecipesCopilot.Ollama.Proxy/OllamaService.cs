# pragma warning disable SKEXP0001
# pragma warning disable SKEXP0020
# pragma warning disable SKEXP0050
# pragma warning disable SKEXP0070

using System.Text;
using System.Text.Json;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.SemanticKernel.Connectors.Ollama;

using OllamaSharp;
using OllamaSharp.Models.Chat;

public interface IOllamaService
{
    Task<IList<ReadOnlyMemory<float>>> GenerateEmbedAsync(GenerateEmbedRequest request);
    Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(GenerateEmbeddingRequest request);
    Task<ChatMessageContent> GetChatMessageContentAsync(string prompt,CancellationToken cancellationToken);
    IAsyncEnumerable<ChatMessageContentResponse> GetStreamingChatMessageContentsAsync(ChatRequest request);
    IAsyncEnumerable<ChatMessageContentResponse> GetStreamingChatMessageContentsAsync(string prompt, CancellationToken cancellationToken);
}

public class OllamaService : IOllamaService
{
    private Kernel _kernel;
    private IChatCompletionService _chatCompletionService;
    private ITextEmbeddingGenerationService _textEmbeddingService;

    public OllamaService(Kernel kernel)
    {
        _kernel = kernel;
        _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        _textEmbeddingService = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
    }

    /// <summary>
    /// Get a single chat message content for the prompt and settings.
    /// </summary>
    public async Task<ChatMessageContent> GetChatMessageContentAsync(string prompt, CancellationToken cancellationToken )
    {
        // Useful links: 
        // - https://github.com/microsoft/semantic-kernel/tree/main/dotnet/src/SemanticKernel.Abstractions/AI/ChatCompletion
        // - https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/ChatCompletion/Ollama_ChatCompletion.cs#L40
        ChatMessageContent chatMessageContent = await _chatCompletionService.GetChatMessageContentAsync(prompt,
           cancellationToken: cancellationToken);

        // Log in the console the ChatMessageContent as json string.
        var json = JsonSerializer.Serialize(chatMessageContent);
        Console.WriteLine(json);

        return chatMessageContent;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="prompt"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<ChatMessageContentResponse> GetStreamingChatMessageContentsAsync(string prompt, CancellationToken cancellationToken)
    {
        // Useful links:
        // - https://learn.microsoft.com/en-us/dotnet/api/microsoft.semantickernel.chatcompletion.chatcompletionserviceextensions.getstreamingchatmessagecontentsasync?view=semantic-kernel-dotnet#microsoft-semantickernel-chatcompletion-chatcompletionserviceextensions-getstreamingchatmessagecontentsasync(microsoft-semantickernel-chatcompletion-ichatcompletionservice-system-string-microsoft-semantickernel-promptexecutionsettings-microsoft-semantickernel-kernel-system-threading-cancellationtoken)

        IAsyncEnumerable<StreamingChatMessageContent> chatMessagesStream = _chatCompletionService.GetStreamingChatMessageContentsAsync(
            prompt,cancellationToken: cancellationToken );

        await foreach (var chatMessage in chatMessagesStream)
        {
            var innerContent = chatMessage.InnerContent as ChatResponseStream;

            // OutputInnerContent(innerContent);

            yield return new ChatMessageContentResponse()
            {
                model = innerContent?.Model,
                created_at = innerContent?.CreatedAt,
                message = new Message()
                {
                    role = innerContent?.Message.Role?.ToString(),
                    content = innerContent?.Message.Content?.ToString()
                },
                done = innerContent.Done
            };
        }
    }

    /// <summary>
    /// Get streaming chat contents for the chat history provided using the specified settings.
    /// </summary>
    public async IAsyncEnumerable<ChatMessageContentResponse> GetStreamingChatMessageContentsAsync(ChatRequest request)
    {
        var chatHistory = PrepareChatHistory(request);
        // Useful links: 
        // - https://github.com/microsoft/semantic-kernel/tree/main/dotnet/src/SemanticKernel.Abstractions/AI/ChatCompletion
        // - https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/ChatCompletion/Ollama_ChatCompletionStreaming.cs
        IAsyncEnumerable<StreamingChatMessageContent> chatMessagesStream = _chatCompletionService
            .GetStreamingChatMessageContentsAsync(chatHistory, kernel: _kernel);

        await foreach (var chatMessage in chatMessagesStream)
        {
            var innerContent = chatMessage.InnerContent as ChatResponseStream;

            // OutputInnerContent(innerContent);

            yield return new ChatMessageContentResponse()
            {
                model = innerContent?.Model,
                created_at = innerContent?.CreatedAt,
                message = new Message()
                {
                    role = innerContent?.Message.Role?.ToString(),
                    content = innerContent?.Message.Content?.ToString()
                },
                done = innerContent.Done
            };
        }
    }

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbedAsync(GenerateEmbedRequest request)
    {
        IList<ReadOnlyMemory<float>> embeddings = await _textEmbeddingService.GenerateEmbeddingsAsync(new string[] { request.input });
        return embeddings;
    }

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(GenerateEmbeddingRequest request)
    {
        IList<ReadOnlyMemory<float>> embeddings = await _textEmbeddingService.GenerateEmbeddingsAsync(new string[] { request.prompt });
        return embeddings;
    }

    // Output the inner content of the chat message.
    // Useful links:
    // - https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/ChatCompletion/Ollama_ChatCompletionStreaming.cs#L250
    private void OutputInnerContent(ChatResponseStream streamChunk)
    {
        Console.WriteLine($"Model: {streamChunk.Model}");
        Console.WriteLine($"Message role: {streamChunk.Message.Role}");
        Console.WriteLine($"Message content: {streamChunk.Message.Content}");
        Console.WriteLine($"Created at: {streamChunk.CreatedAt}");
        Console.WriteLine($"Done: {streamChunk.Done}");

        /// The last message in the chunk is a <see cref="ChatDoneResponseStream"/> type with additional metadata.
        if (streamChunk is ChatDoneResponseStream doneStream)
        {
            Console.WriteLine($"Done Reason: {doneStream.DoneReason}");
            Console.WriteLine($"Eval count: {doneStream.EvalCount}");
            Console.WriteLine($"Eval duration: {doneStream.EvalDuration}");
            Console.WriteLine($"Load duration: {doneStream.LoadDuration}");
            Console.WriteLine($"Total duration: {doneStream.TotalDuration}");
            Console.WriteLine($"Prompt eval count: {doneStream.PromptEvalCount}");
            Console.WriteLine($"Prompt eval duration: {doneStream.PromptEvalDuration}");
        }
        Console.WriteLine("------------------------");
    }

    private ChatHistory PrepareChatHistory(ChatRequest request)
    {
        ChatHistory chat = new ChatHistory();

        if (request?.messages != null)
        {
            foreach (var item in request.messages)
            {
                var role = item.role == "user" ? AuthorRole.User :
                           item.role == "system" ? AuthorRole.System :
                           AuthorRole.Assistant;

                chat.AddMessage(role, item.content);

                Console.WriteLine($"Role {role}, Message: {item.content}");
            }
        }
        return chat;
    }
}