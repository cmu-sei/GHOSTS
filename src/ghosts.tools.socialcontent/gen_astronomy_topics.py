
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
Generate 50 sentences, about topics concerning astronomical topics such as planets, moons,
comets, galaxies, quasars, universes, black holes, stars, space travel, space colonization,
constellations, nebulas.



'''
gen_prompts(llm, 'astronomy', prompt, 'astronomy_content.yml')



