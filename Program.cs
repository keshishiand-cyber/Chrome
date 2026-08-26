#pragma warning disable OPENAI001 // Image generation options are marked experimental by the SDK.

using OpenAI.Chat;
using OpenAI.Images;

string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Set the OPENAI_API_KEY environment variable before running this app.");
    return;
}

ChatClient chatClient = new(model: "gpt-4o-mini", apiKey: apiKey);
ImageClient imageClient = new(model: "dall-e-3", apiKey: apiKey);

// Available image generation models:
// - dall-e-3: OpenAI's DALL-E 3
// - @cf/stabilityai/stable-diffusion-xl-base-1.0: Stable Diffusion XL Base
string currentImageModel = "dall-e-3";

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
        Console.WriteLine("2. stable-diffusion-xl - Stable Diffusion XL Base (@cf/stabilityai/stable-diffusion-xl-base-1.0)");
        Console.Write("Select model (1 or 2): ");
        string? modelChoice = Console.ReadLine();

        if (modelChoice == "1")
        {
            currentImageModel = "dall-e-3";
            imageClient = new(model: "dall-e-3", apiKey: apiKey);
            Console.WriteLine("Switched to DALL-E 3");
        }
        else if (modelChoice == "2")
        {
            currentImageModel = "@cf/stabilityai/stable-diffusion-xl-base-1.0";
            imageClient = new(model: "@cf/stabilityai/stable-diffusion-xl-base-1.0", apiKey: apiKey);
            Console.WriteLine("Switched to Stable Diffusion XL Base");
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

        ImageGenerationOptions options = new()
        {
            Size = GeneratedImageSize.W1024xH1024,
            ResponseFormat = GeneratedImageFormat.Bytes,
        };

        try
        {
            GeneratedImage image = imageClient.GenerateImage(imagePrompt, options);
            string fileName = $"image-{DateTime.Now:yyyyMMdd-HHmmss}.png";
            File.WriteAllBytes(fileName, image.ImageBytes.ToArray());
            Console.WriteLine($"Saved to {fileName}");
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
