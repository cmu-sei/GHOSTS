import os

rtmp_url = "rtmp://localhost:1935/stream/hello"

command = ["ffmpeg ",
           "-y -f lavfi -i testsrc=size=1280x720:rate=24 -acodec libmp3lame -ar 44100 -b:a 128k ",
           "-pix_fmt yuv420p -profile:v baseline -s 1280x720 -bufsize 60k ",
           "-vb 1200k -maxrate 1500k -vcodec libx264 ",
           "-preset veryfast -g 24 -r 24 -f flv ",
           "-flvflags no_duration_filesize ",
           rtmp_url
           ]

while True:
    os.system("".join(command))