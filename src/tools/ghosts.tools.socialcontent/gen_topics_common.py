
import ollama
import yaml
import posixpath
import os
import re
import requests
import base64
from openai import OpenAI
import time

# This file generates a file named movie_content.yml that will have prompts
# for social media posts. This file can be fed to the 'gen_social_posts.py'
# script to generate the content

def gen_names(llmList, prompt,outputFilename):
    ofile = open(outputFilename, 'w')
    for llm in llmList:
        responseDict = ollama.generate(llm, prompt)
        response = responseDict['response']
        #parse the reponses
        lines = response.split('\n')
        state = 'init'
        for line in lines:
            if line == '':
                continue
            if state == 'init':
                if 'Here is a list of':
                    state = 'parse'
                    continue
                if 'Male Names' in line:
                    state = 'parse'
                    continue
            if state == 'parse':
                if 'Last Names' in line:
                    continue
                if 'Mixed' in line:
                    continue
                if 'Names' in line:
                    continue
                if 'Female Names' in line:
                    continue
                if 'randomly generated' in line:
                    continue
                words = line.split(' ')
                if len(words) == 0:
                    continue
                if len(words) > 12:
                    continue
                if 'Doe' in words:
                    continue
                newline = " ".join(words[1:])
                print("%s >>> %s" % (llm,newline))
                ofile.write("%s\n" % newline)
    ofile.close()


def gen_prompts(llm, topic, prompt,yamlOutputFilename, response_file=None):
    """

    :param llm: the LLM used for processing the prompt
    :param topic: the topic related to the prompt, ie. 'movies'
    :param prompt: a prompt that generates one topic per line
    :param filename: output yaml file
    :param response_file: If this is present, do not use LLM but use this file as ouput from the LLM
    :return:
    """
    topdict = {}
    topdict['datadir'] = 'social_content'
    topdict['contentgen'] = {}
    topdict['contentgen'][topic] = {}
    contentDict = topdict['contentgen'][topic]
    lines = []
    if response_file:
        ifile = open(response_file,'r')
        lines = ifile.readlines()
        ifile.close()
    elif llm:
        responseDict = ollama.generate(llm, prompt)
        response = responseDict['response']
        lines = response.split('\n')

    if len(lines) == 0:
        print("No output generated for LLM: %s, topic: %s" % (llm, topic))
        return

    for line in lines:
        if line == '':
            continue
        words = line.split(' ')
        title = words[0].replace('.','')
        moviePrompt = ' '.join(words[1:])
        entirePrompt = 'Write a facebook post about ' + moviePrompt
        movieDict = {}
        movieDict['prompt'] = entirePrompt
        contentDict[title] = movieDict

    #write out the yaml
    ofile = open(yamlOutputFilename, 'w')
    yaml.dump(topdict,ofile)
    ofile.close()

def dalle3PromptFilter(outputFilename):
    newLines = []
    (head,tail) = posixpath.split(outputFilename)
    saveFile = posixpath.join(head,"org_%s" % tail)
    wordCount = 0
    with open(outputFilename, 'r',encoding='Latin-1') as f:
        lines = f.readlines()
        with open(saveFile, 'w',encoding='Latin-1') as outf:
            outf.writelines(lines)


        for line in lines:
            line = line.replace("**Keyword Prompt Area:**","")
            line = line.replace("**Keyword Prompt:**","")
            if re.match("^\*\*keyword prompt.*", line.lower()):
                continue
            if re.match("## stable diffusion.*", line.lower()):
                continue
            if re.match("##.*prompt.*", line.lower()):
                continue
            if re.search("inspired.*", line.lower()):
                temp = line.split("Inspired", 1)
                if len(temp) == 1:
                    temp = line.split("inspired", 1)
                words = temp[0].split()
                if len(words) > 5:
                    line = temp[0]
            if re.search(", by .*", line.lower()):
                temp = line.split(", By", 1)
                if len(temp) == 1:
                    temp = line.split(", by", 1)
                words = temp[0].split()
                if len(words) > 5:
                    line = temp[0]
            if re.search(", style of .*", line.lower()):
                temp = line.split(", By", 1)
                if len(temp) == 1:
                    temp = line.split(", by", 1)
                words = temp[0].split()
                if len(words) > 5:
                    line = temp[0]
            if re.match("^\*\*negative keyword.*", line.lower()):
                break
            newLines.append(line)
            wordCount += len(line.split())

    if (wordCount == 0):
        print("ERROR: new output file will be empty: %s" % outputFilename)
    else:
        with open(outputFilename, 'w') as f:
            f.writelines(newLines)
            f.write("\n")
    return



