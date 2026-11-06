# GHOSTS PANDORA

**GHOSTS Pandora** is a unified web server that combines social media simulation with dynamic content generation capabilities. This server responds to a myriad of request types with randomized content generated in real-time, making it ideal for cybersecurity training, exercises, and simulations. It can be configured to be a realistic website, a social media platform, or simply a content generator that serves diverse file types on demand.

## What is Pandora?

Pandora is a **multi-mode** server that can operate as:

1. **Social Media Platform**: A full-featured social network (Facebook, Instagram, Twitter/X, Reddit themes) with posts, users, comments, likes, direct messaging, and real-time updates
2. **Dynamic Website**: A realistic website (news site, shopping site, sports site, entertainment site) with dynamically generated content
3. **Content Generator**: Responds to any file request with dynamically generated, realistic content (PDFs, Office docs, images, videos, audio, executables, archives, and more)

Originally two separate applications (**GHOSTS Pandora** for content generation and **GHOSTS Socializer** for social media), they are now **unified into a single application** that provides both capabilities seamlessly.

### GHOSTS Pandora + Socializer = One Unified Platform

| Feature | Old: GHOSTS Pandora (Python) | Old: GHOSTS Socializer | **New: Unified Pandora** |
|---------|------------------------------|------------------------|----------------------------|
| Dynamic content generation | ✅ | ❌ | ✅ |
| Social media simulation | ❌ | ✅ | ✅ |
| Website simulation | ❌ | ❌ | ✅ |
| Payload delivery | ✅ | ❌ | ✅ |
| Multiple themes | ❌ | ✅ | ✅ |
| Mode switching | ❌ | ❌ | ✅ |
| Technology | Python/FastAPI | C#/.NET | C#/.NET |
| **Status** | **Deprecated** | **Deprecated** | **✅ Active** |

### Use Cases

