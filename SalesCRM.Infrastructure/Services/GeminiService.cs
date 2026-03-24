using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiService> _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxOutputTokens;
    private readonly double _temperature;

    public GeminiService(HttpClient httpClient, IConfiguration config, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = config["Gemini:ApiKey"] ?? "";
        _model = config["Gemini:Model"] ?? "gemini-2.0-flash";
        _maxOutputTokens = int.TryParse(config["Gemini:MaxOutputTokens"], out var t) ? t : 4096;
        _temperature = double.TryParse(config["Gemini:Temperature"], out var temp) ? temp : 0.3;
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
    }

    public async Task<GeminiResponse> GenerateContentAsync(string systemPrompt, string userPrompt)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var requestBody = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { parts = new[] { new { text = userPrompt } } } },
            generationConfig = new
            {
                temperature = _temperature,
                maxOutputTokens = _maxOutputTokens,
                responseMimeType = "application/json"
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Retry with exponential backoff for rate limiting
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsync(url, content);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var delay = (int)Math.Pow(2, attempt + 1) * 1000;
                    _logger.LogWarning("Gemini rate limited (429). Retrying in {Delay}ms (attempt {Attempt}/3)", delay, attempt + 1);
                    await Task.Delay(delay);
                    continue;
                }

                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API error {StatusCode}: {Response}", response.StatusCode, responseJson);
                    return new GeminiResponse { Success = false, Error = $"Gemini API returned {response.StatusCode}: {responseJson}" };
                }

                // Parse response
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                var text = root
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "";

                var tokensUsed = 0;
                if (root.TryGetProperty("usageMetadata", out var usage))
                {
                    if (usage.TryGetProperty("totalTokenCount", out var total))
                        tokensUsed = total.GetInt32();
                }

                return new GeminiResponse { Content = text, TokensUsed = tokensUsed, Success = true };
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Gemini API request timed out (attempt {Attempt}/3)", attempt + 1);
                if (attempt == 2)
                    return new GeminiResponse { Success = false, Error = "Gemini API request timed out after 3 attempts" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini API call failed (attempt {Attempt}/3)", attempt + 1);
                if (attempt == 2)
                    return new GeminiResponse { Success = false, Error = ex.Message };
            }
        }

        return new GeminiResponse { Success = false, Error = "All retry attempts failed" };
    }
}
