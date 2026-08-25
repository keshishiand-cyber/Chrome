# Chrome

A small .NET console app that chats with OpenAI, generates images, and generates videos.

## Setup

- Chat requires an OpenAI API key: `export OPENAI_API_KEY=...`
- Image generation (`/image <prompt>`) needs **no API key** — it uses the free,
  unauthenticated [Pollinations.ai](https://pollinations.ai) image API. Images
  work even without any environment variables set.
- Video generation (`/video <prompt>`) uses Pollinations' video models (Veo,
  Seedance, Wan, etc.). Unlike images, this endpoint **requires an API key**:
  1. Get a free key at [enter.pollinations.ai/keys](https://enter.pollinations.ai/keys).
  2. `export POLLINATIONS_API_KEY=sk_...`
  3. Free registered accounts get a small weekly Pollen credit grant (about
     1.5 Pollen/week) — enough for roughly one short clip before you hit the
     limit and need to wait for the next grant or buy more Pollen.

  If `POLLINATIONS_API_KEY` isn't set, `/video` is disabled with a message
  pointing you to the signup page; everything else still works.

## Run

```
dotnet run
```

Type a message to chat, `/image <prompt>` to generate and save an image,
`/video <prompt>` to generate and save a video, or `exit` to quit.

