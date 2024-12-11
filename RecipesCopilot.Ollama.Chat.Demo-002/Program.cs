#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, SKEXP0050, SKEXP0070

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Text;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.VectorData;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.InMemory;

// Logging.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace RecipesCopilot.Ollama.Chat.Demo_002;

internal class Program
{
    static async Task Main(string[] args)
    {
        const string OLLAMA_MODEL_ID = "llama3.2:latest";
        const string OLLAMA_ENDPOINT = "http://localhost:11434";
        const string OLLAMA_EMBEDDING_MODEL_ID = "nomic-embed-text:latest";

        // Get Packages.
        // dotnet add package Microsoft.Extensions.Configuration --version 9.0.0
        // dotnet add package Microsoft.Extensions.Logging.Console --version 9.0.0
        // dotnet add package Microsoft.SemanticKernel --version 1.31.0
        // dotnet add package Microsoft.SemanticKernel.Connectors.Ollama --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Plugins.Core --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Plugins.Memory --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Plugins.Core --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Connectors.InMemory --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Plugins.Web --version 1.31.0-alpha

        //Initialize Semantic Kernel
        var builder = Kernel.CreateBuilder();

        // Add AI Services (Ollama Connectors).
        HttpClient ollamaHttpClient = new()
        {
            BaseAddress = new Uri(OLLAMA_ENDPOINT)
        };

        builder.AddOllamaChatCompletion(
                    modelId: OLLAMA_MODEL_ID,
                    httpClient: ollamaHttpClient)
                .AddOllamaTextEmbeddingGeneration(
                    modelId: OLLAMA_EMBEDDING_MODEL_ID,
                    httpClient: ollamaHttpClient);

        // Add Enterprise Components (e.g. Logging).
        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Build the Kernel.
        var kernel = builder.Build();

        ILogger logger = kernel.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Kernel is ready.");

        // Add Bing Search.
        # region Bing Search.

        var bingTextSearch = new BingTextSearch(apiKey: Environment.GetEnvironmentVariable("BING_SEARCH_API_KEY")!);

        #endregion

        // Add Memory, InMemory Vector Store.
        var vectorStore = new InMemoryVectorStore();

        // Get and create collection if it doesn't exist.
        var collection = vectorStore.GetCollection<System.Guid, PersonalRecord>("personal_records");
        await collection.CreateCollectionIfNotExistsAsync();
        logger.LogInformation("Collection is ready.");

        // Get some personal records.
        IList<PersonalRecord> personalEntries = PersonalRecordDB.GetCollection();

        var ollamaEmbeddingGeneratorService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

        // RAG => Chunking + Embedding Generation.
        logger.LogInformation("Starting RAG Chunking and Embedding Generation.");

        // Using the Text Chunker to chunk the text into smaller pieces and generate embeddings for each chunk.        
        foreach (var entry in personalEntries)
        {
            // 10 = Max Tokens per line.
            List<string> splittedLines = TextChunker.SplitPlainTextLines(entry.RecordContent, 10);

            foreach (var line in splittedLines)
            {
                logger.LogInformation($"");
                logger.LogInformation($"Generating -----------------------------------");
                logger.LogInformation($"Generating embedding for: {line}");
                entry.Vector = await ollamaEmbeddingGeneratorService.GenerateEmbeddingAsync(line);
                //show the embedding as numbers.
                logger.LogInformation($"Generated embedding for: {string.Join(",", entry.Vector.ToArray())}");
                logger.LogInformation($"Generating -----------------------------------");
            }

            // Upsert the Personal entries into the collection and return their keys.
            var keys = await collection.UpsertAsync(entry);
            logger.LogInformation($" -----------------------------------");
            logger.LogInformation($" Upserted key: {keys}");
            logger.LogInformation($" -----------------------------------");
        }

        // "You are an AI assistant that helps people find delicious Christmas recipes."
        string systemMessage = @"
            You are an AI assistant that helps people find information.
            Please be polite and reply in Italian language. 
            Just reply the question nothing else. If you don't know the answer, just say 'I don't know'.
            ";

        //Create new chat
        IChatCompletionService ollamaChatService = kernel.GetRequiredService<IChatCompletionService>();
        ChatHistory chatHistory = new();

        chatHistory.AddSystemMessage(systemMessage);

        string userQuestion = "";
        var responseBuilder = new StringBuilder();

        //Chat loop.
        Console.WriteLine("==============================================");
        Console.WriteLine("Welcome to the chat, ask me anything on yourself.");
        Console.WriteLine("Type '/exit' to exit the chat.");
        Console.WriteLine("==============================================");

        while (true)
        {
            Console.Write("User:> ");
            userQuestion = Console.ReadLine()!;

            if (userQuestion == "/exit") { break; }

            var contextBuilder = new StringBuilder();

            #region Get Results from Bing Search (Text Search).

            // Bing Search.
            await contextBuilder.BingSearchAsync(logger, bingTextSearch, userQuestion);
            chatHistory.AddUserMessage(contextBuilder.ToString());

            #endregion

            #region Get Results from Vector Store (Vector Search).

            // Vector Store.
            await contextBuilder.VectoreStoreSearchAsync(logger, ollamaEmbeddingGeneratorService, collection, userQuestion, 3);
            chatHistory.AddUserMessage(contextBuilder.ToString());

            #endregion

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