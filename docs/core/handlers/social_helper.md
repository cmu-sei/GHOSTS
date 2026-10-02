# Social Helper Configuration

The `social` command for a browser handler allows interaction with Pandora V9 social web sites.

The supported themes by this helper are `default`, `facebook`, `linkedin`, `discord`, `instagram`,  `reddit` and `x`.  The `default` theme is the old Socializer theme. Text posts are supported for all themes, and image posting is supported for the `default` theme. The `youtube` theme is not supported yet.

The handlerArgs for the `social` command are:

- "social-version" - required, hardcode as "v1.0".
- "social-username" - optional, used as username for post if social site supports user names.
- "social-use-unique-user" - optional, boolean, if "true", then grab the username from the site.
- "social-post-probability": optional, <0-100 integer>, default  0.
- "social-like-probability": optional, <0-100 integer>, default 0.
- "social-browse-probability": optional, <0-100 integer>, default 0, sum of post+like+browse <= 100.
- "social-addimage-probability" : optional, <0-100 integer>, default 0, only used if a theme supports image posting when a post is done.
- "social-content-directory" : required - contains the absolute directory path to the social content used for posting. See the section below on the required format.
- "social-topiclist" - optional, a string used to prune topic directories found a social content theme directory. If this is string it not null, then a content topic directory must contain this string in order for it be used as a topic source.

The "CommandArgs" should be a list of URLs mapped to Pandora v9 Themes (each URLs will point to a Pandora V9 docker container that is configured for that theme).  The helper randomly selects a site, and then selects a browse/post/like option based on the probabilities.  The entries in the "CommandArgs" is formatted as `site:<URL>`, the keyword `site:` is required.

A sample timeline is shown below.

```json
{
  "Status": "Run",
  "TimeLineHandlers": [
    {
      "HandlerType": "BrowserFirefox",
      "HandlerArgs": {
        "isheadless": "false",
        "browser-id": "ghosts-client-social",
        "delay-jitter": 50,
        "social-post-probability": 40,
        "social-like-probability": 20,
        "social-browse-probability": 40,
        "social-addimage-probability": 100,
        "social-content-directory": "/opt/ghosts_data/social_content_v9",
        "social-use-unique-user": "True",
        "social-version": "v1.0"
      },
      "Initial": "about:blank",
      "UtcTimeOn": "00:00:00",
      "UtcTimeOff": "24:00:00",
      "Loop": true,
      "TimeLineEvents": [
        {
          "Command": "social",
          "CommandArgs": [
              "site:http://www.tweeter.com",
              "site:http://www.facebonk.com",
              "site:http://www.lunkedin.com",
              "site:http://www.discard.com",
              "site:http://www.instaham.com",
              "site:http://www.reddwut.com",
              "site:http://www.xcess.com",
          ],
          "DelayAfter": 60000,
          "DelayBefore": 0
        }
      ]
    }
  ]
}
```
A docker compose file that creates containers the supported themes is shown below:

