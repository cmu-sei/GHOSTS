
import ollama
import yaml
import posixpath
import os
from gen_topics_common import gen_prompts

# This file generates a file named movie_content.yml that will have prompts
# for social media posts. This file can be fed to the 'gen_social_posts.py'
# script to generate the content


llm = 'gemma'


prompt = '''
Generate 50 sentences, using the following EXAMPLE where MOVIEA and MOVIEB are replaced by random movie names.

EXAMPLE:
Which is better, MOVIEA or MOVIEB?

EXAMPLE:
Which movie is more suspenseful, MOVIEA or MOVIEB?

EXAMPLE:
Which movie has a more compelling storyline, MOVIEA or MOVIEB?

EXAMPLE:
Which film offers a more profound experience, MOVIEA or MOVIEB?

'''
gen_prompts(llm, 'movies', prompt, 'movie_content.yml')



