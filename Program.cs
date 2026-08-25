using System.Text;
using System.Text.Json;
using OpenAI.Chat;

// Generation backends, cheapest first:
//   /image  -> image.pollinations.ai        free, no key, no signup
//   /video  -> Hugging Face ZeroGPU Spaces  free, needs a free HF token (HF_TOKEN)
//              falls back to gen.pollinations.ai, which costs Pollen credits
//   chat    -> OpenAI, needs OPENAI_API_KEY
using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

string? openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
ChatClient? chatClient = string.IsNullOrEmpty(openAiApiKey) ? null : new(model: "gpt-4o-mini", apiKey: openAiApiKey);

string? hfToken = Environment.GetEnvironmentVariable("HF_TOKEN");
string? pollinationsApiKey = Environment.GetEnvironmentVariable("POLLINATIONS_API_KEY");

if (chatClient is null)
{
    Console.WriteLine("OPENAI_API_KEY is not set, so chat is disabled. Image and video generation still work.");
}

if (string.IsNullOrEmpty(hfToken) && string.IsNullOrEmpty(pollinationsApiKey))
{
    Console.WriteLine("No video backend configured. For free video, create a token at https://huggingface.co/settings/tokens and set HF_TOKEN.");
}

Console.WriteLine("Type '/image <prompt>', '/video <prompt>', a chat message, or 'exit'.");
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
        string videoPrompt = input["/video ".Length..];
        try
        {
            string fileName;
            if (!string.IsNullOrEmpty(hfToken))
            {
                Console.WriteLine("Generating a starting frame, then animating it on Hugging Face ZeroGPU (free, may take a few minutes)...");
                fileName = await GenerateFreeVideoAsync(httpClient, videoPrompt, hfToken);
            }
            else if (!string.IsNullOrEmpty(pollinationsApiKey))
            {
                Console.WriteLine("HF_TOKEN not set, falling back to Pollinations video (costs Pollen credits)...");
                fileName = await GeneratePollinationsVideoAsync(httpClient, videoPrompt, pollinationsApiKey);
            }
            else
            {
                Console.WriteLine("Video needs a backend. Free option: create a token at https://huggingface.co/settings/tokens and set HF_TOKEN.");
                continue;
            }

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

static string BuildPollinationsImageUrl(string prompt, int width, int height)
{
    string encodedPrompt = Uri.EscapeDataString(prompt);
    return $"https://image.pollinations.ai/prompt/{encodedPrompt}?width={width}&height={height}&nologo=true";
}

static async Task<string> GenerateFreeImageAsync(HttpClient httpClient, string prompt, int width = 1024, int height = 1024)
{
    using HttpResponseMessage response = await httpClient.GetAsync(BuildPollinationsImageUrl(prompt, width, height));
    response.EnsureSuccessStatusCode();

    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
    string fileName = $"image-{DateTime.Now:yyyyMMdd-HHmmss}.jpg";
    await File.WriteAllBytesAsync(fileName, imageBytes);
    return fileName;
}

// Free text-to-video: Pollinations paints the first frame (no key), then the Wan 2.2
// image-to-video Space animates it on Hugging Face's free ZeroGPU tier. ZeroGPU rejects
// anonymous API calls, so a free HF token is required — but no payment is ever involved.
static async Task<string> GenerateFreeVideoAsync(HttpClient httpClient, string prompt, string hfToken, double durationSeconds = 3.5)
{
    const string spaceHost = "https://zerogpu-aoti-wan2-2-fp8da-aoti-faster.hf.space";
    const string endpoint = "/gradio_api/call/generate_video";

    var startingFrame = new Dictionary<string, object?>
    {
        ["path"] = null,
        ["url"] = BuildPollinationsImageUrl(prompt, 832, 480),
        ["meta"] = new Dictionary<string, string> { ["_type"] = "gradio.FileData" },
    };

    // Positional arguments, in the order the Space's /generate_video endpoint declares them.
    var payload = new
    {
        data = new object?[]
        {
            startingFrame,
            prompt,
            6,                      // steps
            "blurry, distorted, static, low quality",
            durationSeconds,
            1,                      // guidance_scale
            1,                      // guidance_scale_2
            42,                     // seed
            true,                   // randomize_seed
        },
    };

    using HttpRequestMessage submit = new(HttpMethod.Post, spaceHost + endpoint)
    {
        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
    };
    submit.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);

    using HttpResponseMessage submitResponse = await httpClient.SendAsync(submit);
    await EnsureSuccessAsync(submitResponse);

    string submitBody = await submitResponse.Content.ReadAsStringAsync();
    string eventId = JsonDocument.Parse(submitBody).RootElement.GetProperty("event_id").GetString()
        ?? throw new InvalidOperationException("Hugging Face did not return an event id.");

    string videoUrl = await ReadGradioResultUrlAsync(httpClient, $"{spaceHost}{endpoint}/{eventId}", hfToken);

    using HttpRequestMessage download = new(HttpMethod.Get, videoUrl);
    download.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);
    using HttpResponseMessage videoResponse = await httpClient.SendAsync(download);
    await EnsureSuccessAsync(videoResponse);

    byte[] videoBytes = await videoResponse.Content.ReadAsByteArrayAsync();
    string fileName = $"video-{DateTime.Now:yyyyMMdd-HHmmss}.mp4";
    await File.WriteAllBytesAsync(fileName, videoBytes);
    return fileName;
}

