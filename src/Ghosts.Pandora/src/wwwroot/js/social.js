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
                        <a href="/u/${user}" class="fb-post-avatar"><img src="/u/${user}/avatar" class="profile-pic" alt=""></a>
                        <div class="post-meta">
                            <div class="post-user-name"><a href="/u/${user}">${typeof fbDisplayName === 'function' ? fbDisplayName(user) : user}</a></div>
                            <div class="post-time"><a href="/posts/${id}">Just now</a> <span aria-hidden="true">·</span> <svg class="fb-globe" aria-label="Shared with Public"><use href="#fb-globe"></use></svg></div>
                        </div>
                    </div>
                    <button type="button" class="post-menu" aria-label="Actions for this post"><svg class="fb-i"><use href="#fb-dots"></use></svg></button>
                    <button type="button" class="fb-post-hide" aria-label="Hide post"><svg class="fb-i"><use href="#fb-close"></use></svg></button>
                </div>
                <div class="post-content">
                    <div class="post-text">${message}</div>
                </div>
                <div class="post-stats" hidden>
                    <div class="fb-reactions" data-total="0"><span class="fb-reaction-icons"><svg><use href="#fb-react-like"></use></svg></span><span class="fb-reaction-text"></span></div>
                    <div class="fb-counts"></div>
                </div>
                <div class="post-actions">
                    <button class="post-action like-btn like-it" data-id="${id}"><svg class="fb-i"><use href="#fb-like"></use></svg><span>Like</span></button>
                    <a href="/posts/${id}" class="post-action comment-btn"><svg class="fb-i"><use href="#fb-comment"></use></svg><span>Comment</span></a>
                    <button type="button" class="post-action share-btn"><svg class="fb-i"><use href="#fb-share"></use></svg><span>Share</span></button>
                </div>
            </article>`,

        instagram: `
            <article class="post" data-post-id="${id}">
                <header class="post-header">
                    <a href="/u/${user}" class="ig-ring"><img src="/u/${user}/avatar" alt="${user}'s profile picture" class="post-avatar"></a>
                    <div class="post-meta">
                        <div class="post-meta-line">
                            <a href="/u/${user}" class="post-user">${user}</a>
                            <span class="ig-sep">•</span>
                            <a href="/posts/${id}" class="post-ago"><time datetime="${created}">now</time></a>
                        </div>
                    </div>
                    <button type="button" class="ig-icon-btn ig-post-more" aria-label="More options" data-user="${user}"><svg class="ig-i"><use href="#ig-dots"/></svg></button>
                </header>
                <div class="post-media" data-ig-dbl-like>
                    <img src="${window.igPhoto ? igPhoto(id, message) : ''}" alt="">
                    <span class="ig-burst" aria-hidden="true"><svg><use href="#ig-heart-fill"/></svg></span>
                </div>
                <div class="post-actions">
                    <div class="action-buttons">
                        <button class="action-btn like-btn like-it" data-id="${id}" aria-label="Like"></button>
                        <a href="/posts/${id}" class="ig-icon-btn" aria-label="Comment"><svg class="ig-i"><use href="#ig-comment"/></svg></a>
                        <button type="button" class="ig-icon-btn ig-share" aria-label="Share Post"><svg class="ig-i"><use href="#ig-share"/></svg></button>
                    </div>
                    <button type="button" class="ig-icon-btn ig-save" aria-label="Save"><svg class="ig-i"><use href="#ig-save"/></svg></button>
                </div>
                <div class="post-likes"><span class="ig-like-count" data-base="${window.igScore ? igScore(id) : 0}">${(window.igScore ? igScore(id) : 0).toLocaleString('en-US')}</span> likes</div>
                <div class="post-caption">
                    <a href="/u/${user}" class="post-user">${user}</a>
                    <span class="caption-text">${window.igCaption ? igCaption(message) : message}</span>
                </div>
                <div class="ig-recent-comments"></div>
                <div class="add-comment">
                    <input type="text" class="comment-input" placeholder="Add a comment…" aria-label="Add a comment…" autocomplete="off">
                    <button type="button" class="comment-post-btn">Post</button>
                    <svg class="ig-i ig-i-13 ig-emoji" aria-hidden="true"><use href="#ig-emoji"/></svg>
                </div>
            </article>`,

        x: `
            <article class="tweet" data-post-id="${id}" data-href="/posts/${id}" data-username="${user}">
                <a href="/u/${user}" class="tweet-avatar-link"><img src="/u/${user}/avatar" alt="" class="tweet-avatar"></a>
                <div class="tweet-content">
                    <div class="tweet-header">
                        <a href="/u/${user}" class="tweet-name">${(window.xNames && window.xNames[user]) || user}</a>
                        <a href="/u/${user}" class="tweet-handle">@${user}</a>
                        <span class="tweet-dot">·</span>
                        <a href="/posts/${id}" class="tweet-time"><time datetime="${created}">now</time></a>
                        <div class="tweet-header-icons">
                            <button type="button" class="x-icon-btn" aria-label="Grok actions"><svg><use href="#x-grok"/></svg></button>
                            <button type="button" class="x-icon-btn" aria-label="More" data-x-menu="post"><svg><use href="#x-dots"/></svg></button>
                        </div>
                    </div>
                    <div class="tweet-text">${message}</div>
                    <div class="tweet-actions" role="group">
                        <a href="/posts/${id}" class="action-btn act-reply" aria-label="Reply"><span class="ab-ic"><svg><use href="#x-reply"/></svg></span><span class="ab-n"></span></a>
                        <button type="button" class="action-btn act-repost" aria-label="Repost" data-count="0"><span class="ab-ic"><svg><use href="#x-repost"/></svg></span><span class="ab-n"></span></button>
                        <button type="button" class="action-btn like-it" aria-label="Like" data-id="${id}" data-count="0"><span class="ab-ic"><svg class="ic-off"><use href="#x-like"/></svg><svg class="ic-on"><use href="#x-like-fill"/></svg></span><span class="ab-n"></span></button>
                        <a href="/posts/${id}" class="action-btn act-views" aria-label="View post analytics"><span class="ab-ic"><svg><use href="#x-views"/></svg></span><span class="ab-n"></span></a>
                        <div class="ab-end">
                            <button type="button" class="action-btn act-bookmark" aria-label="Bookmark"><span class="ab-ic"><svg class="ic-off"><use href="#x-bookmark"/></svg><svg class="ic-on"><use href="#x-bookmark-fill"/></svg></span></button>
                            <button type="button" class="action-btn act-share" aria-label="Share post"><span class="ab-ic"><svg><use href="#x-share"/></svg></span></button>
                        </div>
                    </div>
                </div>
            </article>`,

        linkedin: `
            <article class="li-post" data-post-id="${id}">
                <div class="li-post__header">
                    <a href="/u/${user}" class="li-post__avatar"><img src="/u/${user}/avatar" alt=""></a>
                    <div class="li-post__meta">
                        <a href="/u/${user}" class="li-post__name"><span class="li-post__display">${window.LinkedInTheme ? LinkedInTheme.name(user) : user}</span><span class="li-post__degree"> &bull; ${document.querySelector('meta[name="current-user"]')?.content === user ? 'You' : '2nd'}</span></a>
                        <span class="li-post__headline">${window.LinkedInTheme ? LinkedInTheme.headline(user) : ''}</span>
                        <span class="li-post__time">now &bull; <svg width="16" height="16"><use href="#li-globe"/></svg></span>
                    </div>
                    <div class="li-post__controls">
                        <button type="button" class="li-iconbtn" data-li-postmenu aria-label="Open control menu"><svg width="24" height="24"><use href="#li-more"/></svg></button>
                        <button type="button" class="li-iconbtn" data-li-dismiss aria-label="Dismiss post"><svg width="24" height="24"><use href="#li-close"/></svg></button>
                    </div>
                </div>
                <div class="li-post__body${message.length > 240 ? ' li-post__body--clamp' : ''}"><div class="li-text">${message}</div>${message.length > 240 ? '<button type="button" class="li-seemore" data-li-seemore>&hellip;see more</button>' : ''}</div>
                ${(/<img src="(\/images\/[0-9a-f-]{36}\/image\.(?:jpg|png))"/.exec(message) || [])[1] ? `<a href="/posts/${id}" class="li-post__photo"><img src="${/<img src="([^"]+)"/.exec(message)[1]}" alt=""></a>` : ''}
                <div class="li-post__counts li-post__counts--empty" data-li-counts data-others="0" data-liked="false">
                    <a href="/posts/${id}" class="li-post__reactions li-post__reactions--none"><span class="li-reacticons"><svg width="16" height="16"><use href="#li-r-like"/></svg></span><span class="li-post__reactcount" data-li-reactcount>0</span></a>
                    <span class="li-post__engage"></span>
                </div>
                <div class="li-post__actions">
                    <button class="action-btn like-btn like-it" data-id="${id}" aria-label="React Like"><svg class="li-ic-off" width="24" height="24"><use href="#li-like"/></svg><svg class="li-ic-on" width="24" height="24"><use href="#li-liked"/></svg><span class="action-text">Like</span></button>
                    <a href="/posts/${id}" class="li-action"><svg width="24" height="24"><use href="#li-comment"/></svg><span>Comment</span></a>
                    <div class="li-action-wrap"><button type="button" class="li-action" data-li-repost><svg width="24" height="24"><use href="#li-repost"/></svg><span>Repost</span></button></div>
                    <div class="li-action-wrap"><button type="button" class="li-action" data-li-send><svg width="24" height="24"><use href="#li-send"/></svg><span>Send</span></button></div>
                </div>
            </article>`,

        reddit: `
            <div class="r-item" data-score="1" data-created="${Math.floor(new Date(created).getTime() / 1000)}">
                <article class="r-post" data-post-id="${id}" data-href="/posts/${id}">
                    <div class="r-post-meta">
                        <a class="r-sub" href="/u/${user}"><img class="r-uicon" src="/u/${user}/avatar" alt=""><span class="r-sub-name">u/${user}</span></a>
                        <span class="r-dot">&bull;</span>
                        <time datetime="${created}">just now</time>
                        <div class="r-post-tools"><button type="button" class="r-icon-btn r-overflow" aria-label="Open user actions" data-r="more"><svg class="ri"><use href="#i-more"/></svg></button></div>
                    </div>
                    ${(() => {
                        // the first sentence is the title, as Pandora's SocialPresentation.Title does on the server
                        const parts = /^([\s\S]{10,}?[.!?])\s+([\s\S]+)$/.exec(message);
                        return parts
                            ? `<a class="r-post-title" href="/posts/${id}">${parts[1].replace(/\.$/, '')}</a><div class="r-post-body">${parts[2]}</div>`
                            : `<a class="r-post-title" href="/posts/${id}">${message}</a>`;
                    })()}
                    <div class="post-actions">
                        <span class="r-vote" data-vote="" data-base="1">
                            <button type="button" class="action-btn like-it" data-id="${id}" aria-label="Upvote" aria-pressed="false"><svg class="ri"><use href="#i-up"/></svg></button>
                            <span class="r-score">1</span>
                            <button type="button" aria-label="Downvote" data-r="down"><svg class="ri"><use href="#i-down"/></svg></button>
                        </span>
                        <a class="r-pill" href="/posts/${id}#comments" aria-label="0 comments"><svg class="ri ri-sm"><use href="#i-comment"/></svg><span>0</span></a>
                        <button type="button" class="r-pill r-award" aria-label="Give award" title="Give award"><svg class="ri ri-sm"><use href="#i-award"/></svg></button>
                        <button type="button" class="r-pill" data-r="share" data-url="/posts/${id}"><svg class="ri ri-sm"><use href="#i-share"/></svg><span>Share</span></button>
                    </div>
                </article>
                <hr class="r-sep">
            </div>`,

        youtube: (() => {
            // The message arrives HTML-encoded, with an uploaded image tag when there is one
            const box = document.createElement('div');
            box.innerHTML = message;
            const upload = box.querySelector('img');
            const text = box.textContent.trim();
            const end = text.match(/[.!?](\s|$)/);
            const title = escapeHtml(end && end.index > 10 ? text.slice(0, end.index + 1).replace(/\.$/, '') : text);
            const hex = String(id).replace(/-/g, '');
            const thumb = upload ? upload.getAttribute('src') : `/img/video${parseInt(hex.slice(0, 8), 16) % 17 + 1}.jpg`;
            const seconds = 45 + parseInt(hex.slice(-6), 16) % 1500;
            const owner = document.querySelector(`[data-username="${user}"][data-display-name]`);
            const name = owner ? escapeHtml(owner.textContent.trim()) : user;
            return `
            <div class="yt-video-card" data-post-id="${id}" data-created="${escapeHtml(created)}" data-views="0">
                <a class="yt-thumb" href="/posts/${id}" tabindex="-1" aria-hidden="true">
                    <img src="${thumb}" alt="">
                    <span class="yt-badge">${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}</span>
                </a>
                <div class="yt-details">
                    <a class="yt-avatar" href="/u/${user}"><img src="/u/${user}/avatar" alt=""></a>
                    <div class="yt-meta">
                        <h3 class="yt-title"><a href="/posts/${id}" title="${title}">${title}</a></h3>
                        <div class="yt-byline"><a href="/u/${user}" data-username="${user}" data-display-name>${name}</a></div>
                        <div class="yt-stats"><span>No views</span><span>just now</span></div>
                    </div>
                    <button class="yt-icon-btn yt-card-menu" type="button" aria-label="Action menu"><svg class="yt-icon"><use href="#yt-i-more-vert"/></svg></button>
                </div>
            </div>`;
        })(),

        discord: `
            <div class="message" data-post-id="${id}" data-author="${user}" data-time="${created}">
                <a href="/u/${user}" class="message-avatar-link"><img src="/u/${user}/avatar" alt="" class="message-avatar"></a>
                <div class="message-content">
                    <h3 class="message-header">
                        <a href="/u/${user}" class="message-author dc-name">${user}</a>
                        <time class="message-timestamp" datetime="${created}" data-f="stamp">Today at ${new Date(created).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })}</time>
                    </h3>
                    <div class="message-text">${message}</div>
                    <div class="message-reactions"></div>
                </div>
                <div class="message-actions">
                    <button class="message-action like-it" data-id="${id}" data-tip="Click to react with :heart:">❤️</button>
                    <button type="button" class="message-action" data-reply data-tip="Reply"><svg class="ic" width="20" height="20"><use href="#i-reply"/></svg></button>
                    <a href="/posts/${id}" class="message-action" data-tip="Create Thread"><svg class="ic" width="20" height="20"><use href="#i-threads"/></svg></a>
                    <button type="button" class="message-action" data-message-menu data-tip="More"><svg class="ic" width="20" height="20"><use href="#i-more"/></svg></button>
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
// Delegated, so it covers posts added later; namespaced, so calling it again never binds twice
function initializeLikes() {
    $(document).off('click.pandoraLike').on('click.pandoraLike', '.like-it', function(e) {
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

    // suggest the people on this page, read from their avatar links (/u/{username}/avatar)
    const people = [...document.querySelectorAll('img[src^="/u/"]')]
        .map(img => decodeURIComponent(img.getAttribute('src').split('/')[2] || ''))
        .filter(Boolean);
    const suggestions = [...new Set(people)]
        .filter(item => item.toLowerCase().includes(query.toLowerCase()))
        .slice(0, 8);

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
    // Message input handlers (not for comments)
    const messageInputs = document.querySelectorAll('.message-input');
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
    const sendButtons = document.querySelectorAll('.send-btn');
    sendButtons.forEach(button => {
        button.addEventListener('click', function() {
            const input = this.closest('form, .message-input-container').querySelector('.message-input');
            if (input && input.value.trim()) {
                sendMessage(input.value.trim(), input);
                input.value = '';
            }
        });
    });
}

// Comment functionality
function initializeComments() {
    // Comment post button handlers
    $(document).on('click', '.comment-post-btn', function(e) {
        e.preventDefault();
        const commentContainer = $(this).closest('.add-comment, .comment-input-container');
        const input = commentContainer.find('.comment-input');
        const commentText = input.val().trim();

        if (!commentText) {
            return;
        }

        // Get post ID from the post element
        const postId = $(this).closest('[data-post-id]').data('post-id') ||
                      $(this).closest('.post, .post-detail').find('[data-post-id]').data('post-id');

        if (!postId) {
            console.error('Could not find post ID');
            showToast('Error: Could not post comment');
            return;
        }

        console.log('Posting comment to post', postId);

        // Post comment to server using FormData (controller expects [FromForm])
        const formData = new FormData();
        formData.append('message', commentText);

        $.ajax({
            url: `/posts/${postId}/comments`,
            method: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function(response) {
                console.log('Comment posted successfully', response);

                // Clear input
                input.val('');

                // Add comment to UI
                addCommentToUI(commentText, commentContainer);

                showToast('Comment posted!');
            },
            error: function(error) {
                console.error('Failed to post comment:', error);
                showToast('Failed to post comment');
            }
        });
    });

    // Comment input enter key handler
    $(document).on('keypress', '.comment-input', function(e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            $(this).siblings('.comment-post-btn').click();
        }
    });
}

function addCommentToUI(commentText, commentContainer) {
    // the comment is shown as typed, never as markup
    commentText = escapeHtml(commentText);
    const theme = getCurrentTheme();
    let commentsList = $('.comments-list');

    // For Reddit, look for comments-section instead
    if (commentsList.length === 0 && theme === 'reddit') {
        commentsList = $('.comments-section');
    }

    if (commentsList.length === 0) return;

    // Remove "no comments" message if it exists
    commentsList.find('.no-comments').remove();

    // Get current user (you'd get this from ViewBag or session)
    const currentUser = $('meta[name="current-user"]').attr('content') || 'You';

    let commentHTML = '';

    if (theme === 'linkedin') {
        commentHTML = `
            <div class="comment">
                <img src="/u/${currentUser}/avatar" alt="${currentUser}" class="comment-avatar">
                <div class="comment-content">
                    <div class="comment-header">
                        <span class="comment-author">${currentUser}</span>
                        <span class="comment-title">Professional</span>
                        <span class="comment-time">Just now</span>
                    </div>
                    <div class="comment-text">${commentText}</div>
                    <div class="comment-actions">
                        <button class="comment-action like-action">Like</button>
                        <button class="comment-action reply-action">Reply</button>
                    </div>
                </div>
                <button class="comment-menu">⋯</button>
            </div>
        `;
    } else if (theme === 'reddit') {
        commentHTML = `
            <div class="comment">
                <div class="comment-vote">
                    <button class="vote-btn upvote">▲</button>
                    <span class="vote-count">1</span>
                    <button class="vote-btn downvote">▼</button>
                </div>
                <div class="comment-content">
                    <div class="comment-header">
                        <img src="/u/${currentUser}/avatar" alt="${currentUser}" class="commenter-avatar">
                        <span class="commenter-name">u/${currentUser}</span>
                        <span class="comment-time">Just now</span>
                    </div>
                    <div class="comment-text">${commentText}</div>
                    <div class="comment-actions">
                        <button class="comment-action-btn">
                            <span class="action-icon">↩️</span>
                            <span class="action-text">Reply</span>
                        </button>
                        <button class="comment-action-btn">
                            <span class="action-icon">🏆</span>
                            <span class="action-text">Award</span>
                        </button>
                        <button class="comment-action-btn">
                            <span class="action-icon">🔗</span>
                            <span class="action-text">Share</span>
                        </button>
                    </div>
                </div>
            </div>
        `;
    } else if (theme === 'instagram') {
        commentHTML = `
            <div class="comment">
                <img src="/u/${currentUser}/avatar" alt="${currentUser}" class="comment-avatar">
                <div class="comment-content">
                    <div class="comment-text">
                        <span class="comment-username">${currentUser}</span>
                        <span class="comment-message">${commentText}</span>
                    </div>
                    <div class="comment-actions">
                        <span class="comment-time">Just now</span>
                        <button class="comment-like">Like</button>
                        <button class="comment-reply">Reply</button>
                    </div>
                </div>
                <button class="comment-like-btn">🤍</button>
            </div>
        `;
    } else if (theme === 'x') {
        // For X (Twitter), look for replies container
        commentsList = $('.replies');
        if (commentsList.length === 0) return;

        commentHTML = `
            <div class="reply">
                <img src="/u/${currentUser}/avatar" alt="${currentUser}" class="reply-avatar">
                <div class="reply-content">
                    <div class="reply-header">
                        <span class="reply-name">${currentUser}</span>
                        <span class="reply-handle">@${currentUser}</span>
                        <span class="reply-time">· Just now</span>
                        <span class="reply-more">⋯</span>
                    </div>
                    <div class="reply-text">${commentText}</div>
                    <div class="reply-actions">
                        <button class="action-btn">
                            <span class="action-icon">💬</span>
                        </button>
                        <button class="action-btn">
                            <span class="action-icon">🔄</span>
                        </button>
                        <button class="action-btn">
                            <span class="action-icon">🤍</span>
                        </button>
                        <button class="action-btn">
                            <span class="action-icon">📤</span>
                        </button>
                    </div>
                </div>
            </div>
        `;
    } else if (theme === 'youtube') {
        commentHTML = `
            <div class="comment">
                <img src="/u/${currentUser}/avatar" alt="${currentUser}" class="comment-avatar">
                <div class="comment-content">
                    <div class="comment-header">
                        <span class="commenter-name">${currentUser}</span>
                        <span class="comment-time">Just now</span>
                    </div>
                    <div class="comment-text">${commentText}</div>
                    <div class="comment-actions">
                        <button class="comment-action like-action">
                            <span class="action-icon">👍</span>
                            <span class="like-count">0</span>
                        </button>
                        <button class="comment-action">
                            <span class="action-icon">👎</span>
                        </button>
                        <button class="reply-btn">Reply</button>
                    </div>
                </div>
            </div>
        `;
    }

    // Prepend new comment to the list
    commentsList.prepend(commentHTML);
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
// The server sends message text already encoded; anything else from the hub is escaped here
function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
}

connection.on("SendMessage", function (id, user, theme, message, created) {
    // every Pandora page hears every post; show only the ones made on this site's theme
    if (theme && theme !== getCurrentTheme()) return;

    const userInput = $("#user");
    const shouldShowMessage = !userInput.val() || userInput.val() === user;

    if (shouldShowMessage) {
        const container = document.getElementById("newsfeed-items-grid");
        if (container) {
            const newPostElement = document.createElement("div");
            container.prepend(newPostElement);

            // Get current theme and use appropriate template
            const currentTheme = getCurrentTheme();
            newPostElement.innerHTML = getPostTemplate(currentTheme, escapeHtml(id), escapeHtml(user), message, created);

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
    initializeComments();
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
        initializeComments,
        initializePostMenus
    };
}