def gen_image_prompts(datadir, llm, contentDirs, contentFile,promptTemplateFile, outputFilename, filterResultFunc):
    """

    :param datadir:
    :param llm: llm to use to generate the prompt
    :param contentDirs: list of contentDir prefixes
    :param contentFile: ie 'animal_content.yml'
    :param promptTemplateFile template used to generate the AI prompt
    :param outputFilename file name for output
    :return:
    """


    with open(posixpath.join(datadir, contentFile), 'r') as f:
        data = yaml.load(f, Loader=yaml.SafeLoader)

    # read the AI prompt file
    with open(posixpath.join(datadir, promptTemplateFile), 'r') as f:
        imageTemplate = f.read()

    contentDict = data['contentgen']
    rootdir = posixpath.join(datadir, data['datadir'])

    if posixpath.isdir(rootdir):
        print("Rootdir exists!")
    else:
        print("making dir: %s" % (rootdir))
        os.mkdir(rootdir)

    basePrompt = {'role': 'user', 'content': imageTemplate}

    for topicKey in sorted(contentDict.keys()):
        topicDict = contentDict[topicKey]
        topicDir = posixpath.join(rootdir, topicKey)
        if (not posixpath.isdir(topicDir)):
            os.mkdir(topicDir)
        for postKey in sorted(topicDict.keys()):
            for contentDir in contentDirs:
                postDir = posixpath.join(topicDir, "%s_%s" % (contentDir, postKey))
                if (not posixpath.isdir(postDir)):
                    os.mkdir(postDir)
                postDict = topicDict[postKey]
                prompt = postDict['prompt']
                prompt = prompt.replace('Write a facebook post about', '')
                streamData = [basePrompt]
                streamData.append({'role': 'user', 'content': prompt})
                # now generate the Image prompts
                stream = ollama.chat(
                    model=llm,
                    messages=streamData,
                    stream=True,
                )

                outputPromptFile = posixpath.join(postDir, outputFilename)
                print("Generating file: %s" % (outputPromptFile))
                ofile = open(outputPromptFile, 'w', encoding='utf-8')
                for chunk in stream:
                   #output += chunk['message']['content']
                   print(chunk['message']['content'], file=ofile, end='', flush=True)
                ofile.close()
                if filterResultFunc:
                    filterResultFunc(outputPromptFile)


