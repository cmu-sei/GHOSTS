import os
import shutil


configuration = "release"  # release || debug

# release version is determined by the project file release version parameter
r = ""

with open('../src/ghosts.client.universal/ghosts.client.universal.csproj') as f:
    for line in f:
        if line.strip().startswith("<ReleaseVersion>"):
            r = line.replace("<ReleaseVersion>", "").replace("</ReleaseVersion>", "")
            break


release_version = ".".join(r.strip().split(".")[0:3])
print(f"Preparing to build and package {release_version}")

try:
    shutil.rmtree("../src/ghosts.client.universal/bin")
except Exception as e:
    print(f"Dir delete failed with: {e.strerror}")

os.system(f"dotnet build ../src/ghosts.client.universal.sln --nologo -v minimal --configuration {configuration} -o ../src/ghosts.client.universal/bin/{configuration}")
print("  linux build completed. Preparing package...")

#g = os.system(f"../src/ghosts.client.universal/bin/{configuration}/geckodriver.exe --version").split("(")[0]
#c = os.system(f"../src/ghosts.client.universal/bin/{configuration}/chromedriver.exe --version").split("(")[0]
#print(f"    {g}")
#print(f"    {c}")

os.rename(f"../src/ghosts.client.universal/bin/{configuration}", f"../src/ghosts.client.universal/bin/ghosts-client-universal-v{release_version}")
shutil.make_archive(f"../src/ghosts.client.universal/bin/ghosts-client-universal-v{release_version}", 'zip', f"../src/ghosts.client.universal/bin/ghosts-client-universal-v{release_version}")
print("  linux package complete...")

# clean up folder structure
try:
    shutil.rmtree(f"../src/ghosts.client.universal/bin/ghosts-client-universal-v{release_version}")
except Exception as e:
    print(f"Dir delete failed with: {e.strerror}")


print("All packages completed.")
