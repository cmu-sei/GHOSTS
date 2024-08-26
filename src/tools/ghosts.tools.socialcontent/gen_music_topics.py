
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
Generate 50 sentences, GROUP_A and GROUP_B are replaced by a random musical group name and SONG_A is replaced
by a random song played by that group.

EXAMPLE:
Who do you think was the most influential Rock and Roll GROUP_A of time?

EXAMPLE:
How does anyone afford tickets to watch GROUP_A play, the tickets are so expensive?

EXAMPLE:
What is your favorite SONG_A from GROUP_A?


EXAMPLE:
I believe that GROUP_A is better that GROUP_B and here are the reasons why.


EXAMPLE:
Is GROUP_A still together or have they broken up?

'''
gen_prompts(llm, 'music', prompt, 'music_content.yml')



