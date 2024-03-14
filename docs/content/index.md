# GHOSTS Content Servers Overview

GHOSTS content servers are an evolving part of the framework. They exist for several reasons:

1. On an air-gapped network, where we are simulating some subset of the internet, we want more types of browsable content within that range — documents, spreadsheets, presentations, pdf files, streamed movies, and the like.
2. We want a broad range of URLs within a site we are representing in the range.
3. We want to simulate a document store, such as SharePoint, OneCloud, or similar, but without the hassle of installing and maintaining those actual systems.

> Research by Global WebIndex claims that globally, 59% of the world's population uses social media, and that the average daily use is 2 hours and 29 minutes (July 2022).

### Air-gapped networks

Many ranges have no access to the wider internet. In these cases, recreating a reasonable facsimile of the internet is key to the training experience. While there are many systems that do this well, we often want to augment the scenario with a wider array of URL traffic, or we want to introduce more of certain kinds of content going across the wire. PANDORA was created to address these concerns. Shortly later, we added a social server as well.

### Having to know valid URLs

The other problem is that the internet works by the client having to “know” the location of some resource via:

- Actually knowing the URL
- Being referred from another page - I might know google.com and search for something, which gives me a reference to another page I was not aware of previously.
- Inferring the URL from some like resource - If one is poking around and looking for something on a server, a slight change of URL often gives hints that get you to where you wanted to go.
- Guessing - the proliferation of .com domains means that for something new, it's often fruitful to just try that thing.com and see if it works!

The problem here is that currently, clients must know valid URLs that actually exist out in a simulated greyspace (via [TopGen](https://github.com/cmu-sei/topgen){:target="_blank"}, [GreyBox](https://github.com/cmu-sei/greybox){:target="_blank"}, or otherwise), which limits the array of potential requests and creates range work to maintain. So we created GHOSTS PANDORA, which serves whatever clients ask for - if the request is for a doc file, the server creates a random doc file on the fly — in memory — and serves it back to the client. Pandora serves the following content types: 

- html
- css
- js
- doc|x
- ppt|x
- xls|x
- mp4
- pdf
- gif
- jpg
- png
- zip
- msi
- iso
- other binary formats, etc. 
 
Pandora has generic request handlers for each HTTP verb (GET, POST, etc.) and is deployed as a simple docker container. We configure it to handle a particular IP on a multiple-IP-enabled host machine. It works off any URL, but part of the solution involved introducing more randomness in the GHOSTS clients as well. Those clients now support a creative parameter-built URL system that can be configured to look something like this:

```text
sharepoint.hello.com/{org}/{report_type}/{uuid}/{file_name}.{file_type}
```

These variables are processed at runtime, and produce a final url that might look something like:

```text
sharepoint.hello.com/operations/maintenance/80e6af4e-5107-43b5-832f-0d8027efbd76/report.docx
```

In addition, Pandora supports “bad” payloads via configuration. Here the server responds to specific configured URLs to deploy planted injects. So clients can download malware in an exercise in a manner that is hard to differentiate based on URLs already seen within the event. The configuration looks like:

```text
[payloads]
1=/bad/payload/url/,some_bad.zip,application/zip
```

Where id=url,payload file, MIME response type.
