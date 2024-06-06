

import os
import posixpath
import shutil



targetDir = "./social_content"

destDir = "./all_images"

count = 0

for topicdir in os.listdir(targetDir):
    topicFullPath = posixpath.join(targetDir,topicdir)
    if (posixpath.isdir(topicFullPath)):
        for topicSubDir in os.listdir(topicFullPath):
            topicSubFullPath = posixpath.join(targetDir,topicdir,topicSubDir)
            if (posixpath.isdir(topicSubFullPath)):
                for fname in os.listdir(topicSubFullPath):
                    if 'png' in fname:
                        fullPath = posixpath.join(targetDir,topicdir,topicSubDir,fname )
                        destpath = posixpath.join(destDir, "image%d.png" % count)
                        count += 1
                        if (posixpath.isfile(destpath)):
                            os.unlink(destpath)
                        shutil.copy(fullPath,destpath)
                        print("copied file %s  " % (fullPath))



