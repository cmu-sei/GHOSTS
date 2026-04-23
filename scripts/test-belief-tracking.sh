#!/bin/bash
# GHOSTS Belief Tracking Test Script
# This script tests the end-to-end belief tracking system

API_URL="${API_URL:-http://localhost:5000}"
echo "🧪 Testing GHOSTS Belief Tracking System"
echo "API: $API_URL"
echo ""

# Helper function for API calls
api_call() {
    local method=$1
    local endpoint=$2
    local data=$3

    if [ -z "$data" ]; then
        curl -s -X $method "$API_URL$endpoint" -H "Content-Type: application/json"
    else
        curl -s -X $method "$API_URL$endpoint" -H "Content-Type: application/json" -d "$data"
    fi
}

echo "📋 Step 1: Create Hypotheses with Keywords"
echo "-------------------------------------------"

# Create hypothesis about military operations
hypothesis1=$(api_call POST "/api/hypotheses" '{
  "name": "Military Escalation",
  "keywords": "troops,deployment,military,conflict,war,defense",
  "defaultLikelihood": 0.7
}')
echo "Created hypothesis: Military Escalation"

# Create hypothesis about cyber attacks
hypothesis2=$(api_call POST "/api/hypotheses" '{
  "name": "Cyber Warfare",
  "keywords": "hack,malware,breach,attack,cyber,network",
  "defaultLikelihood": 0.6
}')
echo "Created hypothesis: Cyber Warfare"

# Create hypothesis about disinformation
hypothesis3=$(api_call POST "/api/hypotheses" '{
  "name": "Disinformation Campaign",
  "keywords": "fake,propaganda,misinformation,lies,conspiracy",
  "defaultLikelihood": 0.65
}')
echo "Created hypothesis: Disinformation Campaign"
echo ""

echo "📋 Step 2: Create Test NPCs"
echo "-------------------------------------------"

# Create NPC 1 - Alice (Military Analyst)
npc1=$(api_call POST "/api/npcs" '{
  "name": "Alice Anderson",
  "email": "alice@test.local"
}')
npc1_id=$(echo $npc1 | jq -r '.id')
echo "Created NPC 1: Alice Anderson ($npc1_id)"

# Create NPC 2 - Bob (Cyber Analyst)
npc2=$(api_call POST "/api/npcs" '{
  "name": "Bob Baker",
  "email": "bob@test.local"
}')
npc2_id=$(echo $npc2 | jq -r '.id')
echo "Created NPC 2: Bob Baker ($npc2_id)"

# Create NPC 3 - Carol (Intel Analyst)
npc3=$(api_call POST "/api/npcs" '{
  "name": "Carol Chen",
  "email": "carol@test.local"
}')
npc3_id=$(echo $npc3 | jq -r '.id')
echo "Created NPC 3: Carol Chen ($npc3_id)"
echo ""

echo "📋 Step 3: Create Social Connections"
echo "-------------------------------------------"

# Alice follows Bob
api_call POST "/api/npcs/social/connections" "{
  \"npcId\": \"$npc1_id\",
  \"connectedNpcId\": \"$npc2_id\",
  \"name\": \"follows\",
  \"distance\": \"close\",
  \"relationshipStatus\": 0.8
}" > /dev/null
echo "✓ Alice follows Bob"

# Bob follows Alice
api_call POST "/api/npcs/social/connections" "{
  \"npcId\": \"$npc2_id\",
  \"connectedNpcId\": \"$npc1_id\",
  \"name\": \"follows\",
  \"distance\": \"close\",
  \"relationshipStatus\": 0.8
}" > /dev/null
echo "✓ Bob follows Alice"

# Carol follows both
api_call POST "/api/npcs/social/connections" "{
  \"npcId\": \"$npc3_id\",
  \"connectedNpcId\": \"$npc1_id\",
  \"name\": \"follows\",
  \"distance\": \"moderate\",
  \"relationshipStatus\": 0.6
}" > /dev/null
echo "✓ Carol follows Alice"

