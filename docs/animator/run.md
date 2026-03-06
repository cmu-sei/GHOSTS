# Running Animation Workflows

???+ info "Animations now use n8n workflows"
    As of GHOSTS v9.0, animations are implemented as n8n workflows. This provides a visual, flexible approach to building and managing NPC behaviors. The configuration examples below are from the legacy system and are provided for reference.

Animator is a simulation of a population of agents. Animations run in cycles, and for each cycle, the agents make decisions based on their attributes, preferences, motivations, and behaviors.

## Setup with n8n

1. Ensure the GHOSTS stack is running with n8n enabled (via docker-compose)
2. Access the n8n interface at `http://localhost:5678`
3. Browse the pre-configured animation workflows
4. Activate the workflows you want to run
5. Configure workflow parameters through the n8n interface

Each workflow can be started, stopped, and monitored through the n8n dashboard. Workflow execution logs are available in the n8n interface.

## Legacy Configuration Reference

The following configuration structure is from the legacy animation system and is provided for reference. New deployments should use n8n workflows instead.

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

## Managing Workflows in n8n

**To activate/deactivate workflows:**

1. Navigate to `http://localhost:5678`
2. Click on the workflow you want to manage
3. Toggle the "Active" switch in the top-right corner
4. Active workflows run automatically based on their configured triggers

**To modify workflows:**

1. Open the workflow in n8n
2. Add, remove, or modify nodes as needed
3. Save your changes
4. Test the workflow using the "Execute Workflow" button
5. Activate the workflow when ready

**Monitoring:**

- View workflow execution history in the n8n interface
- Check execution logs for debugging
- Monitor agent interactions through the GHOSTS API dashboard

