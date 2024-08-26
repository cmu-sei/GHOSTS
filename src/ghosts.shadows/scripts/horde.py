import requests
import random
import json
import os
import time
from faker import Faker

fake = Faker()

def post_data(url, data):
    payload = json.dumps({"query": data})
    headers = {'Content-Type': 'application/json'}
    response = requests.post(url, headers=headers, data=payload)
    return response.text

def post_social(url, user, data):
    payload = f"u={user}&m={data}"
    headers = {'Content-Type': 'application/x-www-form-urlencoded'}

    response = requests.post(url, headers=headers, data=payload)
    print("========================================================")
    print(data)
    print(response.text)

def generate_fake_name():
    first_name = fake.first_name()
    last_name = fake.last_name()
    return f"{first_name} {last_name}"


if __name__ == '__main__':
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
    delay = os.getenv("GHOSTS_DELAY", 30)

    #does file exist?
    if not os.path.exists("horde_users.csv"):
        with open("horde_users.csv", "w") as f:
            # create a file with 100 random users
            # write the header of firstname, lastname, party, strength
            f.write("firstname,lastname,party,strength\n")
            for i in range(100):
                first_name = fake.first_name()
                last_name = fake.last_name()
                party = random.choice(["Republican", "Democrat"])
                strength = random.randint(1, 100)
                f.write(f"{first_name},{last_name},{party},{strength}\n")
            f.write("")
    
    # load the data from the file
    with open("horde_users.csv", "r") as f:
        data = f.readlines()
        # remove the header
        data = data[1:]

    with open("scandals.txt", "r") as f:
        scandals = f.readlines()
    scandal = random.choice(scandals).strip()
    print(f"Starting a rumor mill about the following scandal: {scandal}")

    # Loop to continuously send data
    while True:
        # pick a random record from data
        record = random.choice(data)
        record = record.strip().split(",")
        fake_name = f"{record[0]} {record[1]}"
        party = record[2]
        partisan_level = record[3]

        prompt = f"I want to tweet about the following scandal in El Marador: `{scandal}`. Write me a tweet, but don't refer to me ever! I am a {party} with a strength of belief in my part of {partisan_level} out of 100. Write me a tweet, but don't refer to me, and don't refer to my strength of belief. If that strength is a low number, then i will be questiong things, if it is high, then i will be trying to convince others to join me."

        #prompt = f"I want to tweet about the critical upcoming election in El Marador. My name is {fake_name}, and I am a {party} with a strength of belief in my part of {partisan_level} out of 100. Write me a tweet, but don't refer to me, and don't refer to my strength of belief. If that strength is a low number, then i will be questiong things, if it is high, then i will be trying to convince others to join me."
        print("========================================================")
        print(f"{fake_name} {party} {partisan_level}")

        response = post_data(shadow_host, prompt).replace('"', '')
        print(response)
        
        #post_social(socializer_host, fake_name, response)
        
        # Sleep for a random amount of time
        # time.sleep(random.randint(1, delay))
        time.sleep(5)
