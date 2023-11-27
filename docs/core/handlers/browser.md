# Web Browser (Firefox|Chrome) Configuration

A sample timeline for each browser is available [in the repository directory of the same name](https://github.com/cmu-sei/GHOSTS/tree/master/src/Ghosts.Client/Sample%20Timelines).

## Timeline Settings

For any single handler, there are multiple events possible. For a timeline, HandlerArgs settings might look like:

```
 "TimeLineHandlers": [
        {
            "HandlerType": "BrowserFirefox",
            "HandlerArgs": {
                "isheadless": "false",         // running in headless mode requires less resources
                "blockimages": "true",         // block images from loading
                "blockstyles": "true",         // block stylesheets from loading
                "blockflash": "true",          // block flash (RIP) from loading
                "blockscripts": "true",        // block scripts from loading
                "stickiness": 75,              // 75% chance of staying on the same website
                "stickiness-depth-min": 5,     // minimum number of links to click on a website
                "stickiness-depth-max": 10000, // maximum number of links to click on a website
                "incognito": "true"            // run in incognito mode
            },
```

## TimelineEvent Basics

For a given web browser timeline (such as the sample for [FireFox](https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/BrowserFirefox.json)), there are several TimelineEvent commands possible:

- random

This event combined with loop being set to true will cause the timeline to randomly select a website from the list provided in CommandArgs and make a corresponding GET request to that site. The timeline will then wait for the number of seconds specified in the Delay property before selecting another random website from the list. Note that if stickiness is set, the next link may be selected from the same website as the previous link.

An example of this setting is:

```
"TimeLineEvents": [
                {
                    "Command": "random",
                    "CommandArgs": [
                        "http://www.cmu.edu",
                        "http://sei.cmu.edu"
                        ...
```

- randomalt

Similar to random but includes POST commands as well.

- browse

Perform a GET request to the website provided in CommandArgs. This is typically used to perform a just in time JIT browser activity.

- crawl

Used internally in conjunction with a proxy to scrape content for our hosted greyspace.

- outlook

For use with the Outlook web client. This command will perform a GET request to the Outlook web client and then perform a series of actions such as opening an email, replying to an email, and sending an email. The number of emails to open, reply to, and send are specified in the CommandArgs property.

- sharepoint

For use in browsing SharePoint sites and their related content.

- blog

For use in browsing Drupal sites and their related content.

- download

Perform a GET request to the website provided in CommandArgs and save the response to a file. This is typically used to perform a just in time JIT browser activity.

- upload

Perform a POST request to the website provided in CommandArgs and save the response to a file. This is typically used to perform a just in time JIT browser activity.

There are other less often used commands such as:

- type
- typebyid
- click
- click.by.name
- clickbyid
- click.by.id
- click.by.linktext
- click.by.cssselector
- js.executescript
- manage.window.size

## Timeline URL Variables

An example of a URL using variables is: 

```text
http://craigslist.org/{org}/{group}/{uuid}/{verb}/{type}/{n}?{c}={now}
```

All variables are in the format `{variable}`. There are several standard variables:

- `{now}` = short datetime (mm/dd/yyyy format)
- `{uuid}` = uuid
- `{c}` = a single character from a-z and A-Z
- `{n}` = number between 1 and 1000

We can also configure additional variables by adding the following to a browser handler in a timeline:

```json
"url-replace": [
          {"verb": ["order", "enable", "engage"]},
          {"group": ["operations", "logistics", "medical"]},
          {"org": ["army", "command", "brigade", "battalion"]},
          {"type": ["document", "doc", "files", "vault", "filevault"]}
        ]
```

Therefore:

```text
https://www.cmu.edu/{org}/{group}/{verb}/{type}/{uuid}/version_{n}?{c}={now}
```

might be rendered as:

```text
https://www.cmu.edu/command/operations/order/doc/bcc396b5-47d0-4665-93c8-0a314cec13e1/version_55?d=6/21/2022
```
