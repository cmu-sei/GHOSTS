# Web Browser Handler Configuration

The browser handlers simulate realistic web browsing behavior using Firefox or Chrome. GHOSTS can control browser activities including page navigation, form interaction, file downloads, and more.

???+ info "Sample Configurations"
    Sample browser timelines are available in the [GHOSTS GitHub repository](https://github.com/cmu-sei/GHOSTS/tree/master/src/Ghosts.Client/Sample%20Timelines).

## Prerequisites

Before using browser handlers, you must download the appropriate WebDriver:

- **Firefox**: [Download Geckodriver](https://github.com/mozilla/geckodriver/releases) and place it in the same folder as the GHOSTS executable
- **Chrome**: [Download Chromedriver](https://chromedriver.chromium.org/downloads) and place it in the same folder as the GHOSTS executable

Ensure the WebDriver version matches your installed browser version.

## Handler Types

- `BrowserFirefox` - Uses Firefox browser
- `BrowserChrome` - Uses Chrome/Chromium browser

## Handler Arguments

Browser handlers support various arguments to control browser behavior and resource usage:

```json
{
  "TimeLineHandlers": [
    {
      "HandlerType": "BrowserFirefox",
      "HandlerArgs": {
        "isheadless": "false",         // Run without visible browser window (saves resources)
        "blockimages": "true",         // Block images from loading
        "blockstyles": "true",         // Block CSS stylesheets from loading
        "blockflash": "true",          // Block Flash content from loading
        "blockscripts": "true",        // Block JavaScript from executing
        "stickiness": 75,              // 75% chance of staying on same site (0-100)
        "stickiness-depth-min": 5,     // Minimum links to click on a sticky site
        "stickiness-depth-max": 10,    // Maximum links to click on a sticky site
        "incognito": "true"            // Run in incognito/private mode
      }
    }
  ]
}
```

### Handler Arguments Explained

| Argument | Type | Default | Description |
|----------|------|---------|-------------|
| `isheadless` | boolean | false | Run browser without GUI (reduces resource usage) |
| `blockimages` | boolean | false | Prevent images from loading (faster, less bandwidth) |
| `blockstyles` | boolean | false | Prevent CSS from loading |
| `blockscripts` | boolean | false | Prevent JavaScript from executing |
| `blockflash` | boolean | true | Prevent Flash content (deprecated technology) |
| `stickiness` | integer (0-100) | 0 | Probability of staying on same domain for next link |
| `stickiness-depth-min` | integer | 1 | Minimum number of links to follow on sticky domain |
| `stickiness-depth-max` | integer | 10 | Maximum number of links to follow on sticky domain |
| `incognito` | boolean | false | Use private browsing mode |

**Performance Tip**: For resource-constrained environments, set `isheadless="true"` and block images/styles/scripts to significantly reduce CPU and memory usage.

## Timeline Event Commands

Browser handlers support various commands to simulate different browsing behaviors:

### Core Commands

**`random`** - Randomly select and visit URLs from a list

```json
{
  "Command": "random",
  "CommandArgs": [
    "https://www.cmu.edu",
    "https://sei.cmu.edu",
    "https://www.example.com"
  ],
  "DelayAfter": 30000,
  "DelayBefore": 5000
}
```

When `Loop: true` and stickiness is configured, the browser will randomly navigate between sites while occasionally "sticking" to one site and following internal links.

**`randomalt`** - Like `random` but includes POST requests in addition to GET requests. Useful for simulating form submissions.

**`browse`** - Navigate to a specific URL

```json
{
  "Command": "browse",
  "CommandArgs": ["https://www.example.com/page"],
  "DelayAfter": 10000,
  "DelayBefore": 0
}
```

### Specialized Commands

**`download`** - Download a file from a specified element

```json
{
  "Command": "download",
  "CommandArgs": ["//a[contains(@class, 'download-button')]"],
  "TrackableId": "550e8400-e29b-41d4-a716-446655440000",
  "DelayAfter": 0,
  "DelayBefore": 0
}
```

The CommandArg is an XPath selector for the download link element.

**`upload`** - Upload a file through a web form

```json
{
  "Command": "upload",
  "CommandArgs": ["/path/to/file.pdf", "//input[@type='file']"],
  "DelayAfter": 5000,
  "DelayBefore": 1000
}
```

**`outlook`** - Interact with Outlook Web Access (OWA)

Simulates reading, composing, and sending emails through the Outlook web client.

**`sharepoint`** - Navigate and interact with SharePoint sites

Browses SharePoint document libraries, lists, and pages.

**`blog`** - Interact with Drupal-based blogs

Reads posts, follows links, and simulates blog browsing behavior.

**`crawl`** - Internal command for content scraping

Used internally with proxies to collect content for GHOSTS content generation.

### Element Interaction Commands

**`click`** - Click an element by XPath

```json
{
  "Command": "click",
  "CommandArgs": ["//button[@id='submit']"]
}
```

**`click.by.id`** - Click element by ID

```json
{
  "Command": "click.by.id",
  "CommandArgs": ["submit-button"]
}
```

**`click.by.name`** - Click element by name attribute

**`click.by.linktext`** - Click link by visible text

**`click.by.cssselector`** - Click element by CSS selector

**`type`** - Type text into an element (by XPath)

```json
{
  "Command": "type",
  "CommandArgs": ["//input[@name='search']", "ghosts framework"]
}
```

**`typebyid`** - Type text into element by ID

**`js.executescript`** - Execute JavaScript in the browser context

```json
{
  "Command": "js.executescript",
  "CommandArgs": ["window.scrollTo(0, document.body.scrollHeight);"]
}
```

**`manage.window.size`** - Set browser window dimensions

```json
{
  "Command": "manage.window.size",
  "CommandArgs": ["1920", "1080"]
}
```

## Dynamic URL Variables

GHOSTS supports dynamic URL generation using variable placeholders, making it easy to generate realistic, varied web requests.

### Built-in Variables

URLs can include variables using `{variable}` syntax:

| Variable | Description | Example Output |
|----------|-------------|----------------|
| `{now}` | Current date | `01/15/2024` |
| `{uuid}` | Random UUID | `550e8400-e29b-41d4-a716-446655440000` |
| `{c}` | Random letter (a-z, A-Z) | `m` |
| `{n}` | Random number (1-1000) | `742` |

**Example:**

```text
https://example.com/api/data_{uuid}?timestamp={now}&rand={n}
```

**Rendered as:**

```text
https://example.com/api/data_550e8400-e29b-41d4-a716-446655440000?timestamp=01/15/2024&rand=742
```

### Custom URL Variables

Define custom variable replacements in your handler configuration:

```json
{
  "HandlerType": "BrowserChrome",
  "HandlerArgs": {
    "url-replace": [
      {"verb": ["order", "enable", "engage", "deploy"]},
      {"group": ["operations", "logistics", "medical", "admin"]},
      {"org": ["army", "command", "brigade", "battalion"]},
      {"type": ["document", "doc", "files", "vault"]}
    ]
  },
  "TimeLineEvents": [
    {
      "Command": "browse",
      "CommandArgs": [
        "https://portal.mil/{org}/{group}/{verb}/{type}/{uuid}/v_{n}?id={c}&date={now}"
      ]
    }
  ]
}
```

**Example output:**

```text
https://portal.mil/command/operations/order/doc/bcc396b5-47d0-4665-93c8-0a314cec13e1/v_742?id=d&date=01/15/2024
```

Each time the URL is accessed, GHOSTS randomly selects from the custom variable arrays, generating unique URLs that appear realistic.

## Complete Example Configuration

Here's a comprehensive browser handler configuration demonstrating key features:

```json
{
  "TimeLineHandlers": [
    {
      "HandlerType": "BrowserChrome",
      "Initial": "https://www.google.com",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": true,
      "HandlerArgs": {
        "isheadless": "false",
        "blockimages": "true",
        "blockstyles": "true",
        "stickiness": 60,
        "stickiness-depth-min": 3,
        "stickiness-depth-max": 10,
        "url-replace": [
          {"category": ["news", "sports", "tech", "weather"]},
          {"action": ["view", "read", "search"]}
        ]
      },
      "TimeLineEvents": [
        {
          "Command": "random",
          "CommandArgs": [
            "https://www.cmu.edu",
            "https://www.github.com",
            "https://news.ycombinator.com",
            "https://www.example.com/{category}/{action}?id={n}"
          ],
          "DelayAfter": 45000,
          "DelayBefore": 5000
        }
      ]
    }
  ]
}
```

## Best Practices

1. **Resource Management**: Use headless mode and block resources when browser visibility isn't required
2. **Realistic Behavior**: Configure stickiness to simulate users staying on sites
3. **Variable Delays**: Use `DelayBefore` and `DelayAfter` with realistic values (30-60 seconds)
4. **Time Windows**: Set `UtcTimeOn` and `UtcTimeOff` to simulate work hours
5. **Trackables**: Use `TrackableId` for important actions you need to verify
6. **Driver Versions**: Keep WebDrivers updated to match browser versions

## Troubleshooting

### Browser Won't Start

**Check WebDriver**: Ensure Geckodriver/Chromedriver is in the GHOSTS directory and matches your browser version.

```bash
# Check Chrome version
google-chrome --version

# Check Chromedriver version
./chromedriver --version
```

### "Element Not Found" Errors

**Increase Delays**: Web pages may need time to load before elements are clickable:

```json
{
  "Command": "browse",
  "CommandArgs": ["https://example.com"],
  "DelayAfter": 5000
}
```

### High CPU/Memory Usage

**Enable Headless and Blocking**:

```json
{
  "HandlerArgs": {
    "isheadless": "true",
    "blockimages": "true",
    "blockstyles": "true",
    "blockscripts": "true"
  }
}
```

### Display Issues on Linux

If running as root, browser drivers may fail to display. Run GHOSTS as a regular user instead.
