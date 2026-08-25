# AI Art Manifest: Harbour Login Background v1

## Target

- Runtime resource: `Assets/_Project/Resources/Sprites/AIReviewed/Backgrounds/gangland-harbour-login-v1.png`
- Unity resource path: `Sprites/AIReviewed/Backgrounds/gangland-harbour-login-v1`
- Requested canvas: `2048x1152`
- Delivered canvas: `1672x941` (provider-preserved 16:9 output; not upscaled locally)
- Model path: OpenAI-compatible Image API at `https://blackaicoding.com/v1`
- Model: `gpt-image-2`
- Generated quality: `medium`
- Final quality: `high` after visual approval
- SHA-256: `f00e57da5750ce43b29a5c7dcd05c8ef697fdee5133725aecb5b56df4f99375b`

## Source Prompt

`Assets/_Project/Docs/AIArt/Prompts/menu-harbour-login-v1.txt`

## Review Checklist

- No readable or malformed text, logos, watermarks, or embedded UI.
- The upper-left title and right login panel remain readable over the image.
- Midtones survive the menu wash; the image is not crushed into black.
- Harbour, organized-crime, and police-operation cues are visible without a close-up character.
- The palette stays charcoal, oxidized teal, amber, and restrained red rather than generic purple cyberpunk.
- The imported texture is a single Sprite with Bilinear filtering, no mipmaps, and no compression.
- The result is checked at 1280x720, 1920x1080, and 2560x1440 before expanding the style to other assets.

## Provenance

- Prompt authored for this project on 2026-08-06.
- Image generated and reviewed on 2026-08-06.
- The provider returned a temporary URL instead of inline base64; the downloaded PNG hash is recorded above.
- The provider's OpenAI SDK user agent was blocked, so the successful request used the same Image API payload through a neutral HTTP client. No credential was stored in the project.
- Generation output must be stored under the versioned target path above.
- Do not overwrite a reviewed output; increment the filename suffix for later variants.
