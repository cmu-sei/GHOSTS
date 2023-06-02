"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/hubs/posts").build();

//Disable the send button until connection is established.
document.getElementById("sendButton").disabled;

connection.on("SendMessage", function (id, user, message, created) {
    let d = document.createElement("div");
    document.getElementById("itemList").prepend(d);
    d.innerHTML = `<div class="col" id="${id}"><div class="card h-100"><div class="card-body"><p class="card-text">${message}</p><p class="card-text"><small class="text-muted">${user}</small></p><p class="created">${created}</p></div></div></div>`;
});

connection.start().then(function () {
    document.getElementById("sendButton").disabled = false;
}).catch(function (err) {
    return console.error(err.toString());
});

// document.getElementById("sendButton").addEventListener("click", function (event) {
//     let user = document.getElementById("u").value;
//     let message = document.getElementById("m").value;
//     let id = uuidv4();
//     let created = Date.UTC();
//     connection.invoke("SendMessage", id, user, message, created).catch(function (err) {
//         return console.error(err.toString());
//     });
//     event.preventDefault();
// });


function uuidv4() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        let r = Math.random() * 16 | 0, v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}