# Chrome

A small .NET console app that chats with OpenAI, generates images, and generates videos —
using free generation backends wherever one genuinely exists.

## Setup

### Images — free, no key at all

`/image <prompt>` uses the unauthenticated
[Pollinations.ai](https://pollinations.ai) image API. No signup, no key, no cost.
Works with no environment variables set.

### Video — free, but needs a free Hugging Face token

`/video <prompt>` generates a starting frame on Pollinations (free), then animates it
with the [Wan 2.2 image-to-video Space](https://huggingface.co/spaces/zerogpu-aoti/wan2-2-fp8da-aoti-faster)
on Hugging Face's free **ZeroGPU** tier.

No payment is ever involved, but ZeroGPU rejects anonymous API calls, so you need a token:

1. Create a free token at [huggingface.co/settings/tokens](https://huggingface.co/settings/tokens) (read scope is enough).
2. `export HF_TOKEN=hf_...`

Free ZeroGPU quota is time-based and refills; if you exhaust it, `/video` says so and you
wait rather than pay. Generation takes a few minutes.

### Video fallback — paid

If `HF_TOKEN` isn't set but `POLLINATIONS_API_KEY` is, `/video` falls back to
[gen.pollinations.ai](https://gen.pollinations.ai) video models (Veo, Seedance, Wan).
This costs Pollen credits (roughly 0.4 Pollen per short clip); a free registered account
receives only about 1.5 Pollen/week, so this is effectively a paid path.

### Chat — needs an OpenAI key

`export OPENAI_API_KEY=...`. Chat is optional; images and video work without it.

## Run

```
dotnet run
```

Type a message to chat, `/image <prompt>` to generate an image, `/video <prompt>` to
generate a video, or `exit` to quit. Output files are written to the current directory.
