# Web Browser (Firefox|Chrome) Configuration

## Timeline URL Variables

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

`https://www.cmu.edu/{org}/{group}/{verb}/{type}/{uuid}/version_{n}?{c}={now>}`

might be rendered as:

`https://www.cmu.edu/command/operations/order/doc/bcc396b5-47d0-4665-93c8-0a314cec13e1/version_55?d=6/21/2022`
