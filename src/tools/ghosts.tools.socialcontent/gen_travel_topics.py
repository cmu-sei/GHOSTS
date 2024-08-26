
import ollama
import yaml
import posixpath
import os
from gen_topics_common import gen_prompts

# This file generates a file named movie_content.yml that will have prompts
# for social media posts. This file can be fed to the 'gen_social_posts.py'
# script to generate the content


llm = 'mistral'


prompt = '''
Generate 50 sentences, using the following EXAMPLE where CITY_A is replaced by a random city name.

EXAMPLE:
We had a great trip to CITY_A.

EXAMPLE:
What are the best restaurants in CITY_A?

EXAMPLE:
What are the best tourist spots in CITY_A?


EXAMPLE:
What are good children friendly activities in CITY_A?

'''
gen_prompts(llm, prompt, 'travel', 'travel_content.yml')



