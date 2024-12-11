public class ChatMessageContentResponse
{
    public string model { get; set; }
    public string created_at { get; set; }
    public Message message { get; set; }
    public bool done { get; set; }
}

public class VersionResponse
{
    public string version { get; set; }
}


public class EmbeddingResponse
{
    public List<List<float>> Data { get; set; }
}
