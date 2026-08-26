using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

string? apiKey = Environment.GetEnvironmentVariable("HUGGINGFACE_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Set the HUGGINGFACE_API_KEY environment variable before running this app.");
    Console.WriteLine("Get your API key from: https://huggingface.co/settings/tokens");
    return;
}

using HttpClient httpClient = new();
httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

const string HuggingFaceApiUrl = "https://api-inference.huggingface.co/models/MiniMaxAI/MiniMax-M3";

Console.WriteLine("Chat with MiniMax-M3 via HuggingFace (type 'exit' to quit)");
List<Message> conversationHistory = [];

while (true)
{
    Console.Write("> ");
    string? input = Console.ReadLine();
    if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    // Add user message to history
    conversationHistory.Add(new Message { Role = "user", Content = input });

    try
    {
        // Prepare request for MiniMax-M3
        var request = new HuggingFaceRequest
        {
            Inputs = input,
            Parameters = new Parameters { MaxNewTokens = 512 }
        };

        var response = await httpClient.PostAsJsonAsync(HuggingFaceApiUrl, request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<List<HuggingFaceResponse>>();
            if (result != null && result.Count > 0)
            {
                string assistantMessage = result[0].GeneratedText;
                Console.WriteLine(assistantMessage);

                // Add assistant response to history
                conversationHistory.Add(new Message { Role = "assistant", Content = assistantMessage });
            }
        }
        else
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error: {response.StatusCode} - {errorContent}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error calling MiniMax-M3: {ex.Message}");
    }
}

// Models for HuggingFace API
class HuggingFaceRequest
{
    [JsonPropertyName("inputs")]
    public string? Inputs { get; set; }

    [JsonPropertyName("parameters")]
    public Parameters? Parameters { get; set; }
}

class Parameters
{
    [JsonPropertyName("max_new_tokens")]
    public int MaxNewTokens { get; set; } = 256;

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.7f;
}

class HuggingFaceResponse
{
    [JsonPropertyName("generated_text")]
    public string? GeneratedText { get; set; }
}

class Message
{
    public string? Role { get; set; }
    public string? Content { get; set; }
}
