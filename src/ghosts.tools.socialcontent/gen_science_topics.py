
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
Generate 50 sentences, about random science topics from sciences  like biology, chemistry,
physics, astronomy, computers, artificial intelligence, programming languages,
computer design, magnetism, electricity, geology, meteorology, ecology, mathematics,
aeronautics, anthropology, oceanography, agronomy, genetics, astrobiology

'''
gen_prompts(llm, 'science', prompt, 'science_content.yml')



