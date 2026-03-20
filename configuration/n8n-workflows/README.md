# GHOSTS Animation Workflows for n8n

This directory contains n8n workflow JSON files that recreate the GHOSTS animation definitions for use in n8n.

## Workflows Included

1. **social-graph-animation.json** - Builds social connections between NPCs, transfers knowledge, and improves relationships
2. **social-belief-animation.json** - Evolves NPC beliefs over time using Bayesian inference
3. **social-sharing-animation.json** - Generates and posts social media content from NPCs
4. **chat-animation.json** - Generates chat messages and posts to Mattermost channels
5. **full-autonomy-animation.json** - Generates autonomous actions for NPCs using AI

## Prerequisites

Before importing these workflows, you need to:

1. **Install n8n**
   ```bash
   npm install n8n -g
   # or
   docker run -it --rm --name n8n -p 5678:5678 n8nio/n8n
   ```

2. **Set up PostgreSQL Connection**
   - In n8n, go to Credentials → Add Credential → Postgres
   - Name it "GHOSTS PostgreSQL"
   - Configure with your GHOSTS database connection details:
     - Host: `localhost` (or your database host)
     - Database: `ghosts`
     - User: your PostgreSQL user
     - Password: your PostgreSQL password
     - Port: `5432`

3. **Install Ollama (Optional, for AI-generated content)**
   ```bash
   curl https://ollama.ai/install.sh | sh
   ollama pull llama3.2
   ```

   If you don't have Ollama, the workflows will use fallback content generation.

4. **Update URLs in Workflows (if needed)**
   The workflows use `host.docker.internal` by default to reach services on the Docker host.
   - GHOSTS API: `http://host.docker.internal:5000`
   - Socializer: `http://host.docker.internal:5555/`
   - Ollama: `http://host.docker.internal:11434/api/generate`

## How to Import

1. Open n8n in your browser (typically http://localhost:5678)
2. Click "Workflows" in the left sidebar
3. Click "Import from File" or "Import from URL"
4. Select one of the JSON files from this directory
5. The workflow will be imported with all nodes configured
6. Make sure to:
   - Connect the PostgreSQL credential (select "GHOSTS PostgreSQL")
   - Adjust the schedule triggers to your preferred intervals
   - Test the workflow with "Execute Workflow" button

## Workflow Details

### Social Graph Animation
- **Schedule**: Every 5 minutes (customizable via cron: `*/5 * * * *`)
- **What it does**:
  - Selects 10 random NPCs
  - Gets or creates social connections for each NPC
  - Randomly selects targets for interaction
  - Transfers knowledge between NPCs
  - Improves relationship status
  - Notifies the Activity Hub

### Social Belief Animation
- **Schedule**: Every 10 minutes (customizable via cron: `*/10 * * * *`)
- **What it does**:
  - Selects 10 random NPCs
  - Gets the latest belief for each NPC
  - Updates beliefs using Bayesian inference
  - Saves new belief state
  - Notifies the Activity Hub

### Social Sharing Animation
- **Schedule**: Every 15 minutes (customizable via cron: `*/15 * * * *`)
- **What it does**:
  - Selects 5-20 random NPCs
  - Generates social media posts using Ollama (with fallback)
  - Posts to Socializer service (optional)
  - Saves activity to database
  - Notifies the Activity Hub

### Chat Animation
- **Schedule**: Every 8 minutes (customizable via cron: `*/8 * * * *`)
- **What it does**:
  - Selects 3-10 random NPCs
  - Decides between new message or reply (30% reply chance)
  - Gets available Mattermost channels
  - Generates chat message using Ollama (with fallback)
  - Posts to Mattermost channel
  - Saves activity to database
  - Notifies the Activity Hub

**Note**: Requires Mattermost setup and authentication token. Replace `YOUR_MATTERMOST_TOKEN` in the workflow with your actual token.

### Full Autonomy Animation
- **Schedule**: Every 20 minutes (customizable via cron: `*/20 * * * *`)
- **What it does**:
  - Selects 5-20 random NPCs
  - Retrieves recent activity history
  - Generates next autonomous action using Ollama (with fallback)
  - Saves action to database
  - Notifies the Activity Hub

## Customization

### Adjusting Schedule
Edit the cron expression in the "Schedule Trigger" node:
- `*/5 * * * *` - Every 5 minutes
- `0 * * * *` - Every hour
- `0 */4 * * *` - Every 4 hours
- `0 9 * * *` - Daily at 9am

### Adjusting Number of NPCs
In the "Get Random NPCs" query, change the `LIMIT` value:
```sql
SELECT * FROM npcs ORDER BY RANDOM() LIMIT 10  -- Change 10 to your preferred number
```

### Disabling Ollama Integration
If you don't have Ollama running, the workflows will automatically fall back to template-based content generation. No changes needed.

### Changing Knowledge Topics
In the Social Graph workflow, edit the `topics` array in the "Process Learning" code node to add your own knowledge topics.

### Changing Belief Statements
In the Social Belief workflow, edit the `beliefs` array in the "Calculate Belief Update" code node.

## Database Tables Used

These workflows interact with the following GHOSTS database tables:
- `npcs` - NPC profiles
- `npc_social_connections` - Social graph relationships
- `npc_learning` - Knowledge transfer tracking
- `npc_beliefs` - Belief evolution tracking
- `npc_activities` - Activity logging

## Monitoring

- Each workflow includes a "Notify Activity Hub" node that posts updates via SignalR/HTTP
- Check the n8n execution logs for any errors
- Use the n8n UI to see workflow execution history and debug issues

## Integration with GHOSTS API

These workflows are designed to complement, not replace, the built-in GHOSTS animation system. You can:
- Run them alongside the GHOSTS API animations
- Run them independently if you want n8n to manage the scheduling
- Use them as a starting point for custom animation logic

## Troubleshooting

**Workflow fails with database error:**
- Verify your PostgreSQL credentials are correct
- Ensure the GHOSTS database schema is up to date
- Check that NPCs exist in the database

**AI content generation not working:**
- Verify Ollama is running: `curl http://localhost:11434/api/generate`
- Workflows will use fallback generation if Ollama is unavailable
- Adjust the Ollama model in the HTTP request nodes if needed

**ActivityHub notifications fail:**
- This is non-critical - workflows will continue
- Verify your GHOSTS API URL is correct
- Check that the Activity Hub is enabled in your GHOSTS API

## Advanced Usage

### Parallel Execution
The workflows process NPCs sequentially by default. To process in parallel:
1. Remove the "Split in Batches" node
2. Add a "Split Out" node after getting NPCs
3. Adjust downstream nodes to handle multiple items

### Custom Content Engines
Replace the Ollama integration with:
- OpenAI API (add OpenAI HTTP request node)
- Azure OpenAI (add Azure OpenAI HTTP request node)
- Local templates (use the fallback code node logic)
- External content service (add custom HTTP request)

### Webhook Integration
Add webhook nodes to notify external systems when animations complete:
1. Add "HTTP Request" node at the end of workflow
2. Configure to POST to your webhook URL
3. Include relevant NPC and activity data in the payload

## License

These workflows are part of the GHOSTS framework.
Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.
