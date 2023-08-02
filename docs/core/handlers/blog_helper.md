# Blog Helper Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

The 'blog' command for a browser handler allows browse/deletion/upload/reply from a blog site.

The handlerArgs for the blog command are:

- "blog-credentials-file": `json credentials file path`,  required, credentials file, see SSh.json sample handler for format
- "blog-deletion-probability": <0-100 integer>, default  0, probability to delete a blog post
- "blog-upload-probability": <0-100 integer>, default 0 , probability to upload a blog post
- "blog-reply-probability": <0-100 integer>, default 0 , probability to reply to a random blog post
- "blog-browse-probability": <0-100 integer>, default 0, probability to browse to a random blog post
- "blog-version": "drupal",  -- version, required, only drupal is currrently supported, tested with version 7

Sum of browse+deletion+upload+reply <= 100

The CommandArgs are strings of the form "key:value", supported args are:

- site:`blog site`   -- required
- credentialKey:`credential key contained in the credential file`

A handler can only browse a single blog site. The username, password specified by the credentialKey are used to login into the site.

Under drupal, it assumed that all users have the capabilty to delete all blogs, even other user's blogs.

Default content in config directory is blog-content.csv, default reply content is blog-reply.csv

This content can be overridden in application.json by the 'BlogContent', 'BlogReply' fields.

```json
  "Content": {
      "EmailContent": "",
      "EmailReply": "",
      "EmailDomain": "",
      "EmailOutside": "",
      "BlogContent": "",
      "BlogReply": "",
      "FileNames": "",
      "Dictionary": ""
    },
```

The timeline configuration looks like this:

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
        "blog-credentials-file": "c:\\ghosts_data\\blog_creds.json",
        "blog-deletion-probability": 0,
        "blog-upload-probability": 0,
        "blog-browse-probability": 0,
        "blog-reply-probability": 100,
        "blog-version": "drupal"

      },
      "Initial": "about:blank",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": "True",
      "TimeLineEvents": [
        {
          "Command": "blog",
          "CommandArgs": [
            "site:http://www.netexhsv.com:8080",
            "credentialKey:credkey1"
          ],
          "DelayAfter": 10000,
          "DelayBefore": 0
        }
      ]
    }
  ]
}
```
