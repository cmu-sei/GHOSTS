import json
import argparse

parser = argparse.ArgumentParser()

parser.add_argument("-t", "--timeline", help="Input timeline file", required=True)
parser.add_argument("-o", "--output", help="Output", required=True)

args = parser.parse_args()

print("Reading source timeline file...")

j = open(args.timeline)
data = json.load(j)

with open(args.output, "w") as f:
    f.write("\"TimeLineEvents\": [\n")
    for handler in data["TimeLineHandlers"]:
        if handler["HandlerType"] == "BrowserFirefox":
            print("Found Firefox handler...")
            for events in handler["TimeLineEvents"]:
                for site in events["CommandArgs"]:
                    f.write("\t{\n")
                    f.write("\t\t\"Command\": \"browse\",\n")
                    f.write("\t\t\"CommandArgs\": [\"{}\"],\n".format(site))
                    f.write("\t\t\"DelayAfter\": 0,\n")
                    f.write("\t\t\"DelayBefore\": 0\n")
                    print(site)
                    f.write("\t},\n")
    f.write("]")
