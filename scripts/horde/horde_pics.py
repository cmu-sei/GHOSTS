import os
import sys
import random
import time
import requests
import mimetypes
import json
from io import BytesIO
from datetime import datetime
# faker
from faker import Faker
fake = Faker()

# --- Config ---
sites = [
    "facebook",
    "reddit",
    "x",
    "instagram",
    "discord",
    "linkedin",
    "youtube",
]
API_URL = "http://localhost:8800"
IMG_DIR = "/tmp"   # local fallback directory
SAVE_DIR = "/tmp/images"
SCENARIO_FILE = "cyber-exercise-scenario.json"
OLLAMA_URL = "http://localhost:11434/api/generate"
OLLAMA_MODEL = "llama3.2"

# --- Scenario & Knowledge Levels ---


def load_scenario():
    """Load scenario from JSON file."""
    script_dir = os.path.dirname(os.path.abspath(__file__))
    scenario_path = os.path.join(script_dir, SCENARIO_FILE)

    if not os.path.exists(scenario_path):
        print(f"Warning: Scenario file not found at {scenario_path}")
        return None

    with open(scenario_path, 'r') as f:
        return json.load(f)


def select_knowledge_level():
    """Select a knowledge level based on realistic distribution."""
    # Full knowledge: 20% (scenario designers, white cell)
    # Department knowledge: 50% (most participants)
    # Limited knowledge: 30% (support staff, observers)
    rand = random.random()
    if rand < 0.2:
        return "full"
    elif rand < 0.7:
        return "department"
    else:
        return "limited"


def call_ollama(prompt):
    """Call Ollama API to generate text."""
    try:
        payload = {
            "model": OLLAMA_MODEL,
            "prompt": prompt,
            "stream": False,
            "options": {
                "temperature": 0.8,
                "num_predict": 100
            }
        }
        resp = requests.post(OLLAMA_URL, json=payload, timeout=30)
        resp.raise_for_status()
        return resp.json().get("response", "").strip()
    except Exception as e:
        print(f"Ollama error: {e}")
        return None

# --- Utils ---


def generate_search_query_and_post(scenario=None):
    """Generate both a Pexels search query and a realistic social media post message."""
    knowledge = select_knowledge_level()

    # Fallback data if scenario not available or Ollama fails
    fallback_data = [
        {"query": "industrial control panel buttons displays",
            "post": "Running diagnostics on the PLCs again"},
        {"query": "server room equipment racks",
            "post": "Late night troubleshooting network issues"},
        {"query": "SCADA monitoring station multiple screens",
            "post": "Another anomaly to investigate today"},
        {"query": "engineering workstation computer monitors",
            "post": "Reviewing process logs from last night"},
        {"query": "conference room presentation whiteboard",
            "post": "Long briefing about system changes"},
        {"query": "industrial facility control room",
            "post": "Monitoring systems all shift"},
        {"query": "office desk laptop coffee",
            "post": "Engineers seem really busy today"},
        {"query": "security badge access control",
            "post": "Lots of people here late tonight"},
    ]

    if not scenario:
        data = random.choice(fallback_data)
        return data["query"], data["post"]

    # Extract scenario details
    scenario_name = scenario.get("name", "cyber exercise")
    description = scenario.get("description", "")
    tech_env = scenario.get("technicalEnvironment", {})
    assets = tech_env.get("assets", "")
    services = tech_env.get("services", "")

    # Build context-appropriate prompts for Ollama based on knowledge level
    if knowledge == "full":
        # Full knowledge: include specific scenario details
        search_prompt = f"""You are creating a realistic photo search query for an image that would be posted during this scenario: {scenario_name}

Scenario context: {description}

Key assets: {assets}

Generate a single, specific search query (5-10 words) for a stock photo that would realistically be taken or shared by someone involved in this scenario. The photo should show workplace objects, equipment, or settings related to industrial control systems, SCADA, or critical infrastructure operations.

Search query:"""

        post_prompt = f"""You are writing a realistic social media post from someone working on this scenario: {scenario_name}

Scenario context: {description}

Key assets they work with: {assets}

Write a casual, natural social media post (1-2 sentences, max 15 words) about their workday that references specific activities from the scenario. For example: checking control systems, reviewing logs, investigating anomalies, running diagnostics on PLCs, monitoring SCADA, dealing with vendor maintenance, reviewing process data, etc.

The post should sound natural and slightly vague (like real people post about work) but clearly relate to industrial control systems work happening in this scenario.

Do NOT include hashtags. Write like a real person would casually post.

Post:"""

    elif knowledge == "department":
        # Department knowledge: knows it's ICS/SCADA related but not full details
        search_prompt = """You are creating a realistic photo search query for an image that would be posted by someone working in industrial control systems or SCADA operations.

Generate a single, specific search query (5-10 words) for a stock photo showing workplace objects, control equipment, monitoring stations, or industrial facility interiors that someone in this field might photograph and share.

Search query:"""

        post_prompt = """You are writing a realistic social media post from someone working in industrial control systems or SCADA operations during a cyber exercise or incident response.

Write a casual, natural social media post (1-2 sentences, max 15 words) about their workday that references ICS/SCADA activities like: monitoring systems, checking alarms, reviewing process data, running diagnostics, dealing with anomalies, coordinating with engineering, or troubleshooting equipment.

Keep it vague (don't mention specific vulnerabilities or attacks) but clearly relate to ICS/SCADA operational work.

Do NOT include hashtags. Write like a real person would casually post.

Post:"""

    else:
        # Limited knowledge: just knows it's workplace/office related
        search_prompt = """You are creating a realistic photo search query for an image that would be posted by someone at work in a technical or industrial environment.

Generate a single, specific search query (5-10 words) for a stock photo showing generic workplace objects, office equipment, or facility interiors that someone might photograph and share during their workday.

Search query:"""

        post_prompt = """You are writing a realistic social media post from someone working support or admin at an industrial facility during a busy/unusual time.

Write a casual, natural social media post (1-2 sentences, max 15 words) that hints something is going on without knowing details. Examples: "everyone seems stressed today", "lots of meetings happening", "IT dept is busy", "engineers working late again", "something's up with the systems".

Keep it vague and relatable - like someone who notices activity but isn't directly involved.

Do NOT include hashtags. Write like a real person would casually post.

Post:"""

    # Call Ollama to generate both search query and post message
    search_query = call_ollama(search_prompt)
    post_message = call_ollama(post_prompt)

    if search_query and len(search_query) > 5:
        search_query = search_query.split('\n')[0].strip().strip('"\'')
    else:
        data = random.choice(fallback_data)
        search_query = data["query"]

    if post_message and len(post_message) > 3:
        post_message = post_message.split('\n')[0].strip().strip('"\'')
    else:
        data = random.choice(fallback_data)
        post_message = data["post"]

    print(f"[Knowledge: {knowledge}]")
    print(f"  Search query: {search_query}")
    print(f"  Post message: {post_message}")

    return search_query, post_message


