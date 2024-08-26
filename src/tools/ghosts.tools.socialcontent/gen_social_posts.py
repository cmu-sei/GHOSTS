
import ollama
import yaml
import posixpath
import os

#llm_list = ['gemma','llama3', 'wizardlm2']
llm_list = ['wizardlm2']
datadir = "D:/circadence/ollama_dev/content_gen"
#content_file = 'movie_content.yml'
#content_file = 'travel_content.yml'
#content_file = 'product_content.yml'
#content_file = 'animal_content.yml'
#content_file = 'paranormal_content.yml'
#content_file = 'astronomy_content.yml'
#content_file = 'sports_content.yml'
#content_file = 'health_content.yml'
#content_file = 'science_content.yml'
#content_file = 'history_content.yml'
content_file = 'anime_content.yml'


with open(posixpath.join(datadir,content_file), 'r') as f:
    data = yaml.load(f, Loader=yaml.SafeLoader)

contentDict = data['contentgen']
rootdir = posixpath.join(datadir, data['datadir'])

if posixpath.isdir(rootdir):
    print("Rootdir exists!")
else:
    print("making dir: %s" % (rootdir))
    os.mkdir(rootdir)

#get posts
for topicKey in sorted(contentDict.keys()):
    topicDict = contentDict[topicKey]
    topicDir = posixpath.join(rootdir,topicKey)
    if (not posixpath.isdir(topicDir)):
        os.mkdir(topicDir)
    for postKey in sorted(topicDict.keys()):
        for llm in llm_list:
            postDir = posixpath.join(topicDir, "%s_%s" % (llm,postKey))
            if (not posixpath.isdir(postDir)):
                os.mkdir(postDir)
            postDict = topicDict[postKey]
            prompt = postDict['prompt']
            responseDict = ollama.generate(llm, prompt)
            print("##PROMPT")
            print(prompt)
            print("##RESPONSE")
            print(responseDict['response'])
            postFile = posixpath.join(postDir, "post.txt")
            ofile = open(postFile,'w', encoding='utf-8')
            ofile.write(responseDict['response'])
            ofile.close()

