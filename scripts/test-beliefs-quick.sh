#!/bin/bash
# Quick Belief Tracking Test
# Tests belief evolution through social posts, no Ollama required

set -e
API_URL="${API_URL:-http://localhost:5000}"

echo "Quick Belief Tracking Test"
echo "=========================================="
echo ""

# 1. Create hypotheses
echo "1. Creating Hypotheses..."
h1=$(curl -sf -X POST "$API_URL/api/hypotheses" -H "Content-Type: application/json" -d '{
  "name": "Security Threat",
  "keywords": "attack,breach,hack,malware,threat",
  "defaultLikelihood": 0.7
}' | jq -r '.id')
echo "   Security Threat (ID: $h1)"

# 2. Create NPCs via the generate endpoint (creates full profiles)
echo ""
echo "2. Creating NPCs..."
npc1=$(curl -sf -X POST "$API_URL/api/npcs/generate" -H "Content-Type: application/json" | jq -r '.id')
echo "   NPC1: $npc1"
npc2=$(curl -sf -X POST "$API_URL/api/npcs/generate" -H "Content-Type: application/json" | jq -r '.id')
echo "   NPC2: $npc2"
npc3=$(curl -sf -X POST "$API_URL/api/npcs/generate" -H "Content-Type: application/json" | jq -r '.id')
echo "   NPC3: $npc3"

# If generate doesn't work, fall back to existing NPCs
if [ "$npc1" = "null" ] || [ -z "$npc1" ]; then
    echo "   generate endpoint unavailable, using existing NPCs..."
    npc1=$(curl -sf "$API_URL/api/npcs" | jq -r '.[0].id')
    npc2=$(curl -sf "$API_URL/api/npcs" | jq -r '.[1].id')
    npc3=$(curl -sf "$API_URL/api/npcs" | jq -r '.[2].id')
    echo "   NPC1: $npc1"
    echo "   NPC2: $npc2"
    echo "   NPC3: $npc3"
fi

# 3. Create social connections: POST /api/npcs/{id}/connections
echo ""
echo "3. Creating Social Network..."
for from in "$npc1" "$npc2" "$npc3"; do
    for to in "$npc1" "$npc2" "$npc3"; do
        if [ "$from" != "$to" ]; then
            curl -sf -X POST "$API_URL/api/npcs/$from/connections" \
              -H "Content-Type: application/json" \
              -d "{\"connectedNpcId\": \"$to\", \"name\": \"follows\", \"distance\": \"close\", \"relationshipStatus\": 0.8}" > /dev/null 2>&1 || true
        fi
    done
done
echo "   Created connections (fully connected graph)"

# 4. Post social media activities
echo ""
echo "4. Simulating Social Media Posts..."
curl -sf -X POST "$API_URL/api/npcs/$npc1/activity?activityType=SocialMediaPost&detail=Just%20detected%20a%20major%20malware%20attack%20on%20our%20network!%20This%20breach%20is%20serious." > /dev/null
echo "   NPC1 posted about malware attack"

curl -sf -X POST "$API_URL/api/npcs/$npc2/activity?activityType=SocialMediaPost&detail=Confirmed%20hack%20attempt.%20Multiple%20systems%20showing%20signs%20of%20breach." > /dev/null
echo "   NPC2 posted about hack"

curl -sf -X POST "$API_URL/api/npcs/$npc3/activity?activityType=SocialMediaPost&detail=Security%20team%20alert:%20active%20threat%20detected.%20Malware%20spreading%20fast." > /dev/null
echo "   NPC3 posted about threat"

# 5. Check results
echo ""
echo "5. Checking Beliefs..."
sleep 1

beliefs=$(curl -sf "$API_URL/api/npcs/$npc1/beliefs")
count=$(echo "$beliefs" | jq 'length')

echo "   NPC1 beliefs: $count"

beliefs2=$(curl -sf "$API_URL/api/npcs/$npc2/beliefs")
count2=$(echo "$beliefs2" | jq 'length')
echo "   NPC2 beliefs: $count2"

beliefs3=$(curl -sf "$API_URL/api/npcs/$npc3/beliefs")
count3=$(echo "$beliefs3" | jq 'length')
echo "   NPC3 beliefs: $count3"

total=$((count + count2 + count3))
echo ""
if [ "$total" -gt 0 ]; then
    echo "SUCCESS! $total belief records created."
    echo ""
    echo "Belief details:"
    echo "$beliefs" | jq -r '.[] | "   NPC1 step \(.step): \(.name) = \((.posterior * 100) | round)%"'
    echo "$beliefs2" | jq -r '.[] | "   NPC2 step \(.step): \(.name) = \((.posterior * 100) | round)%"'
    echo "$beliefs3" | jq -r '.[] | "   NPC3 step \(.step): \(.name) = \((.posterior * 100) | round)%"'
else
    echo "WARNING: No beliefs created."
    echo "   Make sure the API has been restarted with the latest code."
fi

echo ""
echo "=========================================="
echo "Done."
