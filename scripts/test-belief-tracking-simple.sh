#!/bin/bash
# Simple GHOSTS Belief Tracking Test
# Uses the built-in Social Sharing Animation

API_URL="${API_URL:-http://localhost:5000}"
echo "🧪 Simple Belief Tracking Test"
echo "API: $API_URL"
echo ""

# Helper function
api_call() {
    curl -s -X $1 "$API_URL$2" -H "Content-Type: application/json" ${3:+-d "$3"}
}

echo "📋 Step 1: Create Hypotheses"
echo "-------------------------------------------"
api_call POST "/api/hypotheses" '{
  "name": "Security Threat",
  "keywords": "security,threat,attack,breach,vulnerability,risk,danger",
  "defaultLikelihood": 0.7
}' | jq -r '"✓ Created: \(.name) (ID: \(.id))"'

api_call POST "/api/hypotheses" '{
  "name": "System Outage",
  "keywords": "outage,down,failure,offline,unavailable,error,crash",
  "defaultLikelihood": 0.6
}' | jq -r '"✓ Created: \(.name) (ID: \(.id))"'

api_call POST "/api/hypotheses" '{
  "name": "Policy Change",
  "keywords": "policy,regulation,compliance,rules,requirements,mandate",
  "defaultLikelihood": 0.5
}' | jq -r '"✓ Created: \(.name) (ID: \(.id))"'

echo ""
echo "📋 Step 2: Create Test NPCs with Social Connections"
echo "-------------------------------------------"

# Create 5 NPCs
for i in {1..5}; do
    npc=$(api_call POST "/api/npcs" "{
      \"name\": \"TestUser$i\",
      \"email\": \"test$i@ghosts.local\"
    }")
    npc_id=$(echo $npc | jq -r '.id')
    echo "npc$i_id=$npc_id" >> /tmp/ghosts_npcs.env
    echo "✓ Created: TestUser$i ($npc_id)"
done

source /tmp/ghosts_npcs.env

# Create social connections (everyone follows everyone)
echo ""
echo "Creating social network..."
for i in {1..5}; do
    for j in {1..5}; do
        if [ $i -ne $j ]; then
            from_var="npc${i}_id"
            to_var="npc${j}_id"
            api_call POST "/api/npcs/social/connections" "{
              \"npcId\": \"${!from_var}\",
              \"connectedNpcId\": \"${!to_var}\",
              \"name\": \"follows\",
              \"distance\": \"moderate\",
              \"relationshipStatus\": 0.7
            }" > /dev/null
        fi
    done
done
echo "✓ Created fully connected social network (20 connections)"

echo ""
echo "📋 Step 3: Check Animation Status"
echo "-------------------------------------------"
animations=$(api_call GET "/api/animations")
echo "$animations" | jq -r '.[] | select(.name == "SocialSharing") | "Social Sharing: Status=\(.status), Turn Length=\(.turnLength)ms"'

echo ""
echo "📋 Step 4: Trigger Social Activity"
echo "-------------------------------------------"
echo "Starting social sharing animation..."
api_call POST "/api/animations/socialsharing/start" | jq -r '"Status: \(.status)"'

echo ""
echo "⏳ Waiting 10 seconds for NPCs to post..."
sleep 10

echo ""
echo "📋 Step 5: Check Results"
echo "-------------------------------------------"

activities=$(api_call GET "/api/npcs/${npc1_id}/activity")
activity_count=$(echo "$activities" | jq '. | length')
echo "NPC Activities: $activity_count posts"

beliefs=$(api_call GET "/api/npcs/beliefs")
belief_count=$(echo "$beliefs" | jq '. | length')
echo "Belief Records: $belief_count beliefs tracked"

if [ "$belief_count" -gt 0 ]; then
    echo ""
    echo "✅ SUCCESS! Beliefs are being tracked!"
    echo ""
    echo "📊 Belief Summary:"
    echo "$beliefs" | jq -r 'group_by(.name) | .[] | "\(.[ 0].name): \(length) updates across \(. | map(.npcId) | unique | length) NPCs"'

    echo ""
    echo "📈 Sample Belief Evolution:"
    echo "$beliefs" | jq -r '[.[] | select(.name == (.name))] | group_by(.npcId) | .[] |
        "NPC \(.[0].npcId | .[:8])... - \(.[0].name):" as $header |
        $header,
        (.[] | "  Step \(.step): \((.posterior * 100) | round)% confidence")'  | head -15
else
    echo ""
    echo "⚠️  No beliefs found yet. This could mean:"
    echo "  1. No posts matched hypothesis keywords"
    echo "  2. Animation hasn't completed a cycle yet"
    echo "  3. Evidence processor isn't enabled"
    echo ""
    echo "Check the API logs for evidence processing messages."
fi

echo ""
echo "📋 Step 6: Interactive Commands"
echo "-------------------------------------------"
echo "# View all hypotheses:"
echo "curl $API_URL/api/hypotheses | jq"
echo ""
echo "# View beliefs for a specific hypothesis:"
echo "curl '$API_URL/api/npcs/beliefs?hypothesis=Security%20Threat' | jq"
echo ""
echo "# View beliefs for a specific NPC:"
echo "curl $API_URL/api/npcs/beliefs?npcId=$npc1_id | jq"
echo ""
echo "# Stop the animation:"
echo "curl -X POST $API_URL/api/animations/socialsharing/stop"
echo ""
echo "# Clean up test data:"
echo "rm /tmp/ghosts_npcs.env"

echo ""
echo "✅ Test complete!"
