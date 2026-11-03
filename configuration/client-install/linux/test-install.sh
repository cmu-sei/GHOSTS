#!/bin/sh
# Tests install.sh by installing into a temporary root: no user is created and systemd is not touched.
# Run: sh configuration/client-install/linux/test-install.sh
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
fail=0
check() { if eval "$2"; then echo "ok   - $1"; else echo "FAIL - $1"; fail=1; fi; }

# A package shaped like `dotnet publish` output.
mkdir -p "$WORK/pkg/config"
echo "dll" > "$WORK/pkg/Ghosts.Client.Universal.dll"
echo '{"Handlers":[]}' > "$WORK/pkg/config/timeline.json"
(cd "$WORK/pkg" && zip -qr "$WORK/client.zip" .)

sh "$HERE/install.sh" --root "$WORK/root" --api http://ghosts-api:5000/api --package "$WORK/client.zip" --user npc --dir /opt/ghosts > "$WORK/out.log" 2>&1
check "install exits 0" "[ $? -eq 0 ]"
check "the client is unpacked into the install dir" "[ -f '$WORK/root/opt/ghosts/Ghosts.Client.Universal.dll' ]"
check "the package's timeline is kept" "[ -f '$WORK/root/opt/ghosts/config/timeline.json' ]"
CONF="$WORK/root/opt/ghosts/config/application.json"
check "the config is valid JSON" "python3 -c 'import json,sys;json.load(open(sys.argv[1],encoding=\"utf-8-sig\"))' '$CONF'"
check "the config points at the API" "python3 -c 'import json,sys;d=json.load(open(sys.argv[1],encoding=\"utf-8-sig\"));sys.exit(d[\"ApiRootUrl\"]!=\"http://ghosts-api:5000/api\")' '$CONF'"
check "the config reports the hostname, not a VMware name" "python3 -c 'import json,sys;d=json.load(open(sys.argv[1],encoding=\"utf-8-sig\"));sys.exit(d[\"Id\"][\"IsEnabled\"] is not False)' '$CONF'"
check "the config keeps the socket on for pushed updates" "python3 -c 'import json,sys;d=json.load(open(sys.argv[1],encoding=\"utf-8-sig\"));sys.exit(d[\"Sockets\"][\"IsEnabled\"] is not True)' '$CONF'"
UNIT="$WORK/root/etc/systemd/system/ghosts-client.service"
check "a systemd unit is written" "[ -f '$UNIT' ]"
check "the unit runs the client from the install dir" "grep -q '^ExecStart=/usr/bin/dotnet /opt/ghosts/Ghosts.Client.Universal.dll$' '$UNIT'"
check "the unit sets BASE_URL to the API" "grep -q '^Environment=BASE_URL=http://ghosts-api:5000/api$' '$UNIT'"
check "the unit runs as the given user" "grep -q '^User=npc$' '$UNIT'"

sh "$HERE/install.sh" --root "$WORK/root2" --package "$WORK/client.zip" > /dev/null 2>&1
check "a missing --api is refused with exit 2" "[ $? -eq 2 ]"

sh "$HERE/install.sh" --root "$WORK/root" --api http://ghosts-api:5000/api --package "$WORK/client.zip" --user npc --dir /opt/ghosts > /dev/null 2>&1
check "installing again over an earlier install exits 0" "[ $? -eq 0 ]"

mkdir -p "$WORK/root3/opt/shared" && echo "keep" > "$WORK/root3/opt/shared/other-app.conf"
sh "$HERE/install.sh" --root "$WORK/root3" --api http://ghosts-api:5000/api --package "$WORK/client.zip" --dir /opt/shared > /dev/null 2>&1
check "a directory holding something else is refused with exit 2" "[ $? -eq 2 ]"
check "and nothing is unpacked into it" "[ ! -f '$WORK/root3/opt/shared/Ghosts.Client.Universal.dll' ]"

exit $fail
