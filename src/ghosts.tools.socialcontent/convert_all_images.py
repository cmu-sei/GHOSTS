import os
import posixpath
import shutil
import subprocess
import time


def runProgram(args):
    """
    Run a program in a process, wait for it to finish
    """
    proc = subprocess.Popen(args, cwd=os.getcwd())
    while proc.poll() is None:
        time.sleep(1)


targetDir = "./social_content"

for topicdir in os.listdir(targetDir):
    topicFullPath = posixpath.join(targetDir, topicdir)
    if (posixpath.isdir(topicFullPath)):
        for topicSubDir in os.listdir(topicFullPath):
            topicSubFullPath = posixpath.join(targetDir, topicdir, topicSubDir)
            if (posixpath.isdir(topicSubFullPath)):
                for fname in os.listdir(topicSubFullPath):
                    if 'png' in fname:
                        words = fname.split('.')
                        jpgName = "%s.jpg" % (words[0])

                        fullPath = posixpath.join(targetDir, topicdir, topicSubDir, fname)
                        destpath = posixpath.join(targetDir, topicdir, topicSubDir, jpgName)
                        args = ["convert", fullPath, destpath]
                        print("Running %s" % (args))
                        runProgram(args)
                        # print("copied file %s  " % (fullPath))



