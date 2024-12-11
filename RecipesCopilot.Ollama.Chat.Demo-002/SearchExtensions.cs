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
public static class SearchExtensions
{
    /// <summary>
    /// Bing Search Extension Method.
    /// </summary>
    /// <param name="bingTextSearch"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    public static async Task BingSearchAsync(this StringBuilder contextBuilder, ILogger logger,
        BingTextSearch bingTextSearch, string query)
    {

        // Search and return results as a string items.
        KernelSearchResults<string> stringResults = await bingTextSearch.SearchAsync(query,
            new() { Top = 5, Skip = 0 });

        contextBuilder.Clear();
        contextBuilder.AppendLine("Here's some additional information from the web: ");

        logger.LogInformation("==============================================");
        logger.LogInformation("Bing Search Results: ");
        await foreach (string result in stringResults.Results)
        {
            contextBuilder.AppendLine(result);
            logger.LogInformation(result);
        }
        logger.LogInformation("==============================================");
    }

    public static async Task VectoreStoreSearchAsync(
            this StringBuilder contextBuilder,ILogger logger,
            ITextEmbeddingGenerationService textEmbeddingGenerationService,
            IVectorStoreRecordCollection<System.Guid, PersonalRecord> collection, 
            string query, int top = 3){

        var searchVector = await textEmbeddingGenerationService.GenerateEmbeddingAsync(query);
        var searchResult = await collection.VectorizedSearchAsync(searchVector, new() { Top = top });

        contextBuilder.Clear();
        contextBuilder.AppendLine("Here's some additional information: ");

        logger.LogInformation("==============================================");
        logger.LogInformation("Vector Search Results: ");
        await foreach (VectorSearchResult<PersonalRecord> result in searchResult.Results )
        {
            contextBuilder.AppendLine(result.Record.RecordContent);
            logger.LogInformation(result.Record.RecordContent);
        }
    }

}