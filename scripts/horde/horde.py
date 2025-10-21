import requests
import time
import random
import ollama

import psycopg2
import random

# Adjust for your setup
DB_HOST = "localhost"
DB_PORT = 5432
DB_NAME = "ghosts"
DB_USER = "ghosts"
DB_PASS = "scotty@1"

# Configure these
API_URL = "http://localhost:5000"
OLLAMA_MODEL = "mistral:7b"
POST_DELAY = 0  # seconds between posts

sites = [
    "facebook",
    "reddit",
    "x",
    "instagram",
    "discord",
    "linkedin",
    "youtube",
]

# Message prompts for variety //todo: expand or customize
prompts = [
    "Write a provocative satire about political extremism in a fictional nation called Kerbleckistan. Use sharp humor and irony to highlight absurdity.",
    "Write about underground robots ('beepboops') who develop an obsession with donuts. Use formal British English.",
    "Write something deliberately awkward and cringy, like an over-the-top motivational post that slightly misses the mark.",
    "Write a reflective post about a philosophical idea—such as identity, morality, or meaning—in an accessible tone.",
    "Write an absurdist post blending political history with zombie apocalypse themes. Keep it surreal and metaphorical."
]

def get_random_user():
    conn = psycopg2.connect(
        host=DB_HOST, port=DB_PORT, dbname=DB_NAME,
        user=DB_USER, password=DB_PASS
    )
    cur = conn.cursor()
    cur.execute('SELECT npcprofile from npcs ORDER BY RANDOM() LIMIT 1;')
    row = cur.fetchone()
    conn.close()

    if not row:
        return f"user{random.randint(1000,9999)}"

    try:
        profile = row[0]  # parse JSON string
        # Example: choose username if available, else fallback to Name.First + Last
        if "Accounts" in profile and profile["Accounts"]:
            return profile["Accounts"][0].get("Username", "anon")
        elif "Name" in profile:
            first = profile["Name"].get("First", "")
            last = profile["Name"].get("Last", "")
            return f"{first}{last}".lower()
    except Exception as e:
        print(f"JSON parse error: {e}")
    return f"user{random.randint(1000,9999)}"

def build_prompt():
    p = """write me a tweet and nothing else. never put it in quotes. 
    do not say 'here is a suggested tweet' or anything that reveals you are not the original author. 
    never say anything like 'Here's an example of a provocative and offensive tweet, but please note 
    that I do not endorse or promote such language:'. Never offer any disclaimers. Never say 
    "please note" or similar."""
    p = p + " " + random.choice(prompts)
    return p

def get_ollama_message(prompt):
    try:
        response = ollama.generate(model=OLLAMA_MODEL, prompt=prompt)
        return response['response'].strip().replace('"', '').strip()
    except Exception as e:
        print(f"Ollama error: {e}")
        return f"Failed to generate message: {str(e)}"

def main():
    session = requests.Session()
    post_count = 0
    start_time = time.time()

    while True:
        try:
            url = API_URL + "?theme=" + random.choice(sites)
            username = get_random_user()
            prompt = build_prompt()
            message = get_ollama_message(prompt)

            payload = {"u": username, "m": message}
            response = session.post(url, data=payload)
            post_count += 1

            elapsed = time.time() - start_time
            posts_per_sec = post_count / elapsed
            posts_per_min = posts_per_sec * 60

            print(f"[{response.status_code}] {username}: {message[:50]}...")
            print(f"{url} processed {post_count} posts | {posts_per_sec:.2f}/sec | {posts_per_min:.2f}/min")

            time.sleep(POST_DELAY)

        except KeyboardInterrupt:
            print("\nStopping...")
            break
        except requests.exceptions.RequestException as e:
            print(f"Request error: {e}")
            time.sleep(5)
        except Exception as e:
            print(f"Unexpected error: {e}")
            time.sleep(5)

if __name__ == "__main__":
    main()
