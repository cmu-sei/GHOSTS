
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
Generate 50 sentences, using the following EXAMPLE where ANIMAL_A is replaced by a random animal name such as cat, dog, bear, tiger, marmot, wolf, elephant, cobra, sheep, cow, horse, frog, turtle.

EXAMPLE:
I have found a lost ANIMAL_A, who do I contact?

EXAMPLE:
I have a cat stuck in a tree, can anyone help?

EXAMPLE:
What is the most docile breed of dog?


EXAMPLE:
What ANIMAL_A makes the best pet for children?


EXAMPLE:
Do you have a  veterinarian recommendation for ANIMAL_A?

'''
gen_prompts(llm, 'animals', prompt, 'animal_content.yml')



