using Newtonsoft.Json;

namespace RecipesCopilot.Ollama.Chat.DTO;

public class RecipeId
{
    [JsonProperty("uuid")]
    public string Uuid { get; set; }
}

public class StringValue
{
    [JsonProperty("stringValue")]
    public string Value { get; set; }
}

public class RecipePayload
{
    [JsonProperty("recipe_title")]
    public StringValue RecipeTitle { get; set; }

    [JsonProperty("recipe_description")]
    public StringValue RecipeDescription { get; set; }

    [JsonProperty("recipe_source")]
    public StringValue RecipeSource { get; set; }

    [JsonProperty("recipe_ingredients")]
    public StringValue RecipeIngredients { get; set; }

    [JsonProperty("recipe_procedure")]
    public StringValue RecipeProcedure { get; set; }
}

public class Recipe
{
    [JsonProperty("id")]
    public RecipeId Id { get; set; }

    [JsonProperty("payload")]
    public RecipePayload Payload { get; set; }

    [JsonProperty("score")]
    public float Score { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }
}