def gen_stable_diffusion_images(datadir, llmList, contentFile, checkOnly=False):

    with open(posixpath.join(datadir, contentFile), 'r') as f:
        data = yaml.load(f, Loader=yaml.SafeLoader)

    contentDict = data['contentgen']
    rootdir = posixpath.join(datadir, data['datadir'])

    if posixpath.isdir(rootdir):
        print("Rootdir exists!")
    else:
        print("making dir: %s" % (rootdir))
        os.mkdir(rootdir)

    for topicKey in sorted(contentDict.keys()):
        topicDict = contentDict[topicKey]
        topicDir = posixpath.join(rootdir, topicKey)
        if (not posixpath.isdir(topicDir)):
            os.mkdir(topicDir)
        for postKey in sorted(topicDict.keys()):
            for llm in llmList:
                postDir = posixpath.join(topicDir, "%s_%s" % (llm, postKey))
                if (not posixpath.isdir(postDir)):
                    os.mkdir(postDir)
                promptFile = posixpath.join(postDir, "stable_ai_prompt.txt")
                if not posixpath.isfile(promptFile):
                    print("WARNING: Stable AI prompt file does not exist, expected: %s" % (promptFile))
                with open(promptFile, 'r') as f:
                    lines = f.readlines()
                positivePrompt = ""
                negativePrompt = ""
                state = "positiveSearch"
                positiveAdd = ""
                #filter out empty lines first
                newlines = []
                for line in lines:
                    if line == "":
                        continue
                    if line == "\n":
                        continue
                    newlines.append(line)


                for line in newlines:
                    if re.match('Color:', line):
                        positiveAdd += ',' + line.replace('Color:','')
                        continue
                    if re.match('Style:', line):
                        positiveAdd += ',' + line.replace('Style:', '')
                        continue
                    if re.match('Artist:', line):
                        positiveAdd += ',' + line.replace('Artist:', '')
                        continue
                    if re.match('Additional Details:',line):
                        positiveAdd += ',' + line.replace('Additional Details:', '')
                        continue
                    if state == "positiveSearch" and 'llama' in promptFile and len(newlines) == 3 and 'prompt' in line:
                        state = 'positiveFound'
                        continue

                    if state == "positiveSearch":
                        positiveAdd = ""
                        if ('gemma_38' in promptFile):
                            promptFile = promptFile
                        if 'gemma' in promptFile:

                            if re.match('\*\*Keywords:\*\*', line) and len(line) > (len('**Keywords:**')+5):
                                positivePrompt = line.replace('**Keywords:**', '')
                                state = 'negativeFound'
                                continue
                            if re.match('\*\*Keyword Prompt Area:\*\*', line) and len(line) > (len('**Keyword Prompt Area:**')+5):
                                positivePrompt = line.replace('**Keyword Prompt Area:**', '')
                                state = 'negativeFound'
                                continue
                            if '**Keyword Prompt Area:**' in line:
                                state = 'positiveFound'
                            if '**Keyword Prompt:**' in line:
                                state = 'positiveFound'
                            if '**Prompt:**' in line:
                                state = 'positiveFound'
                            if '**Prompt:**' in line:
                                state = 'positiveFound'
                            if '## Prompt:' in line:
                                state = 'positiveFound'
                            if re.match('\*\*Subject:\*\*', line):
                                positivePrompt = line.replace('**Subject:**','')
                                state = 'negativeFound'
                                continue

                        elif 'llama' in promptFile:
                            if re.match('Keyword Prompt:',line) and (len(line) > len('Keyword Prompt:')+5):
                                positivePrompt = line.replace('Keyword Prompt:', '')
                                state = 'negativeFound'
                                continue


                            if 'fully working Stable Diffusion prompt' in line or\
                                'a stable diffusion prompt:' in line or\
                                'enhanced Stable Diffusion prompt' in line or\
                                'a Stable Diffusion prompt for you:' in line or \
                                'a Stable Diffusion prompt' in line or \
                                'your Stable Diffusion prompt:' in line or\
                                'Keyword Prompt:' in line or\
                                'an enhanced prompt with all' in line or\
                                'the enhanced prompt:' in line or \
                                'Keyword Prompt Area:' in line or \
                                'an enhanced prompt based on your subject' in line or \
                                'an enhanced prompt that takes into account' in line or \
                                'a possible prompt:' in line or \
                                'Here is a potential prompt:' in line or \
                                'a potential Stable Diffusion prompt:' in line or \
                                'an enhanced keyword prompt for a Stable Diffusion image:' in line or \
                                'an attempt at creating a prompt:' in line or \
                                'an advanced Stable Diffusion prompt' in line or \
                                'fully-formed Stable Diffusion prompt' in line or \
                                'an example prompt that could lead to some great results:' in line or \
                                'your Stable Diffusion prompt:' in line or \
                                'an enhanced prompt to generate an image' in line or \
                                'a potential prompt:' in line or \
                                'keyword prompt paragraph and Negative prompt paragraph' in line or \
                                'Here\'s a prompt' in line or \
                                'a revised prompt that incorporates your input:' in line or \
                                'Here\'s an enhanced prompt' in line or \
                                'is the enhanced keyword prompt:' in line or \
                                '**Keyword Prompt Area**' in line:
                                state = 'positiveFound'



                        continue

                    if state == 'positiveFound':
                        if 'gemma' in promptFile:
                            if re.match('\*\*Keyword Prompt Area:\*\*', line) and len(line) > (len('**Keyword Prompt Area:**')+5):
                                positivePrompt = line.replace('**Keyword Prompt Area:**', '')
                                state = 'negativeFound'
                                continue

                            if '**Keyword Prompt Area:**' in line:
                                continue
                            if '**Keyword Prompt:**' in line:
                                continue

                            if line == "":
                                continue
                            positivePrompt += line
                            state = 'negativeFound'
                            continue

                        if 'llama' in promptFile:
                            if '**Keyword Prompt Area:**' in line:
                                continue
                            if 'The keyword prompt area:' in line:
                                continue
                            if line == "":
                                continue
                            if 'Keyword Prompt:' in line:
                                line = line.replace('Keyword Prompt:','')
                            if '**Negative Keyword Prompt Area:**' in line:
                                state = 'negativeFound'
                                continue
                            if re.match('NEGATIVE:', line):
                                negativePrompt = line.replace('NEGATIVE:','')
                                state = "null"
                                continue
                            positivePrompt += line
                            state = 'negativeFound'
                            continue
                    if state == 'negativeFound':
                        if re.match('\*\*Negative Keywords:\*\*', line) and len(line) > (len('**Negative Keywords:**')+5):
                            negativePrompt = line.replace('**Negative Keywords:**', '')
                            state = 'null'
                            continue
                        if re.match('\*\*Negative Keyword Prompt Area:\*\*', line) and len(line) > (len('**Negative Keyword Prompt Area:**')+5):
                            negativePrompt = line.replace('**Negative Keyword Prompt Area:**', '')
                            state = 'null'
                            continue
                        if '**Negative Keyword Prompt Area:**' in line:
                            continue
                        if re.match('\*\*NEGATIVE:\*\*', line) and (len(line) > len('**NEGATIVE:**')+5):
                            negativePrompt = line.replace('**NEGATIVE:**', '')
                            state = "null"
                            continue
                        if re.match('\*\*Negative:\*\*', line) and (len(line) > len('**Negative:**')+5):
                            negativePrompt = line.replace('**Negative:**', '')
                            state = 'null'
                            continue
                        if re.match('NEGATIVE:', line) and (len(line) > len('NEGATIVE:')+5):
                            negativePrompt = line.replace('NEGATIVE:', '')
                            state = 'null'
                            continue

                        if re.match('\*\*Negative Prompt Area:\*\*', line):
                            negativePrompt = line.replace('**Negative Prompt Area:**', '')
                            state = 'null'
                            continue
                        if 'Negative Prompt Area:' in line:
                            continue
                        if '**Negative:**' in line:
                            continue
                        if 'NEGATIVE:' in line:
                            continue
                        if 'the Negative Prompt area to steer the generation' in line:
                            continue
                        #print("State:negativeFound, file: %s " % (promptFile))
                        if line == "":
                            continue
                        negativePrompt = line
                        state = "null"
                        continue

                if positivePrompt == "":
                    print("WARNING: Could not find positive prompt in Stable AI prompt file: %s" % (promptFile))
                    continue

                if negativePrompt == "":
                    print("WARNING: Could not find negative prompt in Stable AI prompt file: %s" % (promptFile))
                    continue

                if positiveAdd != "":
                    positivePrompt += positiveAdd

                print("Found good prompt, generating image for: %s"  % (promptFile))
                if checkOnly:
                    continue

                payload = {
                    "prompt": positivePrompt,
                    "negative_prompt": negativePrompt,
                    "steps": 20,
                }

                response = requests.post(url='http://127.0.0.1:7860/sdapi/v1/txt2img', json=payload)

                r = response.json()
                index = 0
                while (index < 10):
                    imageFileName = posixpath.join(postDir, "image%d.png" % (index))
                    if (not posixpath.isfile(imageFileName)):
                        break

                with open(imageFileName, 'wb') as f:
                    f.write(base64.b64decode(r['images'][0]))



