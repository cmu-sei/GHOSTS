#!/bin/bash
# Belief Tracking Loop - Runs for 1+ hours, posting every 5 minutes
# Creates rising and falling belief curves across multiple hypotheses
#
# Usage: ./test-beliefs-loop.sh [API_URL]
# Stop:  Ctrl+C

API_URL="${1:-${API_URL:-http://localhost:5000}}"
INTERVAL=300  # 5 minutes
CYCLE=0

echo "Belief Tracking Loop"
echo "=========================================="
echo "API:      $API_URL"
echo "Interval: ${INTERVAL}s (5 min)"
echo "Stop:     Ctrl+C"
echo ""

# ── Setup ────────────────────────────────────

echo "Setting up..."

# Create hypotheses with varied likelihoods
# High likelihood (>0.5) → beliefs trend UP when matched
# Low likelihood  (<0.5) → beliefs trend DOWN when matched
echo ""
echo "Creating hypotheses..."

curl -sf -X POST "$API_URL/api/hypotheses" -H "Content-Type: application/json" -d '{
  "name": "Cyber Attack Imminent",
  "keywords": "attack,breach,hack,malware,exploit,intrusion,compromise",
  "defaultLikelihood": 0.75
}' > /dev/null 2>&1
echo "  + Cyber Attack Imminent    (likelihood 0.75 - trends UP)"

curl -sf -X POST "$API_URL/api/hypotheses" -H "Content-Type: application/json" -d '{
  "name": "Systems Are Stable",
  "keywords": "stable,reliable,secure,operational,normal,functioning,healthy",
  "defaultLikelihood": 0.25
}' > /dev/null 2>&1
echo "  + Systems Are Stable       (likelihood 0.25 - trends DOWN)"

curl -sf -X POST "$API_URL/api/hypotheses" -H "Content-Type: application/json" -d '{
  "name": "Insider Threat Active",
  "keywords": "insider,unauthorized,suspicious,anomalous,leak,exfiltration",
  "defaultLikelihood": 0.65
}' > /dev/null 2>&1
echo "  + Insider Threat Active    (likelihood 0.65 - trends UP)"

curl -sf -X POST "$API_URL/api/hypotheses" -H "Content-Type: application/json" -d '{
  "name": "De-escalation Likely",
  "keywords": "peace,resolution,ceasefire,cooperation,agreement,diplomatic,deescalation",
  "defaultLikelihood": 0.30
}' > /dev/null 2>&1
echo "  + De-escalation Likely     (likelihood 0.30 - trends DOWN)"

# Get or create NPCs
echo ""
echo "Preparing NPCs..."
NPC_IDS=$(curl -sf "$API_URL/api/npcs" | jq -r '.[].id')
NPC_COUNT=$(echo "$NPC_IDS" | wc -l)

if [ "$NPC_COUNT" -lt 3 ]; then
    echo "  Need at least 3 NPCs. Generating..."
    for i in 1 2 3 4 5; do
        curl -sf -X POST "$API_URL/api/npcs/generate/one" > /dev/null 2>&1
    done
    NPC_IDS=$(curl -sf "$API_URL/api/npcs" | jq -r '.[].id')
    NPC_COUNT=$(echo "$NPC_IDS" | wc -l)
fi
echo "  $NPC_COUNT NPCs available"

# Store as array
mapfile -t NPCS <<< "$NPC_IDS"

# Create social connections (everyone follows everyone)
echo ""
echo "Creating social connections..."
CONN_COUNT=0
for from in "${NPCS[@]}"; do
    for to in "${NPCS[@]}"; do
        if [ "$from" != "$to" ] && [ -n "$from" ] && [ -n "$to" ]; then
            curl -sf -X POST "$API_URL/api/npcs/$from/connections" \
              -H "Content-Type: application/json" \
              -d "{\"connectedNpcId\": \"$to\", \"name\": \"follows\", \"distance\": \"close\", \"relationshipStatus\": 0.8}" > /dev/null 2>&1
            CONN_COUNT=$((CONN_COUNT + 1))
        fi
    done
done
echo "  $CONN_COUNT connections created"

# ── Post content pools ───────────────────────

