// Store token globally
let token = localStorage.getItem("access_token") || "";

// Event listeners for Enter key
document.getElementById("signup-username").addEventListener("keypress", handleEnter);
document.getElementById("signup-password").addEventListener("keypress", handleEnter);
document.getElementById("login-username").addEventListener("keypress", handleEnter);
document.getElementById("login-password").addEventListener("keypress", handleEnter);
document.getElementById("model-input").addEventListener("keypress", handleEnter);

// Handle Enter key press
function handleEnter(event) {
    if (event.key === "Enter") {
        if (event.target.closest("#signup")) {
            signup();
        } else if (event.target.closest("#login")) {
            login();
        } else if (event.target.closest("#query")) {
            generateContent();
        }
    }
}

// Signup function
async function signup() {
    const username = document.getElementById("signup-username").value;
    const password = document.getElementById("signup-password").value;

    try {
        const response = await fetch("/signup", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ username, password })
        });

        if (!response.ok) {
            const errorDetail = await response.json();
            throw new Error(errorDetail.detail || "Signup failed");
        }

        const result = await response.json();
        document.getElementById("response-output").textContent = result.message;
    } catch (error) {
        document.getElementById("response-output").textContent = `Error: ${error.message}`;
    }
}

// Login function
async function login() {
    const username = document.getElementById("login-username").value;
    const password = document.getElementById("login-password").value;

    try {
        const response = await fetch("http://localhost:7860/token", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ username, password })
        });

        if (!response.ok) {
            const errorDetail = await response.json();
            throw new Error(errorDetail.detail || "Login failed");
        }

        const result = await response.json();

        if (result.access_token) {
            token = result.access_token;
            localStorage.setItem("access_token", token);
            document.getElementById("response-output").textContent = `User "${username}" successfully logged in`;
        } else {
            document.getElementById("response-output").textContent = "Login failed: No access token received.";
        }
    } catch (error) {
        document.getElementById("response-output").textContent = `Error: ${error.message}`;
    }
}

// Generate Content function
async function generateContent() {
    const query = document.getElementById("model-input").value;
    const model = document.getElementById("model").value;

    if (!token) {
        token = localStorage.getItem("access_token");
        if (!token) {
            document.getElementById("response-output").textContent = "Error: Not authenticated. Please log in.";
            return;
        }
    }

    try {
        const response = await fetch(`http://localhost:5900/${model}`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Authorization": `Bearer ${token}`
            },
            body: JSON.stringify({ query })
        });

        if (response.ok) {
            const result = await response.json();
            document.getElementById("response-output").textContent = result.message;
        } else {
            const errorDetail = await response.json();
            document.getElementById("response-output").textContent = `Error: ${errorDetail.detail || "Unknown error occurred"}`;
        }
    } catch (error) {
        document.getElementById("response-output").textContent = `Network error: ${error.message}`;
    }
}