```
services:

  ghosts-db:
    image: postgres:latest
    container_name: ghosts-db
    environment:
      POSTGRES_USER: ghosts
      POSTGRES_PASSWORD: 'Scotty@@1!'
      POSTGRES_DB: pandora
      PGDATA: /var/lib/postgresql/data
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: always

  pandora-default:
    image: ghosts-pandora-v9:latest
    container_name: pandora-default
    ports:
      - "5000:5000"
    environment:
      - MODE_TYPE=social
      - DEFAULT_THEME=default
      - DATABASE_PROVIDER=PostgreSQL
      - CONNECTION_STRING=Host=ghosts-db;Port=5432;Database=pandora;Username=ghosts;Password=Scotty@@1!
    restart: always

  pandora-facebook:
    image: ghosts-pandora-v9:latest
    container_name: pandora-facebook
    ports:
      - "5001:5000"
    environment:
      - MODE_TYPE=social
      - DEFAULT_THEME=facebook
      - DATABASE_PROVIDER=PostgreSQL
      - CONNECTION_STRING=Host=ghosts-db;Port=5432;Database=pandora;Username=ghosts;Password=Scotty@@1!
    restart: always

  pandora-linkedin:
    image: ghosts-pandora-v9:latest
    container_name: pandora-linkedin
    ports:
      - "5002:5000"
    environment:
      - MODE_TYPE=social
      - DEFAULT_THEME=linkedin
      - DATABASE_PROVIDER=PostgreSQL
      - CONNECTION_STRING=Host=ghosts-db;Port=5432;Database=pandora;Username=ghosts;Password=Scotty@@1!
    restart: always

  pandora-discord:
    image: ghosts-pandora-v9:latest
    container_name: pandora-discord
    ports:
      - "5003:5000"
    environment:
      - MODE_TYPE=social
      - DEFAULT_THEME=discord
      - DATABASE_PROVIDER=PostgreSQL
      - CONNECTION_STRING=Host=ghosts-db;Port=5432;Database=pandora;Username=ghosts;Password=Scotty@@1!
    restart: always

  pandora-instagram:
    image: ghosts-pandora-v9:latest
    container_name: pandora-instagram
    ports:
      - "5004:5000"
    environment:
      - MODE_TYPE=social
      - DEFAULT_THEME=instagram
      - DATABASE_PROVIDER=PostgreSQL
      - CONNECTION_STRING=Host=ghosts-db;Port=5432;Database=pandora;Username=ghosts;Password=Scotty@@1!
    restart: always

  pandora-reddit:
    image: ghosts-pandora-v9:latest
    container_name: pandora-reddit
    ports:
      - "5005:5000"
    environment:
      - MODE_TYPE=social
      - DEFAULT_THEME=reddit
      - DATABASE_PROVIDER=PostgreSQL
      - CONNECTION_STRING=Host=ghosts-db;Port=5432;Database=pandora;Username=ghosts;Password=Scotty@@1!
    restart: always

  pandora-x:
    image: ghosts-pandora-v9:latest
    container_name: pandora-x
    ports:
      - "5006:5000"
    environment:
      - MODE_TYPE=social
      - DEFAULT_THEME=x
      - DATABASE_PROVIDER=PostgreSQL
      - CONNECTION_STRING=Host=ghosts-db;Port=5432;Database=pandora;Username=ghosts;Password=Scotty@@1!
    restart: always


  pandora-web:
    image: ghosts-pandora-v9:latest
    container_name: pandora-web
    ports:
      - "8888:5000"
      - "8081:5000"
      - "1935:5000"
      - "8443:5000"
    environment:
      - MODE_TYPE=website
      - DATABASE_PROVIDER=PostgreSQL
      - CONNECTION_STRING=Host=ghosts-db;Port=5432;Database=pandora;Username=ghosts;Password=Scotty@@1!
    restart: always


volumes:
  postgres_data:

```

If you were using the above docker compose and wanted to test with a browser on the local host, the URLs in the sample timeline would be replaced by `site:http://localhost:5000`,  `site:http://localhost:5001`, etc. 


## Social Content Directory Structure

The helper expects a social content directory structure as shown below. The sub directory names under the top directory must match the supported theme names as above. Each theme will have one or more topic sub directories (name unimportant) and each topic directory will have one or more post directories (name unimportant). A post directory must have a `post.txt` file that will be used for text posting. If the theme supports image posting, and if the post directory any `.png` or `.jpg` files, then a random image file is chosen to post with the `post.txt` file.

When the social helper browses to a social theme web site, it infers the theme name from the page content, and then uses this theme name to choose a random topic directory, and then select a random post from that topic directory. 

The social helper will work with the Socializer container that was released prior to the combined Socializer/Pandora V9. However, the previous social helper code expected the topic directories to be directly under the top directory, so any content for the previous Socializer/helper combo would have to moved to a `<topdir>/default` subdirectory to work with the new social helper.

```
  <topdir>/
            <themename1>/
                          <topic1>/
                                    <dirname1>/
                                               post.txt
                                               <aimage>.jpg
                                    <dirname2>/
                                               post.txt
                                              <aimage>.jpg
                                    .......
                          <topic2>/
                                   ....
                          ........
                          <topicN>
           <themename2>/
                        <topic1>/
                                 ....
                        ......
                        <toipicN>
          .............
          <themenameN>/
                      ...

```
