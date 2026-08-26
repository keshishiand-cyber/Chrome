# Chrome - MiniMax-M3 Integration

A C# console application that integrates **MiniMax-M3** model via HuggingFace Inference API for interactive chat.

## Features

- 💬 Interactive chat with MiniMax-M3 
- 🤖 Powered by MiniMax-M3 (1.0M token context window)
- 🔌 HuggingFace Inference API integration
- 🎯 Easy setup and configuration

## Setup

### Prerequisites

- .NET 8.0 or higher
- HuggingFace API key

### Installation

1. **Get your HuggingFace API key**
   - Visit https://huggingface.co/settings/tokens
   - Create a new API token with read permissions

2. **Set the environment variable**
   ```bash
   export HUGGINGFACE_API_KEY="your_api_key_here"
   ```

3. **Build and run**
   ```bash
   dotnet build
   dotnet run
   ```

## Usage

```
Chat with MiniMax-M3 via HuggingFace (type 'exit' to quit)
> Hello, how are you?
[Response from MiniMax-M3]
> 
```

## Model Details

- **Model**: MiniMaxAI/MiniMax-M3
- **Provider**: HuggingFace Inference API
- **Context Window**: 1.0M tokens
- **Pricing**: $0.10/month credit on free tier (HuggingFace)

## Configuration

You can adjust model parameters in `Program.cs`:
- `MaxNewTokens`: Maximum tokens in response (default: 512)
- `Temperature`: Response creativity (default: 0.7)

## License

MIT
