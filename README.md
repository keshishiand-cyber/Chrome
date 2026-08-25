# Chrome

A small .NET console app that chats with OpenAI, generates images, and generates videos —
using free generation backends wherever one genuinely exists.

## Setup

### Images — free, no key at all

`/image <prompt>` uses the unauthenticated
[Pollinations.ai](https://pollinations.ai) image API. No signup, no key, no cost, no
practical limit. Works with no environment variables set.

### Video — free, but heavily rate limited

`/video <prompt>` runs a text-to-video model on a Hugging Face
[ZeroGPU](https://huggingface.co/docs/hub/spaces-zerogpu) Space. No payment is involved,
but ZeroGPU rejects anonymous API calls, so you need a free token:

1. Create a token at [huggingface.co/settings/tokens](https://huggingface.co/settings/tokens) (read scope is enough).
2. `export HF_TOKEN=hf_...`

**Be aware of the real limits before relying on this.** ZeroGPU grants a free account
**5 minutes of GPU per day**, and current video models request **200–300 seconds for a
single clip**. One generation can consume most or all of a day's allowance, and requests
are rejected outright when the remaining budget is too small. Free accounts also get
medium queue priority, so a clip can sit in the queue for a long time. When quota runs
out the app prints Hugging Face's own message, including when the allowance refills:

```
Video generation failed: You have exceeded your free ZeroGPU quota
(200s requested vs. 294s left). Try again in 23:46:30.
```

Community Spaces also break fairly often (missing model weights, app-side exceptions), so
you can point the app at a different one:

```
export HF_VIDEO_SPACE=Phamthihong/LTX-2.3-turbo   # default: JoseAQ/Video_Action
```

The Space must expose a `generate_video` endpoint with the LTX-2.3 parameter signature.

### Video fallback — paid

If `HF_TOKEN` isn't set but `POLLINATIONS_API_KEY` is, `/video` falls back to
[gen.pollinations.ai](https://gen.pollinations.ai) video models (Veo, Seedance, Wan).
This costs Pollen credits — roughly 0.4 Pollen per short clip, while a free registered
account receives only about 1.5 Pollen/week. Treat it as a paid path.

### Chat — needs an OpenAI key

`export OPENAI_API_KEY=...`. Chat is optional; images and video work without it.

## Run

```
dotnet run
```

Type a message to chat, `/image <prompt>` to generate an image, `/video <prompt>` to
generate a video, or `exit` to quit. Output files are written to the current directory.
