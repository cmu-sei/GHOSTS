import requests
import random
import json
import os
import time
from faker import Faker
from typing import NoReturn

# Initialize the Faker instance for generating fake names
fake = Faker()


def post_data(url, data) -> str | None:
    """Send a JSON payload to the specified URL."""
    try:
        payload = json.dumps({"query": data})
        headers = {"Content-Type": "application/json"}
        response = requests.post(url, headers=headers, data=payload)
        response.raise_for_status()  # Raise an error for bad responses
        return response.text
    except requests.exceptions.RequestException as e:
        print(f"Error posting data to {url}: {e}")
        return None


def post_social(url, user, data) -> None:
    """Post user data to the socializer."""
    payload = f"u={user}&m={data}"
    headers = {"Content-Type": "application/x-www-form-urlencoded"}

    try:
        response = requests.post(url, headers=headers, data=payload)
        response.raise_for_status()
        print("========================================================")
        print(data)
        print(response.text)
    except requests.exceptions.RequestException as e:
        print(f"Error posting social data to {url}: {e}")


def generate_fake_name() -> str:
    """Generate a random full name."""
    return f"{fake.first_name()} {fake.last_name()}"


def load_users(file_path) -> list[str]:
    """Load users from the specified CSV file."""
    if not os.path.exists(file_path):
        print(f"{file_path} does not exist. Creating a new file with random users.")
        with open(file_path, "w") as f:
            f.write("firstname,lastname,party,strength\n")
            for _ in range(100):
                first_name = fake.first_name()
                last_name = fake.last_name()
                party = random.choice(["Republican", "Democrat"])
                strength = random.randint(1, 100)
                f.write(f"{first_name},{last_name},{party},{strength}\n")

    with open(file_path, "r") as f:
        data = f.readlines()[1:]  # Skip the header line
    return data


def load_scandals(file_path) -> str | None:
    """Load scandals from the specified text file."""
    try:
        with open(file_path, "r") as f:
            scandals = f.readlines()
        return random.choice(scandals).strip()
    except FileNotFoundError:
        print(f"Error: {file_path} not found.")
        return None


def main() -> NoReturn:
    """Main function to run the GHOSTS HORDE simulation."""
    print("""
              ('-. .-.               .-')    .-') _     .-')    
            ( OO )  /              ( OO ). (  OO) )   ( OO ).  
  ,----.    ,--. ,--. .-'),-----. (_)---\_)/     '._ (_)---\_) 
 '  .-./-') |  | |  |( OO'  .-.  '/    _ | |'--...__)/    _ |  
 |  |_( O- )|   .|  |/   |  | |  |\  :` `. '--.  .--'\  :` `.  
 |  | .--, \|       |\_) |  |\|  | '..`''.)   |  |    '..`''.) 
(|  | '. (_/|  .-.  |  \ |  | |  |.-._)   \   |  |   .-._)   \ 
 |  '--'  | |  | |  |   `'  '-'  '\       /   |  |   \       / 
  `------'  `--' `--'     `-----'  `-----'    `--'    `-----'  
    """)

    print("A GHOSTS HORDE is coming online...")

    shadow_host = os.getenv("GHOSTS_SHADOWS", "http://localhost:5900/social")
    socializer_host = os.getenv("GHOSTS_SOCIALIZER", "http://socializer.com")
    delay = int(os.getenv("GHOSTS_DELAY", 30))

    users = load_users("horde_users.csv")
    scandal = load_scandals("scandals.txt")

    if scandal:
        print(f"Starting a rumor mill about the following scandal: {scandal}")

    # Loop to continuously send data
    while True:
        record = random.choice(users).strip().split(",")
        fake_name = f"{record[0]} {record[1]}"
        party = record[2]
        partisan_level = record[3]

        prompt = (
            f"I want to tweet about the following scandal in El Marador: `{scandal}`. "
            f"Write me a tweet, but don't refer to me ever! I am a {party} "
            f"with a strength of belief in my part of {partisan_level} out of 100. "
            "If that strength is a low number, then I will be questioning things; "
            "if it is high, then I will be trying to convince others to join me."
        )

        print("========================================================")
        print(f"{fake_name} {party} {partisan_level}")

        response = post_data(shadow_host, prompt)
        if response:
            print(response)
            # Uncomment if you want to post to socializer
            # post_social(socializer_host, fake_name, response)

        # Sleep for a random amount of time between iterations
        time.sleep(random.randint(1, delay))


if __name__ == "__main__":
    main()
