
import ollama
import yaml
import posixpath
import os


from gen_topics_common import gen_dalle3_images

# This file generates a file named movie_content.yml that will have prompts
# for social media posts. This file can be fed to the 'gen_social_posts.py'
# script to generate the content
#content_file = 'movie_content.yml'
#content_file = 'travel_content.yml'
#content_file = 'product_content.yml'
#content_file = 'animal_content.yml'

content_files = ['product_content.yml','travel_content.yml','movie_content.yml']
#content_files = ['movie_content.yml']
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


llmList = ['wizardlm2']
datadir = "D:/circadence/ollama_dev/content_gen"

for content_file in content_files:
    gen_dalle3_images(datadir, llmList, content_file, "dalle3_prompt.txt", checkOnly=False)


