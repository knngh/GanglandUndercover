# AI Art Manifest: Harbour Map Preview v1

## Target

- Runtime resource: `Assets/_Project/Resources/Sprites/AIReviewed/MapPreviews/gangland-harbour-map-preview-v1.png`
- Unity resource path: `Sprites/AIReviewed/MapPreviews/gangland-harbour-map-preview-v1`
- Requested canvas: `2048x1152`
- Delivered canvas: `1672x941` (provider-preserved 16:9 output; not upscaled locally)
- Model path: OpenAI-compatible Image API at `https://blackaicoding.com/v1`
- Model: `gpt-image-2`
- Generated quality: `medium`
- Final quality: `high` after visual approval
- SHA-256: `a98eba634e85b2e4c4f2b113fa2cf831aacab9bcbadace6c4c7a9a0234ba55a7`

## Source Prompt

`Assets/_Project/Docs/AIArt/Prompts/menu-harbour-map-preview-v1.txt`

## Review Checklist

- No readable or malformed text, logos, watermarks, embedded UI, or close-up characters.
- Main road, cargo yard, market-side structures, checkpoint activity, and harbour depth remain readable at thumbnail size.
- The palette matches the reviewed login background: charcoal, oxidized teal, amber, and restrained signal red.
- The scene reads as a playable investigation district rather than a generic cyberpunk skyline or empty alley.
- The lower edge remains subdued enough for menu framing without crushing the location into black.
- The imported texture is a single Sprite with Bilinear filtering, no mipmaps, and no compression.
- The menu result is checked at 1280x720, 1920x1080, and 2560x1440 before expanding the style to other location plates.

## Provenance

- Prompt authored for this project on 2026-08-06.
- Image generated and reviewed on 2026-08-07.
- The provider returned a temporary URL instead of inline base64; the downloaded PNG hash is recorded above.
- The provider's OpenAI SDK user agent is blocked, so the request used the same Image API payload through a neutral HTTP client. No credential is stored in the project.
- Generation output is stored under the versioned target path above.
- Do not overwrite this reviewed output; increment the filename suffix for later variants.