def gen_dalle3_images(datadir, llmList, contentFile, promptFileName, checkOnly=False):

    with open(posixpath.join(datadir, contentFile), 'r') as f:
        data = yaml.load(f, Loader=yaml.SafeLoader)

    contentDict = data['contentgen']
    rootdir = posixpath.join(datadir, data['datadir'])

    if posixpath.isdir(rootdir):
        print("Rootdir exists!")
    else:
        print("making dir: %s" % (rootdir))
        os.mkdir(rootdir)

    for topicKey in sorted(contentDict.keys()):
        topicDict = contentDict[topicKey]
        topicDir = posixpath.join(rootdir, topicKey)
        if (not posixpath.isdir(topicDir)):
            os.mkdir(topicDir)
        for postKey in sorted(topicDict.keys()):
            for llm in llmList:
                postDir = posixpath.join(topicDir, "%s_%s" % (llm, postKey))
                if (not posixpath.isdir(postDir)):
                    os.mkdir(postDir)
                imageFileName = posixpath.join(postDir, "image_dalle3.png")
                if (posixpath.isfile(imageFileName)):
                    print("Image file: %s already exists, skipping" % (imageFileName))
                    continue
                promptFile = posixpath.join(postDir, promptFileName)
                if not posixpath.isfile(promptFile):
                    print("WARNING: Dalle3 prompt file does not exist, expected: %s" % (promptFile))
                imagePrompt = ""
                with open(promptFile, 'r') as f:
                    lines = f.readlines()
                    imagePrompt = "".join(lines)

                print("Found good prompt, generating image for: %s"  % (promptFile))
                if checkOnly:
                    continue



                client = OpenAI(api_key="THIS_NEEDS_TO_BE_A_VALID_API_KEY")
                print("Generating image file: %s" % imageFileName)

                response = client.images.generate(
                    model="dall-e-3",
                    prompt=imagePrompt,
                    size="1024x1024",
                    quality="standard",
                    response_format="b64_json",
                    n=1,
                )

                image_data = response.data[0].b64_json


                with open(imageFileName, 'wb') as f:
                    f.write(base64.b64decode(image_data))

                print("Wrote image file: %s" % imageFileName)
                print("Sleeping for 15 seconds")
                time.sleep(15)


