// Gradio streams progress as server-sent events and ends with either "complete" or "error".
static async Task<string> ReadGradioResultUrlAsync(HttpClient httpClient, string streamUrl, string hfToken)
{
    using HttpRequestMessage request = new(HttpMethod.Get, streamUrl);
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);

    using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    await EnsureSuccessAsync(response);

    using StreamReader reader = new(await response.Content.ReadAsStreamAsync());
    string? currentEvent = null;

    while (await reader.ReadLineAsync() is { } line)
    {
        if (line.StartsWith("event:", StringComparison.Ordinal))
        {
            currentEvent = line["event:".Length..].Trim();
            continue;
        }

        if (!line.StartsWith("data:", StringComparison.Ordinal))
        {
            continue;
        }

        string data = line["data:".Length..].Trim();

        if (currentEvent == "error")
        {
            throw new InvalidOperationException(
                data is "null" or ""
                    ? "the Space rejected the request (most often an expired token or exhausted free ZeroGPU quota)"
                    : data);
        }

        if (currentEvent == "complete")
        {
            return ExtractVideoUrl(data);
        }
    }

    throw new InvalidOperationException("the Space closed the stream without returning a video.");
}

static string ExtractVideoUrl(string completeEventData)
{
    JsonElement root = JsonDocument.Parse(completeEventData).RootElement;
    if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
    {
        throw new InvalidOperationException($"unexpected response from the Space: {completeEventData}");
    }

    JsonElement first = root[0];
    if (first.ValueKind == JsonValueKind.Object)
    {
        // Newer Spaces nest the file under "video"; older ones return it directly.
        if (first.TryGetProperty("video", out JsonElement nested) && nested.ValueKind == JsonValueKind.Object)
        {
            first = nested;
        }

        if (first.TryGetProperty("url", out JsonElement url) && url.GetString() is { Length: > 0 } urlValue)
        {
            return urlValue;
        }
    }

    throw new InvalidOperationException($"could not find a video URL in the response: {completeEventData}");
}

// Paid fallback: gen.pollinations.ai charges Pollen credits per clip.
static async Task<string> GeneratePollinationsVideoAsync(HttpClient httpClient, string prompt, string apiKey, string model = "veo")
{
    string url = $"https://gen.pollinations.ai/video/{Uri.EscapeDataString(prompt)}?model={model}";

    using HttpRequestMessage request = new(HttpMethod.Get, url);
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

    using HttpResponseMessage response = await httpClient.SendAsync(request);
    await EnsureSuccessAsync(response);

    byte[] videoBytes = await response.Content.ReadAsByteArrayAsync();
    string fileName = $"video-{DateTime.Now:yyyyMMdd-HHmmss}.mp4";
    await File.WriteAllBytesAsync(fileName, videoBytes);
    return fileName;
}

static async Task EnsureSuccessAsync(HttpResponseMessage response)
{
    if (response.IsSuccessStatusCode)
    {
        return;
    }

    string body = await response.Content.ReadAsStringAsync();
    throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
}
