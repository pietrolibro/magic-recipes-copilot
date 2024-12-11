
public class GenerateRequest
{
    public string model { get; set; }
    public string prompt { get; set; }
    public bool stream { get; set; } = false;
}

public class GenerateEmbedRequest
{
    public string model { get; set; }
    public string input { get; set; }
}

public class EmbeddingRequest
{
    public string Input { get; set; }
}

public class GenerateEmbeddingRequest
{
    public string model { get; set; }
    public string prompt { get; set; }
}


public class ChatRequest
{
    public string model { get; set; }
    public string prompt { get; set; }
    public Message[] messages { get; set; }
}