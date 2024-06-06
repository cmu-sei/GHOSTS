
import ollama
import yaml
import posixpath
import os
from gen_topics_common import gen_image_prompts

# This file generates a file named movie_content.yml that will have prompts
# for social media posts. This file can be fed to the 'gen_social_posts.py'
# script to generate the content
#content_file = 'movie_content.yml'
#content_file = 'travel_content.yml'
#content_file = 'product_content.yml'
#content_file = 'animal_content.yml'
#content_files = ['movie_content.yml','travel_content.yml','product_content.yml']

content_files = ['animal_content.yml','movie_content.yml','travel_content.yml','product_content.yml']
stableAiTemplate = 'image_prompt_template.txt'

#gemma appears to be the best, use Gemma but put results in wizardlm2 directory
#llmList = ['gemma','llama3']
llm = 'gemma'
dirList = ['wizardlm2']
datadir = "D:/circadence/ollama_dev/content_gen"

for content_file in content_files:
    gen_image_prompts(datadir, llm, dirList, content_file, stableAiTemplate, "stable_ai_prompt.txt",None)




