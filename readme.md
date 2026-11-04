# IMApi

Converts product images stored in **OpenCloud** with [Magick.NET](https://github.com/dlemstra/Magick.NET)
(resize, soft alpha edge, drop shadow, image text) and manages the image texts per
Artikelnummer and language.

It runs as a backend container of [opencloud-compose](https://github.com/protronic/opencloud-compose)
(`imapi/imapi.yml`). The OpenCloud web extension `imapi` (in that repository) adds a right-click
action „Bild(er) konvertieren“ to images and the app „Bildtexte“.

## How it works

```
Browser (OpenCloud web, extension "imapi")
   │  https://<oc-domain>/imapi/api/...   Authorization: Bearer <user token>
   ▼
OpenCloud proxy ── authenticates, forwards /imapi/ (config/opencloud/proxy.yaml)
   ▼
IMApi container ── checks the token (graph/v1.0/me), then with the same token:
   │                GET original, MKCOL + PUT results (WebDAV)
   ▼
OpenCloud (http://opencloud:9200/dav/spaces/...)
```

IMApi stores no credentials and has no own user management: every file access happens with the
permissions of the user who clicked. The only state is an SQLite database with the image texts
(`/data/imapi.db` in the container).

Converted images are written next to the original, by default into
`konvertiert/<Sprache>/<Preset>/<Name>.<Format>`.

### File names

`{Artikelnummer}[_{Index}][_{Sprache}].{ext}`, e.g. `1017000034.png`, `1017000034_1.png`,
`1017000034_EN.png`. The Artikelnummer selects the stored texts; a language suffix limits the
conversion to that language, without one all languages of the selected presets are produced.
Other file names are converted too, without stored texts.

### Presets

| Preset | Size | Background | Languages |
|---|---|---|---|
| web | 500×500 | white | DE, EN |
| thumb | 100×100 | white | DE |
| 100x100 | 100×100 | transparent | DE, EN |
| 200x200 | 200×200 | transparent | DE, EN |
| 2000x2000 | 2000×2000 | transparent | DE |
| max | original | transparent | DE |

Own presets and languages: `config/imapi.json` (see `imapi/imapi.json.example` in
opencloud-compose, mounted via `IMAPI_CONFIG_FILE`).

## API

All endpoints below `/imapi/api` need an OpenCloud login (Authorization header). Swagger UI:
`/imapi/swagger`.

| Method | Path | |
|---|---|---|
| GET | `/api/config` | languages, presets, default output folder |
| POST | `/api/convert` | `{ files: [{ spaceId, path }], presets?, languages?, applyText, texts?, outputFolder? }` |
| GET | `/api/texts?q=` | articles with texts |
| GET/PUT/DELETE | `/api/texts/{artikelnummer}` | `PUT { texts: { DE: "...", EN: "..." } }`, empty text removes a language |
| GET | `/api/describe?name=...` | what file names say (Artikelnummer, language) plus stored texts |
| GET | `/health` | health check, no login |

## Configuration

Section `ImApi` in `appsettings.json`, `config/imapi.json` or environment variables
(`ImApi__OpenCloudUrl`, ...):

| Setting | Default | |
|---|---|---|
| `OpenCloudUrl` | `http://opencloud:9200` | OpenCloud as seen from the container |
| `OpenCloudInsecure` | `false` | accept self-signed certificates |
| `PathBase` | `/imapi` | prefix the OpenCloud proxy forwards |
| `DatabasePath` | `data/imapi.db` (`/data/imapi.db` in the image) | SQLite database |
| `LabelFont` | `Arial` (Liberation Sans in the image) | font name or font file |
| `DefaultOutputFolder` | `konvertiert` | relative to the original, or from the space root with `/` |
| `Languages`, `Presets` | see above | |
| `MaxSourceBytes` | 200 MB | largest original |
| `MaxParallelConversions` | 2 | memory bound |

## Container image

GitHub Actions (`.github/workflows/docker.yml`) tests and publishes `ghcr.io/protronic/imapi`:
`latest`/`master` from the master branch, `1.2.3`/`1.2` from tags `v1.2.3`. The image listens on
port 8080, runs as 1000:1000 and keeps its data in `/data`.

```bash
docker build -t imapi .
```

## Development

Requires the .NET 10 SDK.

```bash
dotnet watch                              # http://localhost:5221/imapi/swagger
dotnet test --project tests/IMApi.Tests
dotnet ef migrations add <Name>           # after model changes; applied on startup
```

For a local OpenCloud set `ImApi__OpenCloudUrl` (e.g. `https://cloud.opencloud.test`) and
`ImApi__OpenCloudInsecure=true`.
