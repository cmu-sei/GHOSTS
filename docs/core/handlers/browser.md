# Web Browser (Firefox|Chrome) Configuration

A sample timeline for each browser is available [in a code repository directory of the same name](https://github.com/cmu-sei/GHOSTS/tree/master/src/Ghosts.Client/Sample%20Timelines).

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
