namespace SalesCRM.Core.Interfaces;

public class GeminiResponse
{
    public string Content { get; set; } = "";
    public int TokensUsed { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public interface IGeminiService
{
    Task<GeminiResponse> GenerateContentAsync(string systemPrompt, string userPrompt);
}
