# Chrome

A small .NET console app that chats with OpenAI and generates images.

## Setup

- Chat requires an OpenAI API key: `export OPENAI_API_KEY=...`
- Image generation (`/image <prompt>`) needs **no API key** — it uses the free,
  unauthenticated [Pollinations.ai](https://pollinations.ai) image API. Chat is
  optional; images work even without `OPENAI_API_KEY` set.

## Run

```
dotnet run
```

Type a message to chat, `/image <prompt>` to generate and save an image, or
`exit` to quit.

