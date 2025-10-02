"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/hubs/posts").build();

//Disable the send button until connection is established.
document.getElementById("sendButton").disabled;

// Theme detection function
function getCurrentTheme() {
    console.log('Detecting theme...');

    // First, check for data-theme attribute on body
    const bodyTheme = document.body.getAttribute('data-theme');
    if (bodyTheme) {
        console.log('Detected via data-theme:', bodyTheme);
        return bodyTheme;
    }

    // Fallback: Check if we're in a themed view by looking for theme-specific CSS classes or elements
    if (document.querySelector('.logo') && document.querySelector('.logo').textContent.includes('facebook')) {
        console.log('Detected: facebook');
        return 'facebook';
    } else if (document.querySelector('.instagram-logo')) {
        console.log('Detected: instagram');
        return 'instagram';
    } else if (document.querySelector('.x-logo') || document.querySelector('.tweet')) {
        console.log('Detected: x');
        return 'x';
    } else if (document.querySelector('.linkedin-logo')) {
        console.log('Detected: linkedin');
        return 'linkedin';
    } else if (document.querySelector('.reddit-logo')) {
        console.log('Detected: reddit');
        return 'reddit';
    } else if (document.querySelector('.youtube-logo')) {
        console.log('Detected: youtube');
        return 'youtube';
    } else if (document.querySelector('.discord-logo')) {
        console.log('Detected: discord');
        return 'discord';
    }

    console.log('Detected: default');
    return 'default';
}

