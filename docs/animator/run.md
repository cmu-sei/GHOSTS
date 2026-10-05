# Running Animation Workflows

???+ info "Animations now use n8n workflows"
    As of GHOSTS v9.0, animations are implemented as n8n workflows. This provides a visual, flexible approach to building and managing NPC behaviors.

Animator is a simulation of a population of agents. Animations run in cycles, and for each cycle, the agents make decisions based on their attributes, preferences, motivations, and behaviors.

## Setup with n8n

1. Ensure the GHOSTS stack is running with n8n enabled (via docker-compose)
2. Access the n8n interface at `http://localhost:5678`
3. Browse the pre-configured animation workflows
4. Activate the workflows you want to run
5. Configure workflow parameters through the n8n interface

Each workflow can be started, stopped, and monitored through the n8n dashboard. Workflow execution logs are available in the n8n interface.

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

