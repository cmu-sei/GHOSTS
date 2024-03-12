"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/hubs/posts").build();

//Disable the send button until connection is established.
document.getElementById("sendButton").disabled;

connection.on("SendMessage", function (id, user, message, created) {
    // does user div exist and have a value?
    let u = $("#user");
    if(u.val()) {
        if (u.val() === user) {
            let d = document.createElement("div");
            document.getElementById("newsfeed-items-grid").prepend(d);
            d.innerHTML = `<div class="ui-block"><article class="hentry post"><div class="post__author author vcard inline-items"><img loading="lazy" src="/u/${user}/avatar" alt="author" width="42" height="42"><div class="author-date"><a class="h6 post__author-name fn" href="/u/${user}">${user}</a> shared a <a href="/posts/${id}">link</a><div class="post__date"><time class="published" datetime="${created}">${created}</time></div></div></div>${message}</article></div>`;
        }
    } else {
        let d = document.createElement("div");
        document.getElementById("newsfeed-items-grid").prepend(d);
        d.innerHTML = `<div class="ui-block"><article class="hentry post"><div class="post__author author vcard inline-items"><img loading="lazy" src="/u/${user}/avatar" alt="author" width="42" height="42"><div class="author-date"><a class="h6 post__author-name fn" href="/u/${user}">${user}</a> shared a <a href="/posts/${id}">link</a><div class="post__date"><time class="published" datetime="${created}">${created}</time></div></div></div>${message}</article></div>`;
    }
});

connection.start().then(function () {
    document.getElementById("sendButton").disabled = false;
}).catch(function (err) {
    return console.error(err.toString());
});


$(document).ready(function() {
    $('.like-it').on('click', function() {
        var id = $(this).data("id");
        console.log("posting...")
        $.post(`/posts/${id}/likes`);
    });
});
