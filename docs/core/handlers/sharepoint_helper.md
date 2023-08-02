# Sharepoint Helper Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

The 'sharepoint' command for a browser handler allows download/deletion/upload from a sharepoint site.  

The handlerArgs for the sharepoint command are:
    
- "sharepoint-credentials-file": `json credentials file path`,  required, credentials file, see SSh.json sample handler for format
- "sharepoint-deletion-probability": <0-100 integer>, default  0
- "sharepoint-upload-probability": <0-100 integer>, default 0
- "sharepoint-download-probability": <0-100 integer>, default 0, sum of download+deletion+upload <= 100, download directory is browser download directory
- "sharepoint-version": "2013",  -- version, required, only 2013 is currrently supported
- "sharepoint-upload-directory": `upload directory path`" -- files to be uploaded are read from this directory, default is browser download directory

The CommandArgs are strings of the form "key:value", supported args are:

- site:`sharepoint site`   -- required
- credentialKey:`credential key contained in the credential file`

A handler can only browse a single share point site. The username, password specified by the credentialKey are used to login into the site assuming NTLM authentication (i.e,  username:password is passed in the URL header). The Documents site is assumed to be at `site`/Documents/Forms/Allitems.aspx

```json
{
  "Status": "Run",
  "TimeLineHandlers": [
    {
      "HandlerType": "BrowserChrome",
      "HandlerArgs": {
        "isheadless": "false",
        "blockimages": "true",
        "blockstyles": "true",
        "blockflash": "true",
        "blockscripts": "true",
        "stickiness": 75,
        "stickiness-depth-min": 5,
        "stickiness-depth-max": 10000,
        "incognito": "true",
        "sharepoint-credentials-file": "c:\\ghosts_data\\sharepoint_creds.json",
        "sharepoint-deletion-probability": 15,
        "sharepoint-upload-probability": 35,
        "sharepoint-download-probability": 35,
        "sharepoint-version": "2013",
        "sharepoint-upload-directory": "C:\\ghosts_data\\uploads"

      },
      "Initial": "about:blank",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": "True",
      "TimeLineEvents": [
        {
          "Command": "sharepoint",
          "CommandArgs": [
            "site:http://portal.sitea.com",
            "credentialKey:credkey1"
          ],
          "DelayAfter": 60000,
          "DelayBefore": 0
        }
      ]
    }
  ]
}
```
