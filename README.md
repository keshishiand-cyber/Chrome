# Chrome

A small .NET console app that chats with OpenAI, generates images, and generates videos —
using free generation backends wherever one genuinely exists.

## Setup

### Images — three options

`/image <prompt>` with no keys set uses the legacy, unauthenticated
[Pollinations](https://pollinations.ai) endpoint (`image.pollinations.ai`). No signup, no
key, no cost, no practical limit. That endpoint only offers one model, Sana.

Set `POLLINATIONS_API_KEY` and `/image` switches to `gen.pollinations.ai`, which carries
the full catalogue — `flux` (FLUX.1-schnell), `zimage`, `krea`, `qwen-image`,
`nanobanana` and more. This bills Pollen, but very little: flux is **0.002 Pollen per
image**, roughly five hundred images per Pollen. Pick the model with:

```
export POLLINATIONS_IMAGE_MODEL=zimage   # default: flux
```

Get a key at [enter.pollinations.ai/keys](https://enter.pollinations.ai/keys). A zero
balance fails clearly rather than silently:

```
Image generation failed: 402 ... This request costs ~0.0020 pollen,
but your available balance is 0.0000.
```

`/flux <prompt>` runs [FLUX.1-schnell](https://huggingface.co/black-forest-labs/FLUX.1-schnell)
on Black Forest Labs' own [ZeroGPU Space](https://huggingface.co/spaces/black-forest-labs/FLUX.1-schnell).
Same model as Pollinations' `flux`, but paid for with the free `HF_TOKEN` ZeroGPU
allowance instead of Pollen — so it needs no balance at all.

Avoid routing FLUX.1-schnell through Hugging Face's Inference Providers: that path bills
third-party providers against your monthly inference credit, which a free account exhausts
after roughly a single image (`402: You have depleted your monthly included credits`).

### Video — free, with a free Hugging Face token

`/video <prompt>` paints an opening frame on Pollinations (free), then animates it with
the [Wan 2.2 image-to-video Space](https://huggingface.co/spaces/zerogpu-aoti/wan2-2-fp8da-aoti-faster)
on Hugging Face [ZeroGPU](https://huggingface.co/docs/hub/spaces-zerogpu). No payment is
involved, but ZeroGPU rejects anonymous API calls, so you need a free token:

1. Create a token at [huggingface.co/settings/tokens](https://huggingface.co/settings/tokens) (read scope is enough).
2. `export HF_TOKEN=hf_...`

Clips are short (1.5s by default) and that is deliberate. ZeroGPU sizes its GPU
reservation from the requested duration and step count, and a free account cannot reserve
the 200s+ that full-length text-to-video models ask for — those requests are refused
outright with a quota error no matter how much daily allowance is left. Keeping the clip
short is what makes the free tier usable at all.

Free accounts get 5 minutes of GPU per day and medium queue priority, so expect a handful
of short clips per day rather than unlimited video. When the allowance runs out the app
prints Hugging Face's own message, including when it refills:

```
Video generation failed: You have exceeded your free ZeroGPU quota
(200s requested vs. 294s left). Try again in 23:46:30.
```

To point at a different Space (community Spaces break fairly often), set:

```
export HF_VIDEO_SPACE=owner/space-name   # default: zerogpu-aoti/wan2-2-fp8da-aoti-faster
```

It must expose a `generate_video` endpoint taking an image first.

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

Type a message to chat, `/image <prompt>` or `/flux <prompt>` to generate an image, `/video <prompt>` to
generate a video, or `exit` to quit. Output files are written to the current directory.
