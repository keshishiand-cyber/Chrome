#pragma warning disable OPENAI001 // Image generation options are marked experimental by the SDK.

using System.Text.Json;
using OpenAI.Chat;
using OpenAI.Images;

string? openaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
string? cloudflareApiToken = Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN");
string? cloudflareAccountId = Environment.GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID");

if (string.IsNullOrEmpty(openaiApiKey))
{
    Console.WriteLine("Set the OPENAI_API_KEY environment variable before running this app.");
    return;
}

ChatClient chatClient = new(model: "gpt-4o-mini", apiKey: openaiApiKey);
ImageClient imageClient = new(model: "dall-e-3", apiKey: openaiApiKey);

// Available image generation models:
// - dall-e-3: OpenAI's DALL-E 3
// - @cf/stabilityai/stable-diffusion-xl-base-1.0: Stable Diffusion XL Base (requires CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID)
string currentImageModel = "dall-e-3";
HttpClient httpClient = new();

Console.WriteLine("Chat with OpenAI (type 'exit' to quit, '/image <prompt>' to generate an image, or '/model' to switch models)");
while (true)
{
    Console.Write("> ");
    string? input = Console.ReadLine();
    if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (input.Equals("/model", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Available image generation models:");
        Console.WriteLine("1. dall-e-3 - OpenAI's DALL-E 3");
        if (!string.IsNullOrEmpty(cloudflareApiToken) && !string.IsNullOrEmpty(cloudflareAccountId))
        {
            Console.WriteLine("2. stable-diffusion-xl - Stable Diffusion XL Base (@cf/stabilityai/stable-diffusion-xl-base-1.0)");
        }
        else
        {
            Console.WriteLine("2. stable-diffusion-xl - [UNAVAILABLE - Set CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID]");
        }
        Console.Write("Select model (1 or 2): ");
        string? modelChoice = Console.ReadLine();

        if (modelChoice == "1")
        {
            currentImageModel = "dall-e-3";
            imageClient = new(model: "dall-e-3", apiKey: openaiApiKey);
            Console.WriteLine("Switched to DALL-E 3");
        }
        else if (modelChoice == "2")
        {
            if (string.IsNullOrEmpty(cloudflareApiToken) || string.IsNullOrEmpty(cloudflareAccountId))
            {
                Console.WriteLine("Error: Stable Diffusion XL requires CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID environment variables.");
            }
            else
            {
                currentImageModel = "@cf/stabilityai/stable-diffusion-xl-base-1.0";
                Console.WriteLine("Switched to Stable Diffusion XL Base");
            }
        }
        else
        {
            Console.WriteLine("Invalid choice. Model unchanged.");
        }
        continue;
    }

    if (input.StartsWith("/image ", StringComparison.OrdinalIgnoreCase))
    {
        string imagePrompt = input["/image ".Length..];
        Console.WriteLine($"Generating image with {currentImageModel}...");

        try
        {
            if (currentImageModel == "dall-e-3")
            {
                // Use OpenAI API for DALL-E 3
                ImageGenerationOptions options = new()
                {
                    Size = GeneratedImageSize.W1024xH1024,
                    ResponseFormat = GeneratedImageFormat.Bytes,
                };

                GeneratedImage image = imageClient.GenerateImage(imagePrompt, options);
                string fileName = $"image-{DateTime.Now:yyyyMMdd-HHmmss}.png";
                File.WriteAllBytes(fileName, image.ImageBytes.ToArray());
                Console.WriteLine($"Saved to {fileName}");
            }
            else if (currentImageModel == "@cf/stabilityai/stable-diffusion-xl-base-1.0")
            {
                // Use Cloudflare Workers AI API for Stable Diffusion XL
                if (string.IsNullOrEmpty(cloudflareApiToken) || string.IsNullOrEmpty(cloudflareAccountId))
                {
                    Console.WriteLine("Error: Cloudflare credentials not configured.");
                    continue;
                }

                await GenerateImageWithCloudflare(httpClient, cloudflareAccountId, cloudflareApiToken, imagePrompt);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating image: {ex.Message}");
        }
        continue;
    }

    ChatCompletion completion = chatClient.CompleteChat(input);
    Console.WriteLine(completion.Content[0].Text);
}

async Task GenerateImageWithCloudflare(HttpClient client, string accountId, string apiToken, string prompt)
{
    try
    {
        var requestBody = new
        {
            prompt = prompt
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

        string url = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/ai/run/@cf/stabilityai/stable-diffusion-xl-base-1.0";
        HttpResponseMessage response = await client.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
            string fileName = $"image-{DateTime.Now:yyyyMMdd-HHmmss}.png";
            File.WriteAllBytes(fileName, imageBytes);
            Console.WriteLine($"Saved to {fileName}");
        }
        else
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Cloudflare API error ({response.StatusCode}): {errorContent}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error calling Cloudflare API: {ex.Message}");
    }
}
