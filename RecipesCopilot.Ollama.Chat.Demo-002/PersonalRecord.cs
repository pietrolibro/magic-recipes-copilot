
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

public class PersonalRecord
{
    [VectorStoreRecordKey]
    public System.Guid key { get; set; }

    [VectorStoreRecordData]
    public string RecordType { get; set; }

    [VectorStoreRecordData]
    public string RecordContent { get; set; }

    [VectorStoreRecordVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}



public static class PersonalRecordDB
{
    public static IList<PersonalRecord> GetCollection()
    {
        List<PersonalRecord> persontalEntries = new();

        persontalEntries.Add(new PersonalRecord
        {
            key = Guid.NewGuid(),
            RecordType = "Personal",
            RecordContent = "My name is Pietro and my surname is Libro. And I'm 43 years old. My profession is Cloud Solution Architect. I work for Swisscom."
        });

        persontalEntries.Add(new PersonalRecord
        {
            key = Guid.NewGuid(),
            RecordType = "Personal",
            RecordContent = "I have a cat and his name is 'Micio'. I'm currently living in Zürich, Switzerland with my Family."
        });

        persontalEntries.Add(new PersonalRecord
        {
            key = Guid.NewGuid(),
            RecordType = "Personal",
            RecordContent = "During my spare time I like to read books. I'm also a big fan of Italian cuisine. I'm preparing pizza every weekend."
        });

        return persontalEntries;

    }
}