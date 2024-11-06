# GHOSTS PANDORA SERVER

GHOSTS PANDORA is a web server that responds to a myriad of request types with randomized content generated in real-time. Used in conjunction with [GHOSTS](https://github.com/cmu-sei/GHOSTS) NPCs, the two can provide for agents that are periodically downloading content other than simple HTML and associated image, CSS, and JS files.

## Running this server

Pandora is a FastAPI application — run it using Uvicorn by executing the following command in app directory:

```bash
uvicorn main:app --host 0.0.0.0 --port 80 --reload
```

### Creating a Virtual Environment

1. **Navigate to your project directory.**
2. **Create a virtual environment:**
    ```bash
    python3 -m venv venv
    ```
3. **Activate the virtual environment:**
    - On Linux/Mac:
        ```bash
        source venv/bin/activate
        ```
    - On Windows:
        ```bash
        venv\Scripts\activate
        ```

### Bare Metal

This assumes the host server is a common Linux distribution. For images to render correctly, the PIL or the more recent Pillow library is necessary. See here for more information on [Pillow installation and configuration](https://pillow.readthedocs.io/en/latest/installation.html).

1. Using a Python 3 distribution >= 3.9.0
2. In the terminal, run:
    ```bash
    pip install -r requirements.txt
    ```

### As a Docker Container

1. See the included `docker-compose.yml` file.
2. Run:
    ```bash
    docker-compose up -d
    ```

## Capabilities

### Video

To enable streaming video:

1. In the container's `/usr/src/app/app.config` file:

    ```bash
    $ docker exec -it pandora /bin/bash
    $ vi /usr/src/app/app.config

    [video]
    video_enabled=False
    nginx_enabled=False
    ```

2. Set these to `True`, save the file, and exit.
3. Exit the container and restart it. It should start.

(If starting the container via `docker run -p 80:80 --name pandora -d dustinupdyke/ghosts-pandora:0.5.1`)

### By Directory

-   **/api** - All requests beginning with `/api` automatically respond with JSON. This includes:
    -   `/api/users`
    -   `/api/user/a320f971-b3d9-4b79-bb8d-b41d02572942`
    -   `/api/reports/personnel`
-   **/csv** - All requests beginning with `/csv` automatically respond with CSV. Like the above, this includes URLs such as:
    -   `/csv/users`
    -   `/csv/user/winx.jalton`
    -   `/csv/reports/HR/payroll`
-   **/i, /img, /images** - All requests beginning with these directories automatically respond with a random image of type [gif, jpg, png]. Examples:
    -   `/i/v1/a9f6e2b7-636c-4821-acf4-90220f091351/f8f8b1f0-9aa5-4fc7-8880-379e3192748e/small`
    -   `/images/products/184f3515-f49b-4e07-8c8b-7f978666df0e/view`
    -   `/img/432.png`
-   **/pdf** - All requests respond with a random PDF document. Examples:
    -   `/pdf/operations/SOP_Vault/a7f48bd5-84cb-43a1-8d3d-cd2c732ddff6`
    -   `/pdf/products`
-   **/docs** - All requests respond with a random Word document.
-   **/slides** - All requests respond with a random PowerPoint document.
-   **/sheets** - All requests respond with a random Excel document.

### By Request Type

For requests indicating a specific file type, there are a number of specific handlers built to respond with that particular kind of file, such as:

-   .csv
-   Image requests [.gif, .ico, .jpg, .jpeg, .png]
-   .json
-   Office document requests
    -   .doc, .docx
    -   .ppt, .pptx
    -   .xls, .xlsx
-   .pdf

So that a URL such as `/users/58361185-c9f2-460f-ac45-cb845ba88574/profile.pdf` would return a PDF document typically rendered right in the browser.

All unhandled request types, URLs without a specific file indicator, or requests made outside specifically handled directories (from the preceding section) are returned as HTML, including:

-   `/docs/by_department/operations/users`
-   `/blog/d/2022/12/4/blog_title-text`
-   `/hello/index.html`

## API Documentation

API documentation can be found at `/docs`.

[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.

Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.
