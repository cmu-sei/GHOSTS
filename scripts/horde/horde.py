import requests
import time
import random
import ollama

import psycopg2
import random

# Adjust for your setup
DB_HOST = "localhost"
DB_PORT = 40669
DB_NAME = "ghosts"
DB_USER = "ghosts"
DB_PASS = "scotty@1"

# Configure these
API_URL = "http://localhost:8800"
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
    "Write a provocative satire about electrical grid instability in the United States where people blame and hate Donovia. Use sharp humor and irony to highlight absurdity.",
    "Write a concerned op-ed questioning whether aging infrastructure or foreign interference is really to blame for recent grid outages, leaving the conclusion unresolved.",
    "Write a sarcastic social media thread mocking experts who dismiss grid instability while everyone’s power keeps flickering.",
    "Write a faux investigative article implying Donovia has mastered psychological warfare by convincing Americans their own grid is unreliable.",
    "Write a meme-style commentary where citizens argue online about whether outages are proof of sabotage or just bureaucratic incompetence.",
    "Write a local news piece quoting anonymous sources who speculate about foreign influence without presenting any hard evidence.",
    "Write a darkly humorous blog post about how people prepare for blackouts by buying generators, candles, and conspiracy theories.",
    "Write a satirical interview with an imaginary Donovian official who denies everything while subtly enjoying the chaos.",
    "Write a community Facebook post rant blaming Donovia, regulators, unions, and weather, all in the same breath.",
    "Write a parody think-tank report that uses impressive charts to say almost nothing conclusive about grid instability.",
    "Write a cynical late-night monologue joke set about how Americans trust their phones more than their power grid.",
    "Write a mock public service announcement warning citizens to stay calm while hinting that someone, somewhere, is definitely messing with the grid."
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
        return f"user{random.randint(1000, 9999)}"

    try:
        profile = row[0]  # parse JSON string
        # Example: choose username if available, else fallback to Name.First + Last
        print("---")
        print(profile)
        print("---")
        if "name" in profile:
            first = profile["name"].get("first", "")
            last = profile["name"].get("last", "")
            return f"{first}{last}".lower()
        if "accounts" in profile and profile["accounts"]:
            print(profile["accounts"])
            return profile["accounts"][0].get("username", "anon")

    except Exception as e:
        print(f"JSON parse error: {e}")
    return f"user{random.randint(1000, 9999)}"


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
            print(
                f"{url} processed {post_count} posts | {posts_per_sec:.2f}/sec | {posts_per_min:.2f}/min")

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