// Theme-specific post templates
function getPostTemplate(theme, id, user, message, created) {
    const templates = {
        facebook: `
            <article class="post" data-post-id="${id}">
                <div class="post-header">
                    <div class="post-user-info">
                        <img src="/u/${user}/avatar" class="profile-pic">
                        <div>
                            <div class="post-user-name"><a href="/u/${user}">${user}</a></div>
                            <div class="post-time">${new Date(created).toLocaleDateString('en-US', {month: 'short', day: 'numeric', year: 'numeric'})} · 🌍</div>
                        </div>
                    </div>
                    <button class="post-menu">⋯</button>
                </div>
                <div class="post-content">
                    <div class="post-text">${message}</div>
                </div>
                <div class="post-stats">
                    <span>0 comments</span>
                </div>
                <div class="post-actions">
                    <button class="post-action like-btn like-it" data-id="${id}">👍 Like</button>
                    <button class="post-action comment-btn" data-action="comment">💬 Comment</button>
                    <button class="post-action share-btn" data-action="share">📤 Share</button>
                </div>
            </article>`,

        instagram: `
            <div class="post" data-post-id="${id}">
                <div class="post-header">
                    <div class="post-user">
                        <img src="/u/${user}/avatar" alt="${user}" class="post-avatar">
                        <div class="user-info">
                            <span class="username"><a href="/u/${user}">${user}</a></span>
                            <span class="location">📍 Location</span>
                        </div>
                    </div>
                    <button class="more-options">⋯</button>
                </div>
                <div class="post-actions">
                    <div class="action-buttons">
                        <button class="action-btn like-btn like-it" data-id="${id}">❤️</button>
                        <button class="action-btn comment-btn">💬</button>
                        <button class="action-btn share-btn">📤</button>
                    </div>
                    <button class="save-btn">🔖</button>
                </div>
                <div class="post-info">
                    <div class="caption">
                        <span class="username">${user}</span>
                        <span class="caption-text">${message}</span>
                    </div>
                    <div class="post-time">${new Date(created).toLocaleDateString('en-US', {month: 'short', day: 'numeric', year: 'numeric'})}</div>
                </div>
                <div class="add-comment">
                    <input type="text" placeholder="Add a comment..." class="comment-input">
                    <button class="post-comment">Post</button>
                </div>
            </div>`,

        x: `
            <div class="tweet">
                <img src="/u/${user}/avatar" alt="${user}" class="tweet-avatar">
                <div class="tweet-content">
                    <div class="tweet-header">
                        <span class="tweet-name">${user}</span>
                        <span class="tweet-handle">@${user}</span>
                        <span class="tweet-time">· ${new Date(created).toLocaleDateString()}</span>
                        <span class="tweet-more">⋯</span>
                    </div>
                    <div class="tweet-text">${message}</div>
                    <div class="tweet-actions">
                        <button class="action-btn">
                            <span class="action-icon">💬</span>
                            <span class="action-count">0</span>
                        </button>
                        <button class="action-btn">
                            <span class="action-icon">🔄</span>
                            <span class="action-count">0</span>
                        </button>
                        <button class="action-btn like-it" data-id="${id}">
                            <span class="action-icon">❤️</span>
                            <span class="action-count">0</span>
                        </button>
                        <button class="action-btn">
                            <span class="action-icon">📊</span>
                            <span class="action-count">0</span>
                        </button>
                        <button class="action-btn">
                            <span class="action-icon">📤</span>
                        </button>
                    </div>
                </div>
            </div>`,

        linkedin: `
            <article class="post" data-post-id="${id}">
                <div class="post-header">
                    <div class="post-author">
                        <img src="/u/${user}/avatar" alt="${user}" class="author-avatar">
                        <div class="author-info">
                            <div class="author-name">${user}</div>
                            <div class="author-title">Professional</div>
                            <div class="post-time">${new Date(created).toLocaleDateString('en-US', {month: 'short', day: 'numeric', year: 'numeric'})} • 🌍</div>
                        </div>
                    </div>
                    <button class="post-menu">⋯</button>
                </div>
                <div class="post-content">
                    <div class="post-text">${message}</div>
                </div>
                <div class="post-actions">
                    <button class="action-btn like-btn like-it" data-id="${id}">
                        <span class="action-icon">👍</span>
                        <span class="action-text">Like</span>
                    </button>
                    <button class="action-btn comment-btn">
                        <span class="action-icon">💬</span>
                        <span class="action-text">Comment</span>
                    </button>
                    <button class="action-btn share-btn">
                        <span class="action-icon">📤</span>
                        <span class="action-text">Share</span>
                    </button>
                </div>
            </article>`,

        reddit: `
            <div class="post">
                <div class="post-votes">
                    <button class="vote-btn upvote">▲</button>
                    <span class="vote-count">0</span>
                    <button class="vote-btn downvote">▼</button>
                </div>
                <div class="post-content">
                    <div class="post-header">
                        <img src="/u/${user}/avatar" alt="${user}" class="subreddit-icon">
                        <span class="subreddit-name">r/general</span>
                        <span class="post-meta">
                            • Posted by u/${user}
                            <span class="post-time">${new Date(created).toLocaleDateString()}</span>
                        </span>
                    </div>
                    <div class="post-text">${message}</div>
                    <div class="post-actions">
                        <button class="action-btn">
                            <span class="action-icon">💬</span>
                            <span class="action-text">0 Comments</span>
                        </button>
                        <button class="action-btn like-it" data-id="${id}">
                            <span class="action-icon">❤️</span>
                            <span class="action-text">0 Likes</span>
                        </button>
                        <button class="action-btn">
                            <span class="action-icon">🔗</span>
                            <span class="action-text">Share</span>
                        </button>
                    </div>
                </div>
            </div>`,

        youtube: `
            <div class="video-card">
                <div class="video-thumbnail">
                    <div class="thumbnail-placeholder">
                        <span>Video</span>
                    </div>
                </div>
                <div class="video-info">
                    <img src="/u/${user}/avatar" alt="${user}" class="channel-avatar">
                    <div class="video-details">
                        <div class="video-title">${message}</div>
                        <div class="video-meta">
                            <span class="channel-name">${user}</span>
                            <div class="video-stats">
                                <span>0 likes</span>
                                <span>•</span>
                                <span>0 comments</span>
                                <span>•</span>
                                <span>${new Date(created).toLocaleDateString()}</span>
                            </div>
                            <div class="video-actions">
                                <button class="like-it" data-id="${id}">
                                    ❤️ 0
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>`,

        discord: `
            <div class="message" data-post-id="${id}">
                <div class="message-content">
                    <div class="message-header">
                        <img src="/u/${user}/avatar" alt="${user}" class="message-avatar">
                        <span class="message-author">${user}</span>
                        <span class="message-timestamp">${new Date(created).toLocaleDateString()}</span>
                    </div>
                    <div class="message-text">${message}</div>
                    <div class="message-actions">
                        <button class="reaction-btn like-it" data-id="${id}">❤️</button>
                        <button class="reaction-btn">👍</button>
                        <button class="reaction-btn">😊</button>
                    </div>
                </div>
            </div>`,

        default: `
            <div class="ui-block">
                <article class="hentry post">
                    <div class="post__author author vcard inline-items">
                        <img loading="lazy" src="/u/${user}/avatar" alt="author" width="42" height="42">
                        <div class="author-date">
                            <a class="h6 post__author-name fn" href="/u/${user}">${user}</a> shared a <a href="/posts/${id}">link</a>
                            <div class="post__date">
                                <time class="published" datetime="${created}">${created}</time>
                            </div>
                        </div>
                    </div>
                    ${message}
                    <div class="control-block-button post-control-button">
                        <a href="#" class="btn btn-control like-it" data-id="${id}">
                            <svg class="olymp-like-post-icon">
                                <use xlink:href="#olymp-like-post-icon"></use>
                            </svg>
                        </a>
                    </div>
                </article>
            </div>`
    };

    return templates[theme] || templates.default;
}

connection.on("SendMessage", function (id, user, message, created) {
    // does user div exist and have a value?
    let u = $("#user");
    if(u.val()) {
        if (u.val() === user) {
            let d = document.createElement("div");
            document.getElementById("newsfeed-items-grid").prepend(d);

            // Get current theme and use appropriate template
            const currentTheme = getCurrentTheme();
            d.innerHTML = getPostTemplate(currentTheme, id, user, message, created);

            // Re-bind like button events for the new post
            $(d).find('.like-it').on('click', function() {
                var postId = $(this).data("id");
                console.log("posting like for", postId);
                $.post(`/posts/${postId}/likes`);
            });
        }
    } else {
        let d = document.createElement("div");
        document.getElementById("newsfeed-items-grid").prepend(d);

        // Get current theme and use appropriate template
        const currentTheme = getCurrentTheme();
        d.innerHTML = getPostTemplate(currentTheme, id, user, message, created);

        // Re-bind like button events for the new post
        $(d).find('.like-it').on('click', function() {
            var postId = $(this).data("id");
            console.log("posting like for", postId);
            $.post(`/posts/${postId}/likes`);
        });
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
        console.log("posting like for", id);
        $.post(`/posts/${id}/likes`);
    });
});