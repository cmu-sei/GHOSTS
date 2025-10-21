import os
import sys
import random
import time
import requests
import mimetypes
from io import BytesIO
from datetime import datetime

# --- Config ---
API_URL = "http://localhost:5000/?theme=facebook"
IMG_DIR = "/tmp"   # local fallback directory
SAVE_DIR = "/tmp/images"

# --- Utils ---
def generate_message():
    """Stub message generator. Replace with your LLM call if needed."""
    messages = [
        "pitchfork in a haybale near a barn in the countryside with a blue sky background",
    ]
    return random.choice(messages)

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
    ext = mimetypes.guess_extension(content_type.split(";")[0].strip()) or ".jpg"
    filename = url.split("/")[-1].split("?")[0] or f"image{ext}"
    return resp.content, content_type, filename

# --- Local ---
def get_random_local_image():
    files = [f for f in os.listdir(IMG_DIR) if os.path.isfile(os.path.join(IMG_DIR, f))]
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
    resp = requests.post(API_URL, files=files, data=data)
    print(f"[{resp.status_code}] u={username} m={message[:50]} img={filename} ({content_type})")
    print("Response:", resp.text[:200])
    return resp

# --- Main ---
def main(mode, pexels_key):
    while True:
        try:
            username = f"user{random.randint(1000,9999)}"
            message = generate_message()

            if mode == "local":
                img_bytes, mime, filename = get_random_local_image()
                print(f"Message: {message}\nImage from local: {filename}")
            else:
                img_url = find_image_url(message, pexels_key)
                if img_url:
                    print(f"Message: {message}\nImage from Pexels: {img_url}")
                    img_bytes, mime, filename = download_image(img_url)
                else:
                    print(f"Message: {message}\nNo Pexels image found, falling back to local")
                    img_bytes, mime, filename = get_random_local_image()

            local_path = save_image(img_bytes, filename)
            print(f"Saved image to {local_path}")

            post_message_and_image(username, message, img_bytes, mime, filename)
            time.sleep(3)

        except KeyboardInterrupt:
            print("Stopping...")
            break
        except Exception as e:
            print(f"Error: {e}")
            time.sleep(5)

if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "web"
    pexels_key = sys.argv[2] if len(sys.argv) > 1 else sys.exit(-1)
    if mode not in ("local", "web"):
        print("Usage: python run.py [local|web]")
        sys.exit(1)
    main(mode, pexels_key)
