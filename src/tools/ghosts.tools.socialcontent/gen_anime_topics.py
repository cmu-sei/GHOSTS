
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
Generate 50 sentences, about random anime topics such as anime fandom, anime conventions,
anime cosplay, fullmetal alchemist, cowboy bebop, one piece, monster, neon genesis, 
samurai champloo, code geass, demon slayer, ouran high school host club,
attack on titan, death note, naruto shippuden, jojo's bizarre adventure,
dragon ball z, trigun, gurren lagann, hunter x hunter, fruits basket, 
my neighbor totoro, nauscicaa of the valley of wind, spirited away,
howls moving castle

'''
gen_prompts(llm, 'anime', prompt, 'anime_content.yml')



