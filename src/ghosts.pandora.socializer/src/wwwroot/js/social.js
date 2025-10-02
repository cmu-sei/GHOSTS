// Unified Social Media JavaScript Library
// Handles posting, commenting, liking, and DM messaging for all themes

"use strict";

// Initialize SignalR connection
var connection = new signalR.HubConnectionBuilder().withUrl("/hubs/posts").build();

// Global state
let currentTheme = 'default';

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
    } else if (document.querySelector('.instagram-logo') || document.querySelector('.post-avatar')) {
        console.log('Detected: instagram');
        return 'instagram';
    } else if (document.querySelector('.x-logo') || document.querySelector('.tweet')) {
        console.log('Detected: x');
        return 'x';
    } else if (document.querySelector('.linkedin-logo') || document.querySelector('.author-avatar')) {
        console.log('Detected: linkedin');
        return 'linkedin';
    } else if (document.querySelector('.reddit-logo') || document.querySelector('.post-votes')) {
        console.log('Detected: reddit');
        return 'reddit';
    } else if (document.querySelector('.youtube-logo') || document.querySelector('.video-card')) {
        console.log('Detected: youtube');
        return 'youtube';
    } else if (document.querySelector('.discord-logo') || document.querySelector('.message-avatar')) {
        console.log('Detected: discord');
        return 'discord';
    }

    console.log('Detected: default');
    return 'default';
}

// Theme-specific post templates for dynamic content
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
            </div>`,

        x: `
            <div class="tweet" data-post-id="${id}">
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
            <div class="post" data-post-id="${id}">
                <div class="post-votes">
                    <button class="vote-btn upvote like-it" data-id="${id}">▲</button>
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
            <div class="video-card" data-post-id="${id}">
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
                <img src="/u/${user}/avatar" alt="${user}" class="message-avatar">
                <div class="message-content">
                    <div class="message-header">
                        <span class="message-author">${user}</span>
                        <span class="message-timestamp">${new Date(created).toLocaleDateString()}</span>
                    </div>
                    <div class="message-text">${message}</div>
                    <div class="message-reactions">
                        <div class="reaction">
                            <span class="reaction-emoji">💬</span>
                            <span class="reaction-count">0</span>
                        </div>
                    </div>
                </div>
                <div class="message-actions">
                    <button class="message-action like-it" data-id="${id}" title="Add Reaction">😀</button>
                    <button class="message-action" title="Reply">↩️</button>
                    <button class="message-action" title="More">⋯</button>
                </div>
            </div>`,

        default: `
            <div class="ui-block">
                <article class="hentry post" data-post-id="${id}">
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

// Universal like functionality
function initializeLikes() {
    $(document).on('click', '.like-it', function(e) {
        e.preventDefault();
        const postId = $(this).data("id");
        console.log("posting like for", postId);

        // Send like to server
        $.post(`/posts/${postId}/likes`)
            .done(function(response) {
                console.log("Like posted successfully");
            })
            .fail(function(error) {
                console.error("Failed to post like:", error);
            });

        // Update UI based on theme
        updateLikeUI($(this));
    });
}

// Update like UI based on current theme
function updateLikeUI(button) {
    const theme = getCurrentTheme();

    switch(theme) {
        case 'facebook':
            if (button.hasClass('liked')) {
                button.removeClass('liked').text('👍 Like');
            } else {
                button.addClass('liked').text('👍 Liked');
                animateButton(button);
            }
            break;

        case 'instagram':
            if (button.hasClass('liked')) {
                button.removeClass('liked').html('🤍');
            } else {
                button.addClass('liked').html('❤️');
                animateButton(button);
            }
            break;

        case 'x':
            const countSpan = button.find('.action-count');
            let count = parseInt(countSpan.text()) || 0;
            if (button.hasClass('liked')) {
                button.removeClass('liked');
                countSpan.text(Math.max(0, count - 1));
            } else {
                button.addClass('liked');
                countSpan.text(count + 1);
                animateButton(button);
            }
            break;

        case 'linkedin':
            const textSpan = button.find('.action-text');
            if (button.hasClass('liked')) {
                button.removeClass('liked');
                textSpan.text('Like');
            } else {
                button.addClass('liked');
                textSpan.text('Liked');
                animateButton(button);
            }
            break;

        case 'reddit':
            const voteCount = button.siblings('.vote-count');
            let votes = parseInt(voteCount.text()) || 0;
            if (button.hasClass('voted')) {
                button.removeClass('voted');
                voteCount.text(Math.max(0, votes - 1));
            } else {
                button.addClass('voted').siblings('.vote-btn').removeClass('voted');
                voteCount.text(votes + 1);
                animateButton(button);
            }
            break;

        case 'youtube':
            const likeText = button.text().split(' ');
            let likeCount = parseInt(likeText[1]) || 0;
            if (button.hasClass('liked')) {
                button.removeClass('liked').text(`❤️ ${Math.max(0, likeCount - 1)}`);
            } else {
                button.addClass('liked').text(`❤️ ${likeCount + 1}`);
                animateButton(button);
            }
            break;

        case 'discord':
            // Discord uses reactions, add heart reaction
            if (!button.hasClass('reacted')) {
                button.addClass('reacted');
                const messageContent = button.closest('.message').find('.message-content');
                let reactions = messageContent.find('.message-reactions');
                if (reactions.length === 0) {
                    reactions = $('<div class="message-reactions"></div>');
                    messageContent.append(reactions);
                }

                let heartReaction = reactions.find('.reaction[data-emoji="❤️"]');
                if (heartReaction.length === 0) {
                    heartReaction = $('<div class="reaction" data-emoji="❤️"><span class="reaction-emoji">❤️</span><span class="reaction-count">1</span></div>');
                    reactions.append(heartReaction);
                } else {
                    const count = parseInt(heartReaction.find('.reaction-count').text()) + 1;
                    heartReaction.find('.reaction-count').text(count);
                }
                animateButton(button);
            }
            break;

        default:
            button.addClass('liked');
            animateButton(button);
            break;
    }
}