- **Cybersecurity Training**: Simulate realistic web traffic and file downloads for training exercises
- **Red Team Operations**: Generate realistic-looking documents, websites, and payloads
- **NPC Simulation**: Used with [GHOSTS NPCs](https://github.com/cmu-sei/GHOSTS) to provide realistic browsing and download behaviors
- **Social Engineering Testing**: Test user responses to various social media platforms and content types
- **Malware Analysis**: Serve controlled payloads in a safe training environment

Typical install is via Docker.

## Features

### Mode Switching
- **Social Media Mode**: Full-featured social network with posts, users, comments, likes, themes
- **Website Mode**: Realistic website homepage (news site, shopping site, sports site, entertainment site)
- Switch between modes via configuration without code changes

### Social Media Functionality (Social Mode)
- User registration and authentication
- Post creation, likes, and comments
- Direct messaging between users
- User profiles and following/followers
- Customizable themes (Facebook, Instagram, Reddit, etc.)
- Real-time updates via SignalR

### Website Simulation (Website Mode)
- News site layout with articles, featured stories, trending sections
- Shopping site with product grids, categories, search
- Sports and entertainment site variants
- All content dynamically generated

### Payload Delivery
- Configure specific URLs to serve specific files
- Store payload files in `Payloads` directory
- Useful for cybersecurity training and red team exercises
- List and verify configured payloads via API

### Pandora Dynamic Content Generation

This allows the server to respond to **any file request** with dynamically generated, realistic content. Used in conjunction with [GHOSTS NPCs](https://github.com/cmu-sei/GHOSTS), agents can periodically download diverse content types beyond simple HTML, including documents, images, videos, and executables.

#### How It Works

Content can be requested in **two ways**:

**1. By Directory** - Requests to specific directories automatically return that file type:
- `/pdf/*` - Any URL starting with `/pdf` returns a PDF
- `/doc/*` or `/docs/*` - Returns Word documents
- `/xlsx/*` or `/sheets/*` - Returns Excel spreadsheets
- `/ppt/*` or `/slides/*` - Returns PowerPoint presentations
- `/img/*` or `/images/*` or `/i/*` - Returns random images
- `/json/*` or `/api/*` - Returns JSON data
- `/csv/*` - Returns CSV files
- `/video/*` or `/videos/*` - Returns video files
- `/audio/*` or `/voice/*` or `/call/*` - Returns audio files

**2. By File Extension** - Any URL with a recognized file extension returns that file type:
- `/reports/quarterly-summary.pdf` → Returns a PDF
- `/documents/memo.docx` → Returns a Word document
- `/data/users.json` → Returns JSON
- `/media/video.mp4` → Returns a video file
- `/downloads/installer.exe` → Returns an executable

All content is **generated in real-time** with randomized but realistic data. Content can optionally be cached for repeat requests.

#### Supported File Types

**Documents:**
- PDF (`.pdf`) - Multi-page documents with random text
- Word Documents (`.doc`, `.docx`) - Formatted text documents
- Excel Spreadsheets (`.xls`, `.xlsx`) - Tables with random data
- PowerPoint Presentations (`.ppt`, `.pptx`) - Multi-slide presentations
- OneNote Notebooks (`.one`) - OneNote files

**Images:**
- PNG, JPEG, GIF (`.png`, `.jpg`, `.jpeg`, `.gif`) - Random geometric images with text

**Data Formats:**
- JSON (`.json`) - Structured data with random records
- CSV (`.csv`) - Tabular data
- Plain Text (`.txt`) - Random paragraphs
- HTML (`.html`, `.htm`) - Complete web pages with links, images, CSS, and JavaScript

**Web Resources:**
- JavaScript (`.js`) - Random scripts
- CSS (`.css`) - Random stylesheets

**Archives:**
- ZIP (`.zip`) - Compressed archives with multiple files
- TAR (`.tar`) - TAR archives

**Video:**
- MP4 (`.mp4`, `.avi`, `.mov`, `.mkv`, `.webm`) - Fallback video files

**Audio:**
- WAV (`.wav`) - Uncompressed audio with sine wave
- MP3 (`.mp3`) - Compressed audio files
- OGG (`.ogg`) - OGG audio files
- M4A (`.m4a`) - AAC audio files

**Binary/Executables:**
- Binary (`.bin`) - Random binary data
- Executable (`.exe`) - Fake Windows executables
- MSI Installer (`.msi`) - Fake Windows installer packages
- ISO Disk Image (`.iso`) - Fake disk images

## Usage

### Directory-Based Requests

Access content by directory paths:

- `/pdf/reports/quarterly-report` → Returns a PDF
- `/doc/memos/staff-memo` → Returns a Word document
- `/xlsx/data/sales-data` → Returns an Excel spreadsheet
- `/img/photos/profile` → Returns a random image
- `/json/api/users` → Returns JSON data
- `/csv/exports/employees` → Returns CSV data
- `/video/clips/presentation` → Returns an MP4 video
- `/audio/recordings/meeting` → Returns a WAV audio file
- `/exe/downloads/installer` → Returns a fake executable
- `/onenote/notebooks/project-notes` → Returns a OneNote file

### File Extension-Based Requests

Request any path with a supported file extension:

- `/documents/report.pdf` → Returns a PDF
- `/files/data.xlsx` → Returns an Excel spreadsheet
- `/resources/image.png` → Returns an image
- `/data/users.json` → Returns JSON
- `/media/video.mp4` → Returns a video file
- `/recordings/call.wav` → Returns an audio file
- `/downloads/setup.exe` → Returns a fake executable
- `/software/installer.msi` → Returns a fake MSI installer
- `/disks/backup.iso` → Returns a fake ISO disk image

### API Endpoints

Direct endpoint access:

**Documents:**
- `GET /pdf` or `GET /pdf/{path}`
- `GET /doc` or `GET /doc/{path}`
- `GET /xlsx` or `GET /xlsx/{path}`
- `GET /ppt` or `GET /ppt/{path}`
- `GET /onenote` or `GET /onenote/{path}`

**Images:**
- `GET /img` or `GET /img/{path}`
- `GET /images` or `GET /images/{path}`

**Data:**
- `GET /json` or `GET /json/{path}`
- `GET /csv` or `GET /csv/{path}`
- `GET /text` or `GET /text/{path}`
- `GET /html` or `GET /html/{path}`

**Media:**
- `GET /video` or `GET /video/{path}`
- `GET /audio` or `GET /audio/{path}`
- `GET /voice` or `GET /voice/{path}`
- `GET /call` or `GET /call/{path}`

**Binary/Executables:**
- `GET /bin` or `GET /bin/{path}`
- `GET /exe` or `GET /exe/{path}`
- `GET /msi` or `GET /msi/{path}`
- `GET /iso` or `GET /iso/{path}`

**Archives:**
- `GET /zip` or `GET /zip/{path}`
- `GET /tar` or `GET /tar/{path}`

**Web:**
- `GET /js` or `GET /js/{path}`
- `GET /css` or `GET /css/{path}`

### Info Endpoint

`GET /pandora/about` - Returns information about Pandora capabilities

### Seeding Social Content

In social mode, posts and profiles can be loaded by script, for example to give a scenario its
history before it starts. Both endpoints create the user if it does not exist, and take the theme
from the `theme` query parameter, then the `theme` cookie, then `default` (not `DEFAULT_THEME`), so
pass `?theme=` to choose the site the content shows up on.

`POST /api/posts` creates a post. Form fields:

| Field | Description |
|-------|-------------|
| `user` | Username of the author (required) |
| `message` | Text of the post (required) |
| `created` | Optional ISO-8601 date to backdate the post, read as UTC unless it carries an offset. A value that is not a date is refused with `400`. |
| `file` | Optional image to attach |

```bash
curl -X POST 'http://localhost:5000/api/posts?theme=linkedin' \
  -d user=jsmith -d 'message=Starting a new role this week' -d created=2025-03-01T14:30:00Z
```

A post is dropped as a duplicate when the same user already has the same message dated no more than
`MinutesToCheckForDuplicatePost` before it, or any time after it, so sending the same backdated post
twice stores it once. The cleanup job removes posts older than `CleanupAge` (60 hours by default):
raise it to keep older history.

`POST /api/users/{username}` sets the form fields `bio` (up to 500 characters) and `status` (up to
50) of a user, leaving a field that is not sent as it was, and returns the user.

```bash
curl -X POST 'http://localhost:5000/api/users/jsmith?theme=linkedin' \
  -d 'bio=Control systems engineer' -d status=Active
```

### Reporting Activity to GHOSTS

When `Ghosts.ApiUrl` is set (environment variable `GHOSTS_API_URL`, for example
`http://ghosts-api:5000/api`), every post, like, comment and direct message is reported to the
GHOSTS API as activity of the NPC that owns the username. Pandora asks the API for that NPC with
`GET /api/npcs/lookup?username=`, which matches the username the NPC's machine reports, else the
name part of the NPC's email, else `first.last`. Usernames with no NPC are skipped, and the answer
is remembered for two minutes either way. Reports are sent in the background, so an unreachable API
does not slow the site down, and carry the time they were sent: a backdated post is recorded as
activity of the moment it was loaded.

## Configuration

Edit `appsettings.json` to configure all features:

### Social Media vs Website Mode

The application can run in two modes:

**Social Mode** (default): Acts as a social media platform with user posts, comments, likes, themes, etc.

**Website Mode**: Acts as a regular website (news site, shopping site, sports site, etc.) with a realistic homepage.

```json
{
  "ApplicationConfiguration": {
    "Mode": {
      "Type": "social",  // Options: "social" or "website"
      "DefaultTheme": "facebook",  // Used when Type is "social"
      "SiteType": "news",  // Used when Type is "website" (options: news, shopping, sports, entertainment)
      "SiteName": "Daily Chronicle",  // Used when Type is "website"
      "ArticleCount": 12  // Used when Type is "website"
    }
  }
}
```

Set `Mode.Type` to `"social"` for social media mode or `"website"` for website mode. The root URL (`/`) will behave accordingly.

#### Environment Variable Overrides

You can override mode settings using environment variables (useful for Docker deployments):

```bash
# Mode configuration
MODE_TYPE=social           # "social" or "website"
DEFAULT_THEME=instagram    # Social mode theme
SITE_TYPE=news            # Website mode type (news, shopping, sports, entertainment)
SITE_NAME="Daily News"    # Website mode site name
ARTICLE_COUNT=15          # Website mode article count
```

**Docker Compose Example:**
```yaml
environment:
  - MODE_TYPE=website
  - SITE_TYPE=shopping
  - SITE_NAME=ShopMart
  - ARTICLE_COUNT=20
```

**Docker Run Example:**
```bash
docker run -e MODE_TYPE=social -e DEFAULT_THEME=twitter ghosts-pandora
```

Settings are resolved in this order, highest first:

1. The short variables above, plus `GHOSTS_API_URL`. The Docker image sets defaults for the five mode variables, so in a container the mode can only be changed through them.
2. Any other `ApplicationConfiguration` setting, named by its path in `appsettings.json` with `__` between levels:
   ```bash
   ApplicationConfiguration__CleanupAge__Days=30
   ApplicationConfiguration__Pandora__OllamaEnabled=true
   ```
3. `appsettings.json`.

### Database

Social media data is stored in SQLite by default, in `db/pandora.db` under the application directory (`/app/db` in the container). To use PostgreSQL instead:

```bash
DATABASE_PROVIDER=PostgreSQL
CONNECTION_STRING="Host=ghosts-postgres;Port=5432;Database=pandora;Username=ghosts;Password=..."
```

The database is created on first use if it does not exist.

### Reporting Activity to the GHOSTS API

Set `GHOSTS_API_URL` (for example `http://ghosts-api:5000/api`) to have Pandora report posts, likes, comments and direct messages to a GHOSTS API as NPC activity. It is empty by default, which turns reporting off.

### Payload Configuration

Serve specific files at configured URLs:

```json
{
  "ApplicationConfiguration": {
    "Payloads": {
      "Enabled": true,
      "PayloadDirectory": "Payloads",
      "Mappings": [
        {
          "Url": "/downloads/document",
          "FileName": "sample.pdf",
          "ContentType": "application/pdf"
        }
      ]
    }
  }
}
```

Place your payload files in the `Payloads` directory and configure the mappings. Access `GET /payloads` to list all configured payloads.

### Pandora Content Generation

Configure dynamic content generation:

```json
{
  "ApplicationConfiguration": {
    "Pandora": {
      "Enabled": true,
      "StoreResults": true,
      "ContentCacheDirectory": "_data",
      "OllamaEnabled": false,
      "OllamaApiUrl": "http://localhost:11434/api/generate",
      "OllamaTimeout": 60
    }
  }
}
```

### Configuration Options

- `Enabled`: Enable/disable Pandora functionality
- `StoreResults`: Cache generated content for repeat requests
- `ContentCacheDirectory`: Directory for cached content
- `OllamaEnabled`: Enable AI-powered content generation (requires Ollama)
- `OllamaApiUrl`: URL for Ollama API
- `OllamaTimeout`: Timeout for AI generation requests

## Running the Server

Pandora runs either on its own or as part of the GHOSTS stack. Each works with no extra configuration:

| | Standalone | In the GHOSTS stack |
|---|---|---|
| Started with | `docker run`, or `docker compose up -d` in `src/Ghosts.Pandora/src` | `docker compose up -d` in `src/Ghosts.Api` (service `ghosts-pandora`), or the Aspire host |
| Database | SQLite in `/app/db` | PostgreSQL on `ghosts-postgres`, database `pandora` |
| Activity reporting to the GHOSTS API | off; set `GHOSTS_API_URL` to turn it on | on |
| Address | http://localhost:8000 | http://localhost:8800 (`PANDORA_PORT`); `http://ghosts-pandora:5000` on the compose network |

### Docker

**Quick Start** (builds the image from source):
```bash
cd src
docker compose up -d
```

A prebuilt image is published as `ghcr.io/cmu-sei/ghosts/pandora` (`latest` follows `master`).

**Docker Run with Custom Configuration:**
```bash
# Social media mode (Facebook)
docker run -d -p 8000:5000 \
  -e MODE_TYPE=social \
  -e DEFAULT_THEME=facebook \
  -v pandora-db:/app/db \
  -v pandora-images:/app/wwwroot/images \
  ghcr.io/cmu-sei/ghosts/pandora:latest

# Website mode (News)
docker run -d -p 8000:5000 \
  -e MODE_TYPE=website \
  -e SITE_TYPE=news \
  -e SITE_NAME="Breaking News" \
  -v ./Payloads:/app/Payloads \
  ghcr.io/cmu-sei/ghosts/pandora:latest
```

**Docker Compose with Environment Variables:**
```yaml
services:
  pandora:
    image: ghcr.io/cmu-sei/ghosts/pandora:latest
    ports:
      - "8000:5000"
    environment:
      - MODE_TYPE=social
      - DEFAULT_THEME=instagram
    volumes:
      - pandora-db:/app/db
      - pandora-images:/app/wwwroot/images
    restart: always

volumes:
  pandora-db:
  pandora-images:
```

**Multiple Instances:**

To run several sites (for example one per theme), start one container per site, each with its own port and volumes.

**Volume Mounts:**
- `/app/db` - SQLite database
- `/app/wwwroot/images` - Images uploaded with posts
- `/app/Payloads` - Custom payload files
- `/app/_data` - Cached generated content
- `./appsettings.json:/app/appsettings.json:ro` - Custom configuration (optional)

### Building from Source

```bash
docker build -t ghosts-pandora:custom .
docker run -d -p 8000:5000 ghosts-pandora:custom
```

### Development

```bash
cd src
dotnet restore
dotnet run
```

**With Environment Variables:**
```bash
export MODE_TYPE=website
export SITE_TYPE=shopping
dotnet run
```

The server will be available at `http://localhost:5000` (or configured port).

Visit `/swagger` for API documentation.

## API Documentation

Interactive API documentation is available at `/swagger` when the server is running. This provides:
- Complete endpoint reference
- Request/response examples
- Try-it-out functionality for testing endpoints
- Parameter descriptions

## Technology Stack

**Backend:**
- .NET 10.0 (C#)
- ASP.NET Core MVC
- Entity Framework Core with SQLite
- SignalR for real-time updates

**Content Generation Libraries:**
- QuestPDF - PDF generation
- ClosedXML - Excel spreadsheets
- DocumentFormat.OpenXml - Word and PowerPoint documents
- SkiaSharp - Image generation
- SharpCompress - Archive creation (ZIP, TAR)
- CsvHelper - CSV file generation

**Frontend:**
- Razor views with theme support
- JavaScript for real-time updates
- Responsive CSS for multiple device types

## Migrating from GHOSTS Pandora

Migrating to **GHOSTS Pandora** is straightforward:

### What's Changed

**Unified Application:**
- Pandora and Socializer are now a **single application**
- All Pandora content generation capabilities are included
- Added social media and website simulation modes
- Configuration via `appsettings.json` or environment variables (no more `app.config`)

**Same Endpoints:**
- All Pandora endpoints work the same way (`/pdf/*`, `/doc/*`, `/img/*`, etc.)
- File extension routing works identically
- Directory-based routing works identically

**New Features:**
- **Mode switching**: Choose between social media, website, or pure content generation
- **Payload configuration**: Serve specific files at configured URLs
- **Enhanced HTML**: Generated HTML can match social media themes or be standalone
- **More file types**: Added OneNote, ISO, MSI, and improved video/audio support

### Migration Steps

1. **Replace the old container(s)** with Pandora:
   ```bash
   # Old
   docker run -p 80:80 dustinupdyke/ghosts-pandora:latest

   # New
   docker run -p 80:5000 -e MODE_TYPE=website ghcr.io/cmu-sei/ghosts/pandora:latest
   ```

2. **Update port mappings**: Pandora runs on port 5000 by default (the old Pandora used port 80)

3. **Migrate payload files**: Copy your payload files to the `Payloads/` directory and configure in `appsettings.json`:
   ```json
   {
     "Payloads": {
       "Enabled": true,
       "Mappings": [
         {
           "Url": "/payloads/malicious/document",
           "FileName": "malware.pdf",
           "ContentType": "application/pdf"
         }
       ]
     }
   }
   ```

4. **Configure mode**: Set `MODE_TYPE=website` to get Pandora-like behavior (pure content generation without social features)

## About GHOSTS

GHOSTS is a realistic cyber simulator designed to create realistic, non-player characters (NPCs) for training, simulation, and exercise environments. Learn more at the [GHOSTS GitHub repository](https://github.com/cmu-sei/GHOSTS).

## License

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.

Copyright 2017-2025 Carnegie Mellon University. All Rights Reserved.

See [LICENSE.md](LICENSE.md) file for terms.

## Contributing

Contributions are welcome! Please see the [GHOSTS repository](https://github.com/cmu-sei/GHOSTS) for contribution guidelines.
