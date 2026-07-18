using OpenAI.Chat;

// Image generation uses Pollinations.ai (https://pollinations.ai) — free, no API key or
// signup required. Chat still uses OpenAI and needs OPENAI_API_KEY.
using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(2) };

string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
ChatClient? chatClient = string.IsNullOrEmpty(apiKey) ? null : new(model: "gpt-4o-mini", apiKey: apiKey);

if (chatClient is null)
{
    Console.WriteLine("OPENAI_API_KEY is not set, so chat is disabled. Image generation still works for free.");
}

Console.WriteLine("Chat with OpenAI (type 'exit' to quit, or '/image <prompt>' to generate a free image)");
while (true)
{
    Console.Write("> ");
    string? input = Console.ReadLine();
    if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (input.StartsWith("/image ", StringComparison.OrdinalIgnoreCase))
    {
        string imagePrompt = input["/image ".Length..];
        try
        {
            string fileName = await GenerateFreeImageAsync(httpClient, imagePrompt);
            Console.WriteLine($"Saved to {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Image generation failed: {ex.Message}");
        }
        continue;
    }

    if (chatClient is null)
    {
        Console.WriteLine("Chat is disabled because OPENAI_API_KEY is not set.");
        continue;
    }

    ChatCompletion completion = chatClient.CompleteChat(input);
    Console.WriteLine(completion.Content[0].Text);
}

static async Task<string> GenerateFreeImageAsync(HttpClient httpClient, string prompt, int width = 1024, int height = 1024)
{
    string encodedPrompt = Uri.EscapeDataString(prompt);
    string url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width={width}&height={height}&nologo=true";

    using HttpResponseMessage response = await httpClient.GetAsync(url);
    response.EnsureSuccessStatusCode();

    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
    string fileName = $"image-{DateTime.Now:yyyyMMdd-HHmmss}.jpg";
    await File.WriteAllBytesAsync(fileName, imageBytes);
    return fileName;
}