// Button animation
function animateButton(button) {
    button.css('transform', 'scale(1.2)');
    setTimeout(() => {
        button.css('transform', 'scale(1)');
    }, 150);
}

// Universal search functionality
function initializeSearch() {
    const searchInputs = document.querySelectorAll('.search-bar, .search-input');

    searchInputs.forEach(input => {
        input.addEventListener('input', function() {
            const query = this.value.trim();
            if (query.length > 2) {
                showSearchSuggestions(query, this);
            } else {
                hideSearchSuggestions();
            }
        });

        input.addEventListener('keypress', function(e) {
            if (e.key === 'Enter') {
                const query = this.value.trim();
                if (query) {
                    performSearch(query);
                }
            }
        });
    });
}

// Search suggestions
function showSearchSuggestions(query, input) {
    hideSearchSuggestions();

    const suggestions = [
        'Sarah Wilson', 'Mike Johnson', 'Emma Davis', 'Tech Company',
        'Beach Photos', 'Marathon Training', 'JavaScript Tutorial',
        'React Development', 'Node.js Tips', 'CSS Tricks'
    ].filter(item => item.toLowerCase().includes(query.toLowerCase()));

    if (suggestions.length === 0) return;

    const dropdown = document.createElement('div');
    dropdown.className = 'search-suggestions';
    dropdown.style.cssText = `
        position: absolute;
        top: 100%;
        left: 0;
        right: 0;
        background: white;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        max-height: 300px;
        overflow-y: auto;
        z-index: 1000;
        border: 1px solid #e1e5e9;
    `;

    suggestions.forEach(suggestion => {
        const item = document.createElement('div');
        item.style.cssText = `
            padding: 12px 16px;
            cursor: pointer;
            border-bottom: 1px solid #e4e6ea;
        `;
        item.textContent = suggestion;

        item.addEventListener('mouseenter', function() {
            this.style.backgroundColor = '#f0f2f5';
        });
        item.addEventListener('mouseleave', function() {
            this.style.backgroundColor = 'transparent';
        });
        item.addEventListener('click', function() {
            input.value = suggestion;
            hideSearchSuggestions();
            performSearch(suggestion);
        });

        dropdown.appendChild(item);
    });

    input.parentElement.style.position = 'relative';
    input.parentElement.appendChild(dropdown);
}

function hideSearchSuggestions() {
    const existing = document.querySelector('.search-suggestions');
    if (existing) {
        existing.remove();
    }
}

function performSearch(query) {
    showToast(`Searching for: ${query}`);
    // Here you would implement actual search functionality
    // For now, just show a toast notification
}

// Universal messaging functionality
function initializeMessaging() {
    // Message input handlers
    const messageInputs = document.querySelectorAll('.message-input, .comment-input');
    messageInputs.forEach(input => {
        input.addEventListener('keypress', function(e) {
            if (e.key === 'Enter' && !e.shiftKey && this.value.trim()) {
                e.preventDefault();
                sendMessage(this.value.trim(), this);
                this.value = '';
            }
        });
    });

    // Send button handlers
    const sendButtons = document.querySelectorAll('.send-btn, .post-comment');
    sendButtons.forEach(button => {
        button.addEventListener('click', function() {
            const input = this.closest('form, .message-input-container').querySelector('input, textarea');
            if (input && input.value.trim()) {
                sendMessage(input.value.trim(), input);
                input.value = '';
            }
        });
    });
}

function sendMessage(message, inputElement) {
    // This would typically send to your messaging API
    console.log('Sending message:', message);
    showToast('Message sent!');

    // Add message to UI if in a conversation view
    if (inputElement.classList.contains('message-input')) {
        addMessageToUI(message, 'You', new Date());
    }
}

