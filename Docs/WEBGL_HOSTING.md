# WebGL hosting (Cloudflare Pages + R2)

The hosted demo is split in two so neither host's limits get in the way:

| Piece | Where | What |
| --- | --- | --- |
| Landing page, `demo/index.html`, `TemplateData/`, instructor dashboard | Cloudflare Pages project `counselcue` | Small static files (about 2 MB). |
| `Build/` (loader, framework, wasm, data) | R2 bucket `counselcue-webgl-assets`, key `v<N>/Build/<file>` | About 26 MB, Brotli-compressed with `decompressionFallback`. |

The API worker (`counselcue-api`) binds the bucket as `WEBGL_BUCKET` and serves it at
`GET /webgl/<version>/Build/<file>`. Keys must match `v<N>/Build/<name>`; anything else is a
404. Responses carry CORS for the allowed origins and `Cache-Control: public, max-age=31536000,
immutable`, so a version is never overwritten: upload a new build under a new version.

## How the page picks the build

`Assets/WebGLTemplates/CounselCue/index.html` (marker `CC_BUILD_SOURCE`) sets `buildUrl`:

- default: `https://counselcue-api.jewoong-moon.workers.dev/webgl/<CC_BUILD_VERSION>/Build`
- `?build=v3`: another uploaded version, for trying a build before making it the default
- `?build=local`, `localhost` or `file://`: the `Build` folder next to the page (local testing)

The `.unityweb` files are served as `application/octet-stream` without `Content-Encoding`;
Unity's loader decompresses them in JavaScript (`decompressionFallback`).

## Releasing a new build

1. Build in the editor (`CounselingRoomBuilder.BuildWebGLInEditor`).
2. In the Cloudflare dashboard, R2 → `counselcue-webgl-assets` → upload the four files from
   `Builds/WebGL/Build/` under the prefix `v<N+1>/Build/`.
3. Check `https://counselcue.pages.dev/demo/?build=v<N+1>`.
4. Set `CC_BUILD_VERSION` to `v<N+1>` in the template (and in the deployed `demo/index.html`),
   then deploy Pages with everything except `demo/Build/`.

Old versions can stay in the bucket so earlier links keep working.