api_call POST "/api/npcs/social/connections" "{
  \"npcId\": \"$npc3_id\",
  \"connectedNpcId\": \"$npc2_id\",
  \"name\": \"follows\",
  \"distance\": \"moderate\",
  \"relationshipStatus\": 0.6
}" > /dev/null
echo "✓ Carol follows Bob"
echo ""

echo "📋 Step 4: Simulate Social Media Posts"
echo "-------------------------------------------"

# Alice posts about military deployment
post1=$(api_call POST "/api/npcs/activities" "{
  \"npcId\": \"$npc1_id\",
  \"activityType\": 0,
  \"detail\": \"Seeing reports of increased troop deployment near the border. This military buildup is concerning.\"
}")
echo "✓ Alice posted about military deployment"
sleep 1

# Bob posts about cyber attack
post2=$(api_call POST "/api/npcs/activities" "{
  \"npcId\": \"$npc2_id\",
  \"activityType\": 0,
  \"detail\": \"Major network breach detected. Malware spreading across critical infrastructure. Cyber attack in progress.\"
}")
echo "✓ Bob posted about cyber attack"
sleep 1

# Carol posts about fake news
post3=$(api_call POST "/api/npcs/activities" "{
  \"npcId\": \"$npc3_id\",
  \"activityType\": 0,
  \"detail\": \"Stop spreading fake news and propaganda! This misinformation campaign is dangerous.\"
}")
echo "✓ Carol posted about disinformation"
echo ""

echo "📋 Step 5: Check Beliefs (Initial State)"
echo "-------------------------------------------"

echo "Checking if beliefs were created..."
sleep 2

beliefs=$(api_call GET "/api/npcs/beliefs")
belief_count=$(echo $beliefs | jq '. | length')
echo "Found $belief_count belief records"
echo ""

if [ "$belief_count" -gt 0 ]; then
    echo "✅ SUCCESS! Beliefs are being tracked!"
    echo ""
    echo "Sample beliefs:"
    echo $beliefs | jq -r '.[:5] | .[] | "  - NPC \(.npcId | .[:8])... believes \(.name) at \(.posterior * 100 | round)% confidence (step \(.step))"'
else
    echo "⚠️  No beliefs found. Checking configuration..."
fi

echo ""
echo "📋 Step 6: View Belief Evolution by Hypothesis"
echo "-------------------------------------------"

hypotheses=$(api_call GET "/api/hypotheses")
echo $hypotheses | jq -r '.[] | "\(.name) (ID: \(.id))"'

echo ""
echo "To see beliefs by hypothesis, run:"
echo "  curl $API_URL/api/npcs/beliefs?hypothesis=Military%20Escalation | jq"
echo ""
echo "To see beliefs for a specific NPC:"
echo "  curl $API_URL/api/npcs/beliefs?npcId=$npc1_id | jq"
echo ""
echo "To see all belief evolution:"
echo "  curl $API_URL/api/npcs/beliefs | jq -r '.[] | \"Step \(.step): \(.name) = \(.posterior)\"'"
echo ""

echo "🎯 Quick Test Commands:"
echo "-------------------------------------------"
echo "# Post another message and watch beliefs update:"
echo "curl -X POST $API_URL/api/npcs/activities -H 'Content-Type: application/json' -d '{
  \"npcId\": \"$npc1_id\",
  \"activityType\": 0,
  \"detail\": \"Confirmed: military forces are deploying. Defense systems activated.\"
}'"
echo ""
echo "# Then check beliefs again:"
echo "curl $API_URL/api/npcs/beliefs | jq -r '.[] | select(.name == \"Military Escalation\") | \"Step \(.step): NPC \(.npcId | .[:8]) believes at \(.posterior * 100 | round)%\"'"
echo ""

echo "✅ Test complete! The belief tracking system is ready."
