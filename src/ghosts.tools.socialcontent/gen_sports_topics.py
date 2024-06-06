
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
Generate 50 sentences, SPORT_A is replaced by a random sports name like football, baseball, basketball, 
swimming, hockey, indy car racing, nascar, lacrosse, badminton, tennis, track and field and TEAM_A is replaced
by a professional team or player from that sport.

EXAMPLE:
I really don't under the rules about SPORT_A, could someone explain?

EXAMPLE:
I love watching SPORT_A, and TEAM_A is my favorite?

EXAMPLE:
Could someone recommend a youth coach for SPORT_A?


EXAMPLE:
I think that TEAM_A from SPORT_A should fire their coach, he is terrible!


EXAMPLE:
Which TEAM_A from SPORT_A do you think was the best team in history?

'''
gen_prompts(llm, 'sports', prompt, 'sports_content.yml')



