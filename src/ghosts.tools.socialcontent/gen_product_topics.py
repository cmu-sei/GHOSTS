
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
Generate 50 sentences, using the following EXAMPLE where PRODUCT_A is replaced by a random product name such as car, boat, home, lawn mower.

EXAMPLE:
I have PRODUCT_A for sale, make offer.

EXAMPLE:
PRODUCT_A is too expensive, is there a cheaper alternative?

EXAMPLE:
Where can I buy PRODUCT_A?

EXAMPLE:
Can someone give me feedback their use of PRODUCT_A in the home?

'''

# use the following powershell command to generate this:
#  cat .\product_template.txt | powershell -Command "ollama run wizardlm2" > product_template_response.txt
# you have to change the encoding of the response file to utf-8 after you do this

# for some reason, the python version generates different things

gen_prompts(None, 'products', prompt, 'product_content.yml', response_file="product_template_response.txt")

