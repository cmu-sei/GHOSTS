# GHOSTS Content Servers Overview

GHOSTS content servers are an evolving part of the framework. They exist for several reasons:

1. On an air-gapped network, where we are simulating some subset of the internet, we want more types of browsable content within that range — documents, spreadsheets, presentations, pdf files, streamed movies, and the like.
2. We want a broad range of URLs within a site we are representing in the range.
3. We want to simulate a document store, such as SharePoint, OneCloud, or similar, but without the hassle of installing and maintaining those actual systems.

> Research by Global WebIndex claims that globally, 59% of the world's population uses social media, and that the average daily use is 2 hours and 29 minutes (July 2022).



Many ranges are air-gapped, which means they have no access to the wider internet. In these cases, recreating a reasonable facsimile of the internet is key to the training experience. While there are many systems that do this well, we often want to augment the scenario with a wider array of URL traffic, or we want to introduce more of certain kinds of content going across the wire. PANDORA was created to address these concerns. Soon afterwards, we added a social server as well.

???+ info "GHOSTS PANDORA Source Code"
    The [GHOSTS PANDORA Source Code Repository](https://github.com/cmu-sei/GHOSTS/tree/master/src/ghosts.pandora) is hosted on GitHub
