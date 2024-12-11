using Qdrant.Client;
using Qdrant.Client.Grpc;

using Newtonsoft.Json;

using Microsoft.Extensions.Configuration;

using RecipesCopilot.Ollama.Chat.DTO;
using RecipesCopilot.Ollama.Chat.DTO.Responses;

namespace RecipesCopilot.Ollama.Chat.Services;

public interface IVectorRecipesSearchService
{
    Task<Recipe?> QueryAsync(EmbeddingResponse embeddingResponse, ILogger logger, float scoreThreshold = 0.6f);
}

public class VectorRecipesSearchService: IVectorRecipesSearchService
{
    private readonly ILogger _logger;

    public int QdrantRestPort{ get ;set; }= 0;
    public string VectorName{ get ;set; }= "";
    public string Collection { get ;set; }= "";
    public string QdrantApiKey { get ;set; }= "";
    public int QdrantGrpcPort { get ;set; }= 0;
    public string QdrantHost { get ;set; }= "";

    public VectorRecipesSearchService(IConfiguration configuration)
    {
        QdrantHost = configuration["Qdrant:Host"] ?? throw new ArgumentNullException("Qdrant:Host");
        QdrantRestPort = int.Parse(configuration["Qdrant:RestPort"] ?? throw new ArgumentNullException("Qdrant:RestPort"));
        QdrantGrpcPort = int.Parse(configuration["Qdrant:GrpcPort"] ?? throw new ArgumentNullException("Qdrant:GrpcPort"));
        QdrantApiKey = configuration["Qdrant:ApiKey"] ?? throw new ArgumentNullException("Qdrant:ApiKey");
        Collection = configuration["Qdrant:Collection"] ?? throw new ArgumentNullException("Qdrant:Collection");
        VectorName = configuration["Qdrant:VectorName"] ?? throw new ArgumentNullException("Qdrant:VectorName");
    }

    public async Task<Recipe?> QueryAsync(EmbeddingResponse embeddingResponse, ILogger logger,float scoreThreshold = 0.6f)
    {
        logger.LogInformation("Qdrant Configuration:");
        logger.LogInformation($"Qdrant Host: {QdrantHost}");
        logger.LogInformation($"Qdrant Rest Port: {QdrantRestPort}");
        logger.LogInformation($"Qdrant Grpc Port: {QdrantGrpcPort}");
        logger.LogInformation($"Qdrant Api Key: {QdrantApiKey}");
        logger.LogInformation($"Qdrant Collection: {Collection}");
        logger.LogInformation($"Qdrant Vector Name: {VectorName}");

        QdrantGrpcClient qdrantGrpcClient = new QdrantGrpcClient(
            host: QdrantHost,
            port: QdrantGrpcPort,
            apiKey: QdrantApiKey);

        var qdrantClient = new QdrantClient(qdrantGrpcClient);

        IReadOnlyList<Qdrant.Client.Grpc.ScoredPoint> result = await qdrantClient.QueryAsync(
            collectionName: Collection,
                query: embeddingResponse.Embeddings[0],
                usingVector: VectorName,
                scoreThreshold: scoreThreshold,
                limit: 1
            );

        string jsonResponse = JsonConvert.SerializeObject(result);
        var recipes = JsonConvert.DeserializeObject<List<Recipe>>(jsonResponse);

        return recipes.FirstOrDefault();
    }
}