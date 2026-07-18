using OpenAI.Chat;

string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Set the OPENAI_API_KEY environment variable before running this app.");
    return;
}

ChatClient client = new(model: "gpt-4o-mini", apiKey: apiKey);

Console.WriteLine("Chat with OpenAI (type 'exit' to quit)");
while (true)
{
    Console.Write("> ");
    string? input = Console.ReadLine();
    if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    ChatCompletion completion = client.CompleteChat(input);
    Console.WriteLine(completion.Content[0].Text);
}
