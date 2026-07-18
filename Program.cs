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
ImageClient imageClient = new(model: "gpt-image-1", apiKey: apiKey);

Console.WriteLine("Chat with OpenAI (type 'exit' to quit, or '/image <prompt>' to generate an image)");
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
        ImageGenerationOptions options = new()
        {
            Size = GeneratedImageSize.W1024xH1024,
        };

        GeneratedImage image = imageClient.GenerateImage(imagePrompt, options);
        string fileName = $"image-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        File.WriteAllBytes(fileName, image.ImageBytes.ToArray());
        Console.WriteLine($"Saved to {fileName}");
        continue;
    }

    ChatCompletion completion = chatClient.CompleteChat(input);
    Console.WriteLine(completion.Content[0].Text);
}
