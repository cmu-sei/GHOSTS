# Running Animator Animations

Animator is a simulation of a population of agents. Animator runs in cycles, and for each cycle, the agents make decisions based on their attributes, preferences, motivations, and behaviors.

## Setup

- Get the Animator API up and running as outlined [here](index.md)
- The `appsettings.json` file points to Animator-specific configuration, which by default is in `./config/config.json`:

```json
  {
    "ApplicationDatabaseSettings": {
      "ConnectionString": "mongodb://ghosts-mongo:32770",
      "DatabaseName": "AnimatorDb"
    },
    "ApplicationSettings": {
      "GhostsApiUrl": "http://localhost:52388/", //the root url of the Ghosts API
      "Proxy": "",
      "Animations": {
        "IsEnabled": false, //if false, then all animations are disbled
        "SocialGraph": {
          "IsEnabled": false, //if false, just this animation is disabled
          "IsMultiThreaded": true, //helpful to set to false for debugging purposes
          "IsInteracting": true, //means new agent interactions are being generated
          "MaximumSteps": 4000, //max steps to execute
          "TurnLength": 9000, //ms per step
          "ChanceOfKnowledgeTransfer": 0.3, //chance that an agent will share knowledge with another agent
          "Decay": {
            "StepsTo": 10, //min steps to execute before an agent begins forgetting things
            "ChanceOf": 0.05
          }
        },
        "SocialBelief": {
          "IsEnabled": false,
          "IsMultiThreaded": true,
          "IsInteracting": true,
          "MaximumSteps": 300,
          "TurnLength": 9000
        },
        "SocialSharing": {
          "IsEnabled": false,
          "IsMultiThreaded": true,
          "IsInteracting": true,
          "IsSendingTimelinesToGhostsApi": false,
          "IsSendingTimelinesDirectToSocializer": true,
          "PostUrl": "http://localhost:8000",
          "MaximumSteps": 100,
          "TurnLength": 9000,
          "ContentEngine": {
            "Source": "ollama",
            "Host": "http://localhost:11434",
            "Model": "chat"
          }
        },
        "Chat": {
          "IsEnabled": false,
          "IsMultiThreaded": true,
          "IsInteracting": true,
          "MaximumSteps": 300,
          "TurnLength": 9000,
          "IsSendingTimelinesToGhostsApi": false,
          "PostUrl": "http://localhost:8065",
          "ContentEngine": {
            "Source": "ollama",
            "Host": "http://localhost:11434",
            "Model": "chat"
          }
        },
        "FullAutonomy": {
          "IsEnabled": false,
          "IsMultiThreaded": true,
          "IsInteracting": true,
          "IsSendingTimelinesToGhostsApi": false,
          "MaximumSteps": 10000,
          "TurnLength": 9000,
          "ContentEngine": {
            "Source": "ollama",
            "Host": "http://localhost:11434",
            "Model": "activity"
          }
        }
      }
    },
    "AllowedHosts": "*",
    "ClientSettings": {
    },
    "CorsPolicy": {
      "Origins": [
        "http://localhost:4200"
      ],
      "Methods": [],
      "Headers": [],
      "AllowAnyOrigin": false,
      "AllowAnyMethod": true,
      "AllowAnyHeader": true,
      "SupportsCredentials": true
    }
  }
  ...
```

After you update the appsettings.json file, you will need to restart the Animator API server via:

```bash
docker restart animator-api
```

