
import ollama
import yaml
import posixpath
import os
from gen_topics_common import gen_image_prompts, dalle3PromptFilter

#content_file = 'movie_content.yml'
#content_file = 'travel_content.yml'
#content_file = 'product_content.yml'
#content_file = 'animal_content.yml'
#content_files = ['movie_content.yml','travel_content.yml','product_content.yml']

#content_files = ['animal_content.yml','movie_content.yml','travel_content.yml','product_content.yml']
template = 'dalle3_prompt_template.txt'
#content_files = ['paranormal_content.yml']
#content_files = ['travel_content.yml']
#content_files = ['animal_content.yml']
#content_files = ['movie_content.yml']
#content_files = ['product_content.yml']
#content_files = ['astronomy_content.yml']
#content_files = ['anime_content.yml']
#content_files = ['history_content.yml']
#content_files = ['science_content.yml']
content_files = ['sports_content.yml']

llm = 'gemma'
dirList = ['wizardlm2']
datadir = "D:/circadence/ollama_dev/content_gen"

for content_file in content_files:
    gen_image_prompts(datadir, llm, dirList, content_file, template, "dalle3_prompt.txt", dalle3PromptFilter)