# Each pool targets specific hypothesis keywords
CYBER_POSTS=(
    "ALERT: Detected active malware spreading across the network. This looks like a coordinated attack."
    "Security team confirmed a breach in the perimeter. Intrusion detected on multiple endpoints."
    "Another exploit attempt logged. The hack attempts are escalating hourly."
    "Critical: malware variant found in email attachments. Systems are being compromised."
    "Active intrusion in progress. The attack surface is expanding rapidly."
    "Breach confirmed on database servers. This exploit is sophisticated."
    "New malware samples detected. The attack pattern matches known APT groups."
    "Network compromise detected. Unauthorized access through VPN exploit."
)

STABLE_POSTS=(
    "All systems operational and stable. Network is functioning within normal parameters."
    "Routine check complete. Everything is healthy and reliable across all endpoints."
    "Security scan finished. Systems are secure and operational. No anomalies detected."
    "Infrastructure report: all services functioning normally. Stable performance metrics."
    "Weekly assessment: systems remain reliable and secure. Operational capacity at 100%."
    "Network status: healthy. All nodes responding. Stable and operational."
    "Good news: everything is normal. All systems reliable and functioning as expected."
    "Status update: infrastructure is secure and stable. No incidents to report."
)

INSIDER_POSTS=(
    "Flagged: suspicious login from an unauthorized location. Possible insider activity."
    "Data exfiltration alert - anomalous transfer volumes from a privileged account."
    "Unusual: insider access patterns detected. Unauthorized file downloads spiking."
    "Suspicious activity: employee accessing systems outside their role. Potential leak."
    "Alert: anomalous behavior from admin account. Unauthorized data export detected."
    "Insider risk indicator: suspicious after-hours access with exfiltration patterns."
    "Security flag: unauthorized USB device connected. Possible insider data leak."
    "Anomalous database queries from insider account. Suspicious access patterns."
)

DEESCALATION_POSTS=(
    "Diplomatic channels reopened. Both sides signaling cooperation and willingness for agreement."
    "Breaking: ceasefire announcement. De-escalation talks show real progress toward peace."
    "Resolution framework proposed. Diplomatic teams express optimism about cooperation."
    "Peace envoy reports positive progress. Agreement on key terms seems within reach."
    "Joint statement calls for deescalation. Cooperation between parties is strengthening."
    "Ceasefire holding. Diplomatic resolution pathway gaining momentum and support."
    "Agreement reached on humanitarian corridor. Peace process advancing through cooperation."
    "Diplomatic breakthrough: both parties commit to resolution through peaceful means."
)

NEUTRAL_POSTS=(
    "Had a great lunch today. The weather is really nice this afternoon."
    "Just finished reading an interesting book about history."
    "Traffic was terrible this morning. Took twice as long to get in."
    "Team meeting went well. Lots of good discussion about the project roadmap."
    "Looking forward to the weekend. Anyone have plans?"
    "The new coffee machine in the break room is actually pretty good."
)

# ── Helper functions ─────────────────────────

