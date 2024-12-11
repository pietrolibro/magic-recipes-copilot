using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Text;
using System.Text.RegularExpressions;

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.VectorData;

using Qdrant.Client;
using Qdrant.Client.Grpc;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Connectors.InMemory;

// Logging.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace RecipesCopilot.Ollama.Chat.Demo;

internal class Program
{
    static async Task Main(string[] args)
    {
        const string OLLAMA_MODEL_ID = "llama3.2:latest";
        const string OLLAMA_ENDPOINT = "http://localhost:11434";
        const string OLLAMA_EMBEDDING_MODEL_ID = "nomic-embed-text:latest";

        string QDRANT_HOST = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
        int QDRANT_REST_PORT = int.Parse(Environment.GetEnvironmentVariable("QDRANT_REST_PORT"));
        int QDRANT_GRPC_PORT = int.Parse(Environment.GetEnvironmentVariable("QDRANT_GRPC_PORT"));
        string QDRANT_API_KEY = Environment.GetEnvironmentVariable("QDRANT_API_KEY") ?? Guid.Empty.ToString();

        // dotnet add package Microsoft.Extensions.Configur ation --version 9.0.0
        // dotnet add package Microsoft.Extensions.Logging.Console --version 9.0.0
        // dotnet add package Microsoft.SemanticKernel --version 1.31.0
        // dotnet add package Microsoft.SemanticKernel.Connectors.Qdrant --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Connectors.Ollama --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Plugins.Core --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Plugins.Memory --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Plugins.Core --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Connectors.InMemory --version 1.31.0-alpha
        // dotnet add package Microsoft.SemanticKernel.Plugins.Web --version 1.31.0-alpha

        //Step 2 - Add AI Services (Experimental).
        #pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, SKEXP0050, SKEXP0070

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
        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Build the Kernel.
        var kernel = builder.Build();

        // Grab the logger.
        ILogger logger = kernel.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Kernel is ready.");

        // Add Memory, this time we are using Qdrant Vector Store.
        QdrantGrpcClient qdrantGrpcClient = new QdrantGrpcClient(
            host: QDRANT_HOST,
            port: QDRANT_GRPC_PORT,
            apiKey: QDRANT_API_KEY);

        var qdrantClient = new QdrantClient(qdrantGrpcClient);

        // Useful links:
        // - https://github.com/qdrant/qdrant-dotnet/blob/main/src/Qdrant.Client/QdrantClient.cs
        // - https://github.com/microsoft/semantic-kernel/blob/main/dotnet/src/Connectors/Connectors.Memory.Qdrant/QdrantVectorStore.cs
        var vectorStore = new QdrantVectorStore(qdrantClient, new() { HasNamedVectors = true });

        var ollamaEmbeddingGeneratorService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

        var recipesCollection = await vectorStore.CreateCollectionAsync(logger, qdrantClient, ollamaEmbeddingGeneratorService);

        #region System Message

        string systemMessage = @"
            You are a helpful AI assistant specializing in Italian recipes for Christmas. All responses need to be in Italian.

            If not recipes have been found or if the user does not specify a particular ingredient, preference, or the name of a specific recipe, 
            begin by politely asking for more information as preferences, number of guest or ingredients before suggesting a recipe.
            Do not suggest the same recipe twice and do not suggest recipes without know user preferences.
           
            If the user asks for a name of recipe, provide the recipe directly.

            Ensure that the recipe is simple, with ingredients that can be found in a local store.

            For each recipe, provide the following information in this order:

            - Name of the recipe in uppercase
            - List of ingredients (as a numbered list)
            - Preparation steps (as a numbered list)
            - Cooking time
            - Calories per portion

            If the user asks for another recipe, suggest a different recipe that fits the criteria. 

            Each piece of information should be separated by a new line.
            ";

        #endregion

        //Create new chat
        IChatCompletionService ollamaChatService = kernel.GetRequiredService<IChatCompletionService>();
        ChatHistory chatHistory = new();

        chatHistory.AddSystemMessage(systemMessage);

        string userQuestion = "";
        var responseBuilder = new StringBuilder();

        //Chat loop.
        Console.WriteLine("==============================================");
        Console.WriteLine("Welcome to the Christmas-Recipes Copilot, ask me to find some Christmas recipes.");
        Console.WriteLine("Type '/exit' to exit the chat.");
        Console.WriteLine("==============================================");

        StringBuilder contextBuilder = new();

        while (true)
        {
            Console.Write("User:> ");
            userQuestion = Console.ReadLine()!;

            if (userQuestion == "/exit") { break; }

            contextBuilder.Clear();

            await contextBuilder.VectoreStoreSearchAsync(logger, ollamaEmbeddingGeneratorService, recipesCollection, userQuestion, 1);

            int contextToRemove = -1;
            if (contextBuilder.Length != 0)
            {
                contextBuilder.Insert(0, "Here's some additional information: ");
                contextToRemove = chatHistory.Count;
                chatHistory.AddUserMessage(contextBuilder.ToString());

                Console.WriteLine("==============================================");
                Console.WriteLine("RAG Context:");
                Console.WriteLine(contextBuilder.ToString());
                Console.WriteLine("==============================================");
            }

            chatHistory.AddUserMessage(userQuestion);

            contextBuilder.Clear();
            await foreach (var message in ollamaChatService.GetStreamingChatMessageContentsAsync(chatHistory, null, kernel))
            {
                Console.Write(message);
                contextBuilder.Append(message.Content);
            }

            Console.WriteLine();
            chatHistory.AddAssistantMessage(contextBuilder.ToString());

            if (contextToRemove >= 0)
            {
                // Console.WriteLine("==============================================");
                // Console.WriteLine("RAG Context to remove:");
                // Console.WriteLine(chatHistory[contextToRemove]);
                chatHistory.RemoveAt(contextToRemove);
                // Console.WriteLine(contextToRemove);
                // Console.WriteLine("==============================================");
            }
            Console.WriteLine();
        }
    }
}


