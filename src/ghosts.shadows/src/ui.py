import gradio as gr
import requests
import os

# URLs for authentication and shadows services, defaulting to local if environment variables are not set
auth_host = os.getenv("AUTH_URL", "http://0.0.0.0:8000")
shadows_host = os.getenv("SHADOWS_URL", "http://0.0.0.0:5900")

# Variable to store the JWT token
token = None

# Function to handle user login
def login(username, password):
    global token
    response = requests.post(f"{auth_host}/token", data={"username": username, "password": password})
    if response.status_code == 200:
        token = response.json().get("access_token")
        return "Login successful", True
    else:
        return f"Login failed: {response.json().get('detail', 'Unknown error')}", False

# Function to handle user signup
def signup(username, password):
    global token
    response = requests.post(f"{auth_host}/signup", data={"username": username, "password": password})
    if response.status_code == 200:
        token = response.json().get("access_token")
        return "Signup successful", True
    else:
        return f"Signup failed: {response.json().get('detail', 'Unknown error')}", False

# Function to query the API with the given input text and model
def query_api(input_text, model):
    if not token:
        return "Please log in to access."
    headers = {"Authorization": f"Bearer {token}"}
    response = requests.post(f"{shadows_host}/{model}", json={'query': input_text}, headers=headers)
    return response.json()

# CSS to hide the 'Clear' button
css = """
button:contains('Clear') {
    display: none !important;
}
"""

# Available model options for querying the API
model_options = ["activity", "chat", "content", "social"]

# Function to authenticate a user based on the authentication type (login or signup)
def authenticate(username, password, auth_type):
    if auth_type == "login":
        status, logged_in = login(username, password)
    else:
        status, logged_in = signup(username, password)
    
    return status, logged_in

# Create Gradio interface for querying the API
ux_interface = gr.Interface(
    fn=query_api,
    inputs=[gr.Textbox(label="Query"), gr.Dropdown(label="Model", choices=model_options, value="content")],
    outputs=gr.Textbox(label="Response"),
    description=f"<img src='{shadows_host}/assets/ghosts-shadows.png' width='350' height='350' />",
    flagging_options=None,  # This removes the flagging functionality
    css=css  # This applies the custom CSS to hide the Clear button
)

# Create Gradio Blocks for the UI
with gr.Blocks(css=css) as demo:
    with gr.Tab("Authenticate"):
        with gr.Row():
            with gr.Column(scale=1):
                gr.Image(value=f"{shadows_host}/assets/ghosts-shadows.png", width=350, height=350)
            with gr.Column(scale=2):
                username = gr.Textbox(label="Username")
                password = gr.Textbox(label="Password", type="password")
                auth_type = gr.Radio(["login", "signup"], label="New or existing User?")
                status = gr.Textbox(label="Status", interactive=False)

                # Function to handle authentication and update the status
                def handle_auth(username, password, auth_type):
                    status, logged_in = authenticate(username, password, auth_type)
                    return status, logged_in
                
                auth_output = gr.Button("Submit").click(handle_auth, inputs=[username, password, auth_type], outputs=[status])

    with gr.Tab("Query API"):
        # Function to query the API from the UI, ensuring the user is logged in
        def query_api_ui(query_text, model_choice):
            if token:
                return query_api(query_text, model_choice)
            else:
                return "Please log in to access the Query API."

        query_text = gr.Textbox(label="Query")
        model_choice = gr.Dropdown(label="Model", choices=model_options, value="content")
        response = gr.Textbox(label="Response", interactive=False)
        
        query_output = gr.Button("Submit").click(query_api_ui, inputs=[query_text, model_choice], outputs=[response])

# Launch the Gradio app
demo.launch(share=False, server_name='0.0.0.0')
