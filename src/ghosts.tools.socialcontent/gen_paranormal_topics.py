
import ollama
import yaml
import posixpath
import os
from gen_topics_common import gen_prompts

# This file generates a file named movie_content.yml that will have prompts
# for social media posts. This file can be fed to the 'gen_social_posts.py'
# script to generate the content


llm = 'wizardlm2'


prompt = '''
Generate 50 sentences, about topics concerning unidentified flying object (UFOs), alien encounters,
alien abductions, lizard people, bigfoot, mothman, loch ness, nessie, werewolves, vampires, bailisks, fairies, unicorns, and paranormal topics such as ghosts, demons, demonic
possession, exorcism, ghost hunting.


'''
gen_prompts(llm, 'paranormal2', prompt, 'paranormal2_content.yml')



