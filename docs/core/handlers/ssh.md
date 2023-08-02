# Secure Shell (SSH) Configuration

???+ info "Sample Configuration"
    The sample configuration below is also available in the [GHOSTS GitHub repository](<https://github.com/cmu-sei/GHOSTS/blob/master/src/Ghosts.Client/Sample%20Timelines/clicks>

The credentials JSON file expected by this handler has the following format.

```json
{
         "Version": "1.0",
         "Data": {
            "credkey1": {"username":"user1","password":"pw1base64"},
            "credkey2": {"username":"user2","password":"pw2base64"},
            ....
            "credkeyN": {"username":"userN","password":"pwNbase64"},
          }
}
```

The Version slot string is unused at the moment but is there in case this implementation is extended in the future. The credkey is simply some unique string that identifies the credential. The password is assumed to be UTF8 that is base64 encoded. See src\Ghosts.Client\Infrastructure\SshSupport.cs for a list [`reservedword`] supported in Ssh commands

```json
{
  "Status": "Run",
  "TimeLineHandlers": [
    {
      "HandlerType": "Ssh",
      "HandlerArgs": {
        "CommandTimeout": 1000, //max time to wait for new input from an SSH command execution
        "TimeBetweenCommandsMax": 5000, //max,min between individual SSH commands
        "TimeBetweenCommandsMin": 1000,
        "ValidExts": "txt;doc;png;jpeg", //used by [randomextension] reserved word, choose random extension from this list
        "CredentialsFile": "d:\\ghosts_data\\ssh_creds.json", //required, file path to a JSON file containing the SSH credentials
        "delay-jitter": 0 //optional, default =0, range 0 to 50, if specified, DelayAfter varied by delay-%jitter*delay to delay+%jitter*delay
      },
      "Initial": "",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": "True",
      "TimeLineEvents": [
        {
          "Command": "random",
          "CommandArgs": [
            "<an IP>|<unique_key_from_credentials>|ls -lah;ls -ltrh;help;pwd;date;time;uptime;uname -a;df -h;cd ~;cd [remotedirectory];touch [randomname].[randomextension];mkdir [randomname]"  //<serverIP>|<credKey|<commmandList>
          ],
          "DelayAfter": 20000,
          "DelayBefore": 0
        }
      ]
    }
  ]
}
```
