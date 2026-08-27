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

Console.WriteLine("Type '/image <prompt>' (free), '/flux <prompt>' (higher quality), '/video <prompt>', a chat message, or 'exit'.");
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

    if (input.StartsWith("/flux ", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrEmpty(hfToken))
        {
            Console.WriteLine("/flux needs a free token from https://huggingface.co/settings/tokens set as HF_TOKEN. Plain /image needs no key.");
            continue;
        }

        string fluxPrompt = input["/flux ".Length..];
        try
        {
            Console.WriteLine("Generating with FLUX.1-schnell on ZeroGPU...");
            string fileName = await GenerateFluxImageAsync(httpClient, fluxPrompt, hfToken);
            Console.WriteLine($"Saved to {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FLUX generation failed: {ex.Message}");
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
                Console.WriteLine("Generating on Hugging Face ZeroGPU (free, but queues can take several minutes)...");
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

// Higher-fidelity images from FLUX.1-schnell, run on Black Forest Labs' own ZeroGPU Space.
// Routing the same model through Hugging Face's Inference Providers instead would bill
// third-party providers against the account's monthly credit, which a free account
// exhausts in about one image; the Space is covered by the free ZeroGPU allowance.
static async Task<string> GenerateFluxImageAsync(HttpClient httpClient, string prompt, string hfToken, int width = 1024, int height = 1024, int steps = 4)
{
    const string space = "black-forest-labs/FLUX.1-schnell";
    string spaceHost = $"https://{ToSpaceSubdomain(space)}.hf.space";

    int functionIndex = await ResolveFunctionIndexAsync(httpClient, spaceHost, "infer");
    string sessionHash = Guid.NewGuid().ToString("n")[..12];

    // Positional arguments, in the order the Space's infer endpoint declares them.
    var payload = new
    {
        data = new object?[] { prompt, 0, true, width, height, steps },
        fn_index = functionIndex,
        session_hash = sessionHash,
    };

    using HttpRequestMessage join = new(HttpMethod.Post, $"{spaceHost}/gradio_api/queue/join")
    {
        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
    };
    join.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);

    using HttpResponseMessage joinResponse = await httpClient.SendAsync(join);
    await EnsureSuccessAsync(joinResponse);

    string imageRef = await ReadQueueResultAsync(httpClient, $"{spaceHost}/gradio_api/queue/data?session_hash={sessionHash}", hfToken);
    string imageUrl = imageRef.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        ? imageRef
        : $"{spaceHost}/gradio_api/file={imageRef}";

    using HttpRequestMessage download = new(HttpMethod.Get, imageUrl);
    download.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);
    using HttpResponseMessage imageResponse = await httpClient.SendAsync(download);
    await EnsureSuccessAsync(imageResponse);

    string extension = Path.GetExtension(new Uri(imageUrl).AbsolutePath) is { Length: > 1 } ext ? ext : ".webp";
    byte[] imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
    string fileName = $"flux-{DateTime.Now:yyyyMMdd-HHmmss}{extension}";
    await File.WriteAllBytesAsync(fileName, imageBytes);
    return fileName;
}

// Free video on Hugging Face's ZeroGPU tier. Pollinations paints the opening frame for
// free, then the Wan 2.2 image-to-video Space animates it. No payment is involved, but
// ZeroGPU rejects anonymous API calls, so a free HF token is required.
//
// Two details matter for staying inside the free tier:
//   * The clip is deliberately short and low-step. ZeroGPU sizes its GPU reservation from
//     these values, and a free account cannot reserve the 200s+ that a full-length
//     text-to-video model asks for — such requests are refused outright.
//   * The frame is uploaded to the Space rather than handed over as a URL. The Space
//     fails to fetch remote URLs itself and reports it only as an opaque app error.
static async Task<string> GenerateFreeVideoAsync(HttpClient httpClient, string prompt, string hfToken, double durationSeconds = 1.5, int steps = 4)
{
    string space = Environment.GetEnvironmentVariable("HF_VIDEO_SPACE") ?? "zerogpu-aoti/wan2-2-fp8da-aoti-faster";
    string spaceHost = $"https://{ToSpaceSubdomain(space)}.hf.space";

    Console.WriteLine("Painting the opening frame...");
    byte[] frame = await httpClient.GetByteArrayAsync(BuildPollinationsImageUrl(prompt, 832, 480));
    string framePath = await UploadToSpaceAsync(httpClient, spaceHost, hfToken, frame);

    int functionIndex = await ResolveFunctionIndexAsync(httpClient, spaceHost, "generate_video");
    string sessionHash = Guid.NewGuid().ToString("n")[..12];

    var uploadedFrame = new Dictionary<string, object?>
    {
        ["path"] = framePath,
        ["meta"] = new Dictionary<string, string> { ["_type"] = "gradio.FileData" },
    };

    // Positional arguments, in the order the Space's generate_video endpoint declares them.
    var payload = new
    {
        data = new object?[]
        {
            uploadedFrame,
            prompt,
            steps,
            "blurry, distorted, low quality",
            durationSeconds,
            1,      // guidance_scale
            1,      // guidance_scale_2
            42,     // seed
            true,   // randomize_seed
        },
        fn_index = functionIndex,
        session_hash = sessionHash,
    };

    using HttpRequestMessage join = new(HttpMethod.Post, $"{spaceHost}/gradio_api/queue/join")
    {
        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
    };
    join.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);

    using HttpResponseMessage joinResponse = await httpClient.SendAsync(join);
    await EnsureSuccessAsync(joinResponse);

    Console.WriteLine("Animating it on ZeroGPU...");
    string videoRef = await ReadQueueResultAsync(httpClient, $"{spaceHost}/gradio_api/queue/data?session_hash={sessionHash}", hfToken);
    string videoUrl = videoRef.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        ? videoRef
        : $"{spaceHost}/gradio_api/file={videoRef}";

    using HttpRequestMessage download = new(HttpMethod.Get, videoUrl);
    download.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);
    using HttpResponseMessage videoResponse = await httpClient.SendAsync(download);
    await EnsureSuccessAsync(videoResponse);

    byte[] videoBytes = await videoResponse.Content.ReadAsByteArrayAsync();
    string fileName = $"video-{DateTime.Now:yyyyMMdd-HHmmss}.mp4";
    await File.WriteAllBytesAsync(fileName, videoBytes);
    return fileName;
}

