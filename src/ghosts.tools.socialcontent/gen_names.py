
import ollama
import yaml
import posixpath
import os
from gen_topics_common import gen_names

# This file generates a file named movie_content.yml that will have prompts
# for social media posts. This file can be fed to the 'gen_social_posts.py'
# script to generate the content


#llm = 'llama3'
llm = ['mistral','gemma','llama3','wizardlm2','llava']

prompt = '''
Give 100 male and female names, first and last, and also social media names mixed in the same list
'''
gen_names(llm, prompt, 'social_content\\names.txt')



