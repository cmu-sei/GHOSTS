# Payloads Directory

This directory contains files that can be served as configured payloads.

## Configuration

Configure payload mappings in `appsettings.json`:

```json
{
  "ApplicationConfiguration": {
    "Payloads": {
      "Enabled": true,
      "PayloadDirectory": "Payloads",
      "Mappings": [
        {
          "Url": "/downloads/document",
          "FileName": "malicious.pdf",
          "ContentType": "application/pdf"
        },
        {
          "Url": "/files/installer",
          "FileName": "setup.exe",
          "ContentType": "application/octet-stream"
        }
      ]
    }
  }
}
```

## Usage

1. Place your payload files in this directory
2. Configure the mapping in `appsettings.json`
3. Access the payload via the configured URL

Example:
- Configuration: `{ "Url": "/downloads/document", "FileName": "test.pdf" }`
- Access at: `http://localhost:5000/downloads/document`
- This will serve the file `Payloads/test.pdf`

## List Configured Payloads

Access `GET /payloads` (without a path) to see all configured payloads and their status.

## Use Cases

This feature is useful for:
- Cybersecurity training and exercises
- Testing download behavior
- Simulating malicious payloads in a controlled environment
- Red team exercises

**WARNING**: Be careful what files you place in this directory, as they will be served to anyone who accesses the configured URLs.