// Hands the frame to the Space's own file store and returns the server-side path.
static async Task<string> UploadToSpaceAsync(HttpClient httpClient, string spaceHost, string hfToken, byte[] frame)
{
    using MultipartFormDataContent form = new();
    ByteArrayContent file = new(frame);
    file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
    form.Add(file, "files", "frame.jpg");

    using HttpRequestMessage request = new(HttpMethod.Post, $"{spaceHost}/gradio_api/upload") { Content = form };
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);

    using HttpResponseMessage response = await httpClient.SendAsync(request);
    await EnsureSuccessAsync(response);

    using JsonDocument uploaded = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    if (uploaded.RootElement.ValueKind == JsonValueKind.Array
        && uploaded.RootElement.GetArrayLength() > 0
        && uploaded.RootElement[0].GetString() is { Length: > 0 } path)
    {
        return path;
    }

    throw new InvalidOperationException("the Space did not accept the opening frame.");
}

// "owner/Space_Name" is served from "owner-space-name.hf.space".
static string ToSpaceSubdomain(string space)
{
    StringBuilder builder = new(space.Length);
    foreach (char c in space)
    {
        builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
    }

    return builder.ToString();
}

// queue/join wants a numeric fn_index, which the Space's config maps from the api_name.
static async Task<int> ResolveFunctionIndexAsync(HttpClient httpClient, string spaceHost, string apiName)
{
    using HttpResponseMessage response = await httpClient.GetAsync($"{spaceHost}/config");
    await EnsureSuccessAsync(response);

    using JsonDocument config = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    if (config.RootElement.TryGetProperty("dependencies", out JsonElement dependencies))
    {
        foreach (JsonElement dependency in dependencies.EnumerateArray())
        {
            if (dependency.TryGetProperty("api_name", out JsonElement name)
                && name.ValueKind == JsonValueKind.String
                && string.Equals(name.GetString(), apiName, StringComparison.Ordinal)
                && dependency.TryGetProperty("id", out JsonElement id)
                && id.TryGetInt32(out int index))
            {
                return index;
            }
        }
    }

    throw new InvalidOperationException($"the Space does not expose a '{apiName}' endpoint.");
}

// The queue stream emits one JSON object per "data:" line until the job finishes.
static async Task<string> ReadQueueResultAsync(HttpClient httpClient, string streamUrl, string hfToken)
{
    using HttpRequestMessage request = new(HttpMethod.Get, streamUrl);
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hfToken);

    using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    await EnsureSuccessAsync(response);

    using StreamReader reader = new(await response.Content.ReadAsStreamAsync());
    while (await reader.ReadLineAsync() is { } line)
    {
        if (!line.StartsWith("data:", StringComparison.Ordinal))
        {
            continue;
        }

        using JsonDocument message = JsonDocument.Parse(line["data:".Length..].Trim());
        JsonElement root = message.RootElement;
        string? kind = root.TryGetProperty("msg", out JsonElement msg) ? msg.GetString() : null;

        if (kind == "estimation" && root.TryGetProperty("rank", out JsonElement rank) && rank.TryGetInt32(out int position) && position > 0)
        {
            Console.WriteLine($"Queued on ZeroGPU (position {position})...");
            continue;
        }

        if (kind != "process_completed")
        {
            continue;
        }

        bool success = root.TryGetProperty("success", out JsonElement ok) && ok.ValueKind == JsonValueKind.True;
        root.TryGetProperty("output", out JsonElement output);

        if (!success)
        {
            string? error = output.ValueKind == JsonValueKind.Object
                && output.TryGetProperty("error", out JsonElement errorElement)
                    ? errorElement.GetString()
                    : null;

            throw new InvalidOperationException(error ?? "the Space reported a failure without a message.");
        }

        return ExtractFileRef(output);
    }

    throw new InvalidOperationException("the Space closed the stream without returning a video.");
}

static string ExtractFileRef(JsonElement output)
{
    if (output.ValueKind == JsonValueKind.Object
        && output.TryGetProperty("data", out JsonElement data)
        && data.ValueKind == JsonValueKind.Array
        && data.GetArrayLength() > 0)
    {
        JsonElement first = data[0];

        // Spaces return either a bare file object or one nested under "video".
        if (first.ValueKind == JsonValueKind.Object
            && first.TryGetProperty("video", out JsonElement nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            first = nested;
        }

        if (first.ValueKind == JsonValueKind.Object)
        {
            foreach (string key in new[] { "url", "path" })
            {
                if (first.TryGetProperty(key, out JsonElement value) && value.GetString() is { Length: > 0 } reference)
                {
                    return reference;
                }
            }
        }
    }

    throw new InvalidOperationException($"could not find a file in the response: {output}");
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
