using OpenAI.Chat;

// Image generation uses Pollinations.ai (https://pollinations.ai) — free, no API key or
// signup required. Video generation uses the same platform's gen.pollinations.ai API, which
// requires a free API key (POLLINATIONS_API_KEY) with a limited weekly credit grant. Chat
// still uses OpenAI and needs OPENAI_API_KEY.
using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

string? openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
ChatClient? chatClient = string.IsNullOrEmpty(openAiApiKey) ? null : new(model: "gpt-4o-mini", apiKey: openAiApiKey);

string? pollinationsApiKey = Environment.GetEnvironmentVariable("POLLINATIONS_API_KEY");

if (chatClient is null)
{
    Console.WriteLine("OPENAI_API_KEY is not set, so chat is disabled. Image generation still works for free.");
}

if (string.IsNullOrEmpty(pollinationsApiKey))
{
    Console.WriteLine("POLLINATIONS_API_KEY is not set, so /video is disabled. Get a free key at https://enter.pollinations.ai/keys");
}

Console.WriteLine("Chat with OpenAI (type 'exit' to quit, '/image <prompt>' for a free image, or '/video <prompt>' for a video)");
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

    if (input.StartsWith("/video ", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrEmpty(pollinationsApiKey))
        {
            Console.WriteLine("Video generation needs a free Pollinations API key. Get one at https://enter.pollinations.ai/keys and set POLLINATIONS_API_KEY.");
            continue;
        }

        string videoPrompt = input["/video ".Length..];
        try
        {
            string fileName = await GenerateVideoAsync(httpClient, videoPrompt, pollinationsApiKey);
            Console.WriteLine($"Saved to {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Video generation failed: {ex.Message}");
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

static async Task<string> GenerateVideoAsync(HttpClient httpClient, string prompt, string apiKey, string model = "veo")
{
    string encodedPrompt = Uri.EscapeDataString(prompt);
    string url = $"https://gen.pollinations.ai/video/{encodedPrompt}?model={model}";

    using HttpRequestMessage request = new(HttpMethod.Get, url);
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

    using HttpResponseMessage response = await httpClient.SendAsync(request);
    if (!response.IsSuccessStatusCode)
    {
        string body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }

    byte[] videoBytes = await response.Content.ReadAsByteArrayAsync();
    string fileName = $"video-{DateTime.Now:yyyyMMdd-HHmmss}.mp4";
    await File.WriteAllBytesAsync(fileName, videoBytes);
    return fileName;
}
