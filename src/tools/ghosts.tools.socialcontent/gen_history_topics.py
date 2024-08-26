
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
Generate 50 sentences, about random historical topics such as dinosaurs, prehistoric
times, world war I, world war II, civil war, vietnam war, iraq war, gulf war, 
ancient greeks, ancient roman empire, industrial revolution, sufferage, the great
depression, bronze age, medieval period, the renaissance, the atomic age, great 
battles of history, great naval battles, naval history, airplane history,
great land battles, napoleonic wars.


'''
gen_prompts(llm, 'history', prompt, 'history_content.yml')