function addMessageToUI(message, author, timestamp) {
    const theme = getCurrentTheme();
    const messagesContainer = document.querySelector('.messages-list, .conversation-messages, .message-thread');

    if (!messagesContainer) return;

    const messageElement = document.createElement('div');
    messageElement.className = theme === 'discord' ? 'message' : 'message-item';

    // Use theme-appropriate markup
    switch(theme) {
        case 'discord':
            messageElement.innerHTML = `
                <img src="/u/${author}/avatar" alt="${author}" class="message-avatar">
                <div class="message-content">
                    <div class="message-header">
                        <span class="message-author">${author}</span>
                        <span class="message-timestamp">${timestamp.toLocaleTimeString()}</span>
                    </div>
                    <div class="message-text">${message}</div>
                </div>
            `;
            break;
        default:
            messageElement.innerHTML = `
                <div class="message-author">${author}</div>
                <div class="message-content">${message}</div>
                <div class="message-time">${timestamp.toLocaleTimeString()}</div>
            `;
    }

    messagesContainer.appendChild(messageElement);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

// Universal post menu functionality
function initializePostMenus() {
    $(document).on('click', '.post-menu, .more-options, .tweet-more, .message-action[title="More"]', function(e) {
        e.stopPropagation();
        showPostMenu($(this));
    });

    // Close menu when clicking outside
    $(document).on('click', function() {
        $('.post-dropdown-menu').remove();
    });
}

function showPostMenu(button) {
    // Remove existing menu
    $('.post-dropdown-menu').remove();

    const menu = $('<div class="post-dropdown-menu"></div>');
    menu.css({
        'position': 'absolute',
        'background': 'white',
        'border-radius': '8px',
        'box-shadow': '0 4px 12px rgba(0,0,0,0.15)',
        'padding': '8px 0',
        'z-index': '1000',
        'min-width': '200px',
        'top': (button.offset().top + 30) + 'px',
        'right': '16px',
        'border': '1px solid #e1e5e9'
    });

    const menuItems = [
        { text: 'Save post', action: 'save' },
        { text: 'Hide post', action: 'hide' },
        { text: 'Report post', action: 'report' },
        { text: 'Copy link', action: 'copy' }
    ];

    menuItems.forEach(item => {
        const menuItem = $(`<div class="menu-item">${item.text}</div>`);
        menuItem.css({
            'padding': '8px 16px',
            'cursor': 'pointer',
            'transition': 'background-color 0.2s'
        });

        menuItem.hover(
            function() { $(this).css('background-color', '#f0f2f5'); },
            function() { $(this).css('background-color', 'transparent'); }
        );

        menuItem.on('click', function() {
            handleMenuAction(item.action);
            menu.remove();
        });

        menu.append(menuItem);
    });

    button.closest('.post, .tweet, .message').css('position', 'relative').append(menu);
}

function handleMenuAction(action) {
    switch(action) {
        case 'save':
            showToast('Post saved!');
            break;
        case 'hide':
            showToast('Post hidden');
            break;
        case 'report':
            showToast('Post reported');
            break;
        case 'copy':
            navigator.clipboard.writeText(window.location.href).then(() => {
                showToast('Link copied to clipboard!');
            });
            break;
    }
}

// Toast notification system
function showToast(message) {
    // Remove existing toast
    $('.toast').remove();

    const toast = $(`<div class="toast">${message}</div>`);
    toast.css({
        'position': 'fixed',
        'bottom': '20px',
        'left': '50%',
        'transform': 'translateX(-50%)',
        'background': '#333',
        'color': 'white',
        'padding': '12px 24px',
        'border-radius': '24px',
        'z-index': '10000',
        'opacity': '0',
        'transition': 'opacity 0.3s ease'
    });

    $('body').append(toast);

    // Animate in
    setTimeout(() => toast.css('opacity', '1'), 100);

    // Auto remove
    setTimeout(() => {
        toast.css('opacity', '0');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// SignalR message handling
connection.on("SendMessage", function (id, user, message, created) {
    const userInput = $("#user");
    const shouldShowMessage = !userInput.val() || userInput.val() === user;

    if (shouldShowMessage) {
        const container = document.getElementById("newsfeed-items-grid");
        if (container) {
            const newPostElement = document.createElement("div");
            container.prepend(newPostElement);

            // Get current theme and use appropriate template
            const currentTheme = getCurrentTheme();
            newPostElement.innerHTML = getPostTemplate(currentTheme, id, user, message, created);

            // Re-bind event handlers for the new post
            initializeLikes();
            initializePostMenus();
        }
    }
});

// Initialize everything when DOM is ready
$(document).ready(function() {
    console.log('Social.js initializing...');

    // Detect and store current theme
    currentTheme = getCurrentTheme();
    console.log('Current theme:', currentTheme);

    // Initialize all functionality
    initializeLikes();
    initializeSearch();
    initializeMessaging();
    initializePostMenus();

    // Initialize SignalR connection
    connection.start().then(function () {
        const sendButton = document.getElementById("sendButton");
        if (sendButton) {
            sendButton.disabled = false;
        }
        console.log('SignalR connection established');
    }).catch(function (err) {
        console.error('SignalR connection failed:', err.toString());
    });

    console.log('Social.js initialization complete');
});

// Export for use by other scripts if needed
if (typeof window !== 'undefined') {
    window.SocialJS = {
        getCurrentTheme,
        showToast,
        initializeLikes,
        initializeSearch,
        initializeMessaging,
        initializePostMenus
    };
}