def save_image(img_bytes, filename):
    """Save image bytes to disk under downloads/ with a unique filename."""
    os.makedirs(SAVE_DIR, exist_ok=True)
    base, ext = os.path.splitext(filename)
    ts = datetime.utcnow().strftime("%Y%m%d%H%M%S%f")
    unique_name = f"{base}_{ts}{ext or '.jpg'}"
    path = os.path.join(SAVE_DIR, unique_name)
    with open(path, "wb") as f:
        f.write(img_bytes)
    return path

# --- Pexels ---


def find_image_url(query, pexels_key):
    headers = {"Authorization": pexels_key}
    resp = requests.get(
        "https://api.pexels.com/v1/search",
        params={"query": query, "per_page": 30},
        headers=headers,
        timeout=10,
        verify=False
    )
    resp.raise_for_status()
    data = resp.json()
    if not data.get("photos"):
        return None
    return random.choice(data["photos"])["src"]["medium"]


def download_image(url):
    resp = requests.get(url, stream=True, timeout=10, verify=False)
    resp.raise_for_status()
    content_type = resp.headers.get("Content-Type", "application/octet-stream")
    ext = mimetypes.guess_extension(
        content_type.split(";")[0].strip()) or ".jpg"
    filename = url.split("/")[-1].split("?")[0] or f"image{ext}"
    return resp.content, content_type, filename

# --- Local ---


def get_random_local_image():
    files = [f for f in os.listdir(
        IMG_DIR) if os.path.isfile(os.path.join(IMG_DIR, f))]
    if not files:
        raise RuntimeError("No images in directory")
    img_path = os.path.join(IMG_DIR, random.choice(files))
    mime = mimetypes.guess_type(img_path)[0] or "application/octet-stream"
    with open(img_path, "rb") as f:
        return f.read(), mime, os.path.basename(img_path)

# --- Poster ---


def post_message_and_image(username, message, img_bytes, content_type, filename):
    files = {"file": (filename, BytesIO(img_bytes), content_type)}
    data = {"u": username, "m": message}
    resp = requests.post(API_URL + "?theme=" +
                         random.choice(sites), files=files, data=data)
    print(f"[{resp.status_code}] u={username} m={message[:50]} img={filename} ({content_type})")
    print("Response:", resp.text[:200])
    return resp

# --- Main ---


def main(mode, pexels_key):
    # Load scenario once at startup
    print("Loading scenario...")
    scenario = load_scenario()
    if scenario:
        print(f"Loaded scenario: {scenario.get('name', 'Unknown')}")
    else:
        print("No scenario loaded, using fallback prompts")

    while True:
        try:
            username = fake.user_name()
            search_query, post_message = generate_search_query_and_post(
                scenario)

            if mode == "local":
                img_bytes, mime, filename = get_random_local_image()
                print(f"Image from local: {filename}")
            else:
                img_url = find_image_url(search_query, pexels_key)
                if img_url:
                    print(f"Image from Pexels: {img_url}")
                    img_bytes, mime, filename = download_image(img_url)
                else:
                    print(
                        f"No Pexels image found for '{search_query}', falling back to local")
                    img_bytes, mime, filename = get_random_local_image()

            local_path = save_image(img_bytes, filename)
            print(f"Saved image to {local_path}")

            post_message_and_image(
                username, post_message, img_bytes, mime, filename)
            time.sleep(3)

        except KeyboardInterrupt:
            print("Stopping...")
            break
        except Exception as e:
            print(f"Error: {e}")
            time.sleep(5)


if __name__ == "__main__":
    # print(len(sys.argv))
    # print(sys.argv[0])
    # print(sys.argv[1])

    mode = "web"  # sys.argv[1] if len(sys.argv) > 1 else "web"
    pexels_key = sys.argv[1] if len(sys.argv) > 0 else sys.exit(1)
    print(f"Running in {mode} mode with Pexels key {pexels_key[:8]}...")
    if mode not in ("local", "web"):
        print("Usage: python run.py [local|web]")
        sys.exit(1)
    main(mode, pexels_key)