public class RecipeRecord
{
    [VectorStoreRecordKey]
    public Guid Key { get; set; }

    [VectorStoreRecordData(IsFilterable = true, StoragePropertyName = "recipe_title")]
    public string? Title { get; set; }

    [VectorStoreRecordData(IsFullTextSearchable = true, StoragePropertyName = "recipe_description")]
    public string? Description { get; set; }

    [VectorStoreRecordData(IsFilterable = true, StoragePropertyName = "recipe_ingredients")]
    public string? Ingredients { get; set; }

    [VectorStoreRecordData(IsFullTextSearchable = true, StoragePropertyName = "recipe_procedure")]
    public string? Preparation { get; set; }

    [VectorStoreRecordData(IsFilterable = true, StoragePropertyName = "recipe_source")]
    public string? ReferenceLink { get; set; }

    // Supported types of metrics by Qdrant:
    // https://qdrant.tech/documentation/concepts/search/
    [VectorStoreRecordVector(768, DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> TitleVector { get; set; }

    // Supported types of metrics by Qdrant:
    // https://qdrant.tech/documentation/concepts/search/
    [VectorStoreRecordVector(768, DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> IngredientsVector { get; set; }

    // Supported types of metrics by Qdrant:
    // https://qdrant.tech/documentation/concepts/search/
    [VectorStoreRecordVector(768, DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> DescriptionVector { get; set; }
}

public static class TextSearchExtensions
{
    public static async Task VectoreStoreSearchAsync(
            this StringBuilder contextBuilder, ILogger logger,
            ITextEmbeddingGenerationService textEmbeddingGenerationService,
            IVectorStoreRecordCollection<System.Guid, RecipeRecord> collection,
            string query, int top = 3)
    {
        var searchVector = await textEmbeddingGenerationService.GenerateEmbeddingAsync(query);
        var searchResult = await collection.VectorizedSearchAsync(searchVector, new() { Top = top });

        contextBuilder.Clear();

        logger.LogInformation("==============================================");
        logger.LogInformation("Vector Search Results: ");

        await foreach (VectorSearchResult<RecipeRecord> result in searchResult.Results)
        {
            if (result.Score < 0.6)
            {
                contextBuilder.AppendLine("No recipes or other information found.");
            }
            else
            {
                contextBuilder.AppendLine("Recipe Title:");
                contextBuilder.AppendLine(result.Record.Title);
                contextBuilder.AppendLine("Recipe Description:");
                contextBuilder.AppendLine(result.Record.Description);
                contextBuilder.AppendLine("Recipe Ingredients:");
                contextBuilder.AppendLine(result.Record.Ingredients);
                contextBuilder.AppendLine("Recipe Procedure:");
                contextBuilder.AppendLine(result.Record.Preparation);
            }
        }

        logger.LogInformation("contextBuilder: " + contextBuilder.ToString());
        logger.LogInformation("==============================================");
    }

}
public static class RecipeRecordDB
{
    public static async Task<IVectorStoreRecordCollection<System.Guid, RecipeRecord>> CreateCollectionAsync(this QdrantVectorStore vectorStore,
        ILogger logger, QdrantClient qdrantClient, ITextEmbeddingGenerationService textEmbeddingGenerationService)
    {
        // Get and create collection if it doesn't exist.
        var recipesCollection = vectorStore.GetCollection<System.Guid, RecipeRecord>("christmas-recipes");

        var collection = new QdrantVectorStoreRecordCollection<RecipeRecord>(qdrantClient,
            "christmas-recipes", new() { HasNamedVectors = true });

        await recipesCollection.CreateCollectionIfNotExistsAsync();

        logger.LogInformation("Christmas-recipes collection is ready.");

        // RAG => Chunking + Embedding Generation.
        logger.LogInformation("Starting RAG Chunking and Embedding Generation.");

        // Read the content of the recipes from the "Recipes" folder.
        var recipesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Recipes");
        var recipeFiles = Directory.GetFiles(recipesDirectory, "*.txt");

        logger.LogInformation($"==============================================");
        logger.LogInformation($"Found {recipeFiles.Length} recipe files.");
        logger.LogInformation($"==============================================");

        foreach (var recipeFile in recipeFiles)
        {
            logger.LogInformation($"==============================================");
            logger.LogInformation($"Reading content from '{recipeFile}'");

            var content = await File.ReadAllTextAsync(recipeFile);

            // Parse sections from the recipe
            var sections = ParseRecipeSections(content);

            // Log or use the extracted sections
            foreach (var section in sections)
            {
                Console.WriteLine($"{section.Key}: {section.Value.Substring(0, Math.Min(section.Value.Length, 100))}...");
            }

            RecipeRecord recipeRecord = new()
            {
                Key = Guid.NewGuid(),
                ReferenceLink = sections["Url"] ?? "",
                Title = sections["Title"] ?? "",
                Description = sections["Description"] ?? "",
                Ingredients = sections["Ingredients"] ?? "",
                Preparation = sections["Step by step recipe instructions"],

                TitleVector = await textEmbeddingGenerationService.GenerateEmbeddingAsync(sections["Title"] ?? ""),
                DescriptionVector = await textEmbeddingGenerationService.GenerateEmbeddingAsync(sections["Description"] ?? ""),
                IngredientsVector = await textEmbeddingGenerationService.GenerateEmbeddingAsync(sections["Ingredients"] ?? "")
            };

            await recipesCollection.UpsertAsync(recipeRecord);
            // }
            logger.LogInformation($"==============================================");
            logger.LogInformation(Environment.NewLine);
        }

        return recipesCollection;
    }

    // Define a method to parse sections
    private static Dictionary<string, string> ParseRecipeSections(string content)
    {
        var sections = new Dictionary<string, string>();

        // Regex to match headers like [Url=], [Title=], etc.
        var regex = new Regex(@"\[(.+?)\]");

        // Find all matches
        var matches = regex.Matches(content);

        for (int i = 0; i < matches.Count; i++)
        {
            var header = matches[i].Groups[1].Value;
            var headerName = header.Contains("=") ? header.Split('=')[0] : header;

            // Determine the start and end of the section
            int startIndex = matches[i].Index + matches[i].Length;
            int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : content.Length;

            // Extract section content and trim any whitespace
            var sectionContent = content.Substring(startIndex, endIndex - startIndex).Trim();

            // Add section to dictionary
            sections[headerName] = sectionContent;
        }

        return sections;
    }
}