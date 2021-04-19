from time import strftime, gmtime
import argparse


class Entry:
    def __init__(self, handler: str, command: str, command_args: str, tags: str):
        self.handler = handler,
        self.command = command,
        self.command_args = command_args
        self.tags = tags

    def toPayload(self):
        dt_gmt = strftime("%m/%d/%Y %H:%M:%S", gmtime())
        return 'TIMELINE|' + str(dt_gmt) + '|{"Handler":"' + str(self.handler[0]) + '","Command":"' + \
            str(self.command[0]) + '","CommandArg":"' + str(self.command_args) + '", "Tags":"' + str(self.tags) + '"}'


parser = argparse.ArgumentParser(description='Log file entry information')
parser.add_argument('--log_file', dest='log_file', type=str, help='Name of the log file to append')
parser.add_argument('--handler', dest='handler', type=str, help='Name of the handler')
parser.add_argument('--command', dest='command', type=str, help='Name of the command')
parser.add_argument('--command_args', dest='command_args', type=str, help='The command args')
parser.add_argument('--tags', dest='tags', type=str, help='The tags')

args = parser.parse_args()

entry = Entry(args.handler, args.command, args.command_args, args.tags)

#payload = 'TIMELINE|2/23/2021 6:14:08 PM|{"Handler":"Command","Command":"exit","CommandArg":""}'
# print(entry.toPayload())

with open(args.log_file, "a") as f:
    f.write(entry.toPayload())

# python add-to-timeline-file.py --log_file /Users/dustin/Projects/ghosts/src/Ghosts.Client/bin/Debug/logs/clientupdates.log --handler BrowserFirefox --command browse --command_args https://yaf.com --tags ssl
