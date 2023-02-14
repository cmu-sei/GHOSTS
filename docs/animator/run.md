# Running Animator Animations

Animator is a simulation of a population of agents. Animator runs in cycles, and for each cycle, the agents make decisions based on their attributes, preferences, motivations, and behaviors.

## Setup

- Get the Animator API up and running as outlined [here](index.md)
- Edit the `appsettings.json` file to enable Animations:

```json
"ApplicationSettings": {
    "GhostsApiUrl": "http://localhost:52388/",        // this should be root url of your ghosts API server
    "Animations": {
      "IsEnabled": false,                             // set this to true to enable animation jobs
      "SocialGraph": {
        "IsEnabled": false,                           // set this to true to enable the entire module (inc. web gui access)
        "IsInteracting": false,                       // set this to true to actually run the interactions
        "MaximumSteps": 4000,                         // animator stops when it reaches this many steps 
        "TurnLength": 900,                            // by default, a step is a cpu cycle, the higher this number, the slower the simulation 
        "ChanceOfKnowledgeTransfer": 0.3,
        "Decay": {
          "StepsTo": 10,
          "ChanceOf": 0.05
        }
      },
      "SocialSharing": {
        "IsEnabled": false,                          // set this to true to enable the entire module (inc. web gui access)
        "IsInteracting": false,                      // set this to true to actually run the interactions
        "IsSendingTimelinesToGhostsApi": false,      // set this to true to actually send the timeline commands to the ghosts API
        "IsChatGptEnabled": false,                   // this is still under development, here be dragons
        "SocializerUrl": "http://socializer.com",    // change this to the root url of your in-game social server
        "MaximumSteps": 14000,
        "TurnLength": 9000                           // by default, a step is a cpu cycle, the higher this number, the slower the simulation 
      },
      "SocialBelief": {
        "IsEnabled": false,                         // set this to true to enable the entire module (inc. web gui access)
        "IsInteracting": false,                     // set this to true to actually run the interactions
        "MaximumSteps": 14000,
        "TurnLength": 9000
      }
    }
  },
  ...
```

After you update the appsettings.json file, you will need to restart the Animator API server via:

```bash
docker restart animator-api
```