pick_random_npc() {
    local idx=$((RANDOM % ${#NPCS[@]}))
    echo "${NPCS[$idx]}"
}

pick_random_post() {
    local -n arr=$1
    local idx=$((RANDOM % ${#arr[@]}))
    echo "${arr[$idx]}"
}

url_encode() {
    python3 -c "import urllib.parse; print(urllib.parse.quote('$1'))" 2>/dev/null || \
    echo "$1" | sed 's/ /%20/g; s/!/%21/g; s/:/%3A/g; s/,/%2C/g; s/(/%28/g; s/)/%29/g; s/\./%2E/g'
}

post_activity() {
    local npc_id=$1
    local content=$2
    local encoded=$(url_encode "$content")
    curl -sf -X POST "$API_URL/api/npcs/$npc_id/activity?activityType=SocialMediaPost&detail=$encoded" > /dev/null 2>&1
}

# ── Main loop ────────────────────────────────

echo ""
echo "=========================================="
echo "Starting belief loop... (Ctrl+C to stop)"
echo "=========================================="
echo ""

while true; do
    CYCLE=$((CYCLE + 1))
    NOW=$(date '+%H:%M:%S')
    PHASE=$((CYCLE % 6))

    echo "--- Cycle $CYCLE ($NOW) ---"

    # Each cycle posts 3-4 messages from a mix of categories.
    # The mix shifts over time to create waves in the belief curves.

    case $PHASE in
        0)
            # Heavy cyber attack content
            echo "  Theme: Cyber attack escalation"
            for i in 1 2 3; do
                npc=$(pick_random_npc)
                msg=$(pick_random_post CYBER_POSTS)
                post_activity "$npc" "$msg"
                echo "  > Posted cyber alert (NPC ${npc:0:8}...)"
            done
            # One neutral to vary things
            npc=$(pick_random_npc)
            msg=$(pick_random_post NEUTRAL_POSTS)
            post_activity "$npc" "$msg"
            ;;
        1)
            # Stability reports push "Systems Are Stable" DOWN
            echo "  Theme: System stability reports"
            for i in 1 2 3; do
                npc=$(pick_random_npc)
                msg=$(pick_random_post STABLE_POSTS)
                post_activity "$npc" "$msg"
                echo "  > Posted stability report (NPC ${npc:0:8}...)"
            done
            ;;
        2)
            # Insider threat content
            echo "  Theme: Insider threat indicators"
            for i in 1 2; do
                npc=$(pick_random_npc)
                msg=$(pick_random_post INSIDER_POSTS)
                post_activity "$npc" "$msg"
                echo "  > Posted insider alert (NPC ${npc:0:8}...)"
            done
            npc=$(pick_random_npc)
            msg=$(pick_random_post CYBER_POSTS)
            post_activity "$npc" "$msg"
            echo "  > Posted cyber alert (NPC ${npc:0:8}...)"
            ;;
        3)
            # De-escalation content pushes that belief DOWN
            echo "  Theme: De-escalation signals"
            for i in 1 2 3; do
                npc=$(pick_random_npc)
                msg=$(pick_random_post DEESCALATION_POSTS)
                post_activity "$npc" "$msg"
                echo "  > Posted de-escalation news (NPC ${npc:0:8}...)"
            done
            ;;
        4)
            # Mixed: cyber + stability (competing signals)
            echo "  Theme: Mixed signals"
            npc=$(pick_random_npc)
            msg=$(pick_random_post CYBER_POSTS)
            post_activity "$npc" "$msg"
            echo "  > Posted cyber alert (NPC ${npc:0:8}...)"

            npc=$(pick_random_npc)
            msg=$(pick_random_post STABLE_POSTS)
            post_activity "$npc" "$msg"
            echo "  > Posted stability report (NPC ${npc:0:8}...)"

            npc=$(pick_random_npc)
            msg=$(pick_random_post INSIDER_POSTS)
            post_activity "$npc" "$msg"
            echo "  > Posted insider alert (NPC ${npc:0:8}...)"

            npc=$(pick_random_npc)
            msg=$(pick_random_post DEESCALATION_POSTS)
            post_activity "$npc" "$msg"
            echo "  > Posted de-escalation news (NPC ${npc:0:8}...)"
            ;;
        5)
            # Heavy de-escalation + neutral
            echo "  Theme: De-escalation wave + quiet period"
            for i in 1 2; do
                npc=$(pick_random_npc)
                msg=$(pick_random_post DEESCALATION_POSTS)
                post_activity "$npc" "$msg"
                echo "  > Posted de-escalation news (NPC ${npc:0:8}...)"
            done
            for i in 1 2; do
                npc=$(pick_random_npc)
                msg=$(pick_random_post NEUTRAL_POSTS)
                post_activity "$npc" "$msg"
            done
            echo "  > Posted neutral chatter"
            ;;
    esac

    # Quick status check
    belief_count=$(curl -sf "$API_URL/api/npcs/${NPCS[0]}/beliefs" | jq 'length' 2>/dev/null)
    echo "  Beliefs for first NPC: ${belief_count:-0} records"
    echo ""

    echo "  Next cycle in $((INTERVAL / 60)) minutes... (Ctrl+C to stop)"
    sleep $INTERVAL
done
