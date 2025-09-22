// Reddit Post JavaScript

document.addEventListener('DOMContentLoaded', function() {
    // Vote functionality for main post and comments
    const voteButtons = document.querySelectorAll('.vote-btn');
    voteButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            const isUpvote = this.classList.contains('upvote');
            const isDownvote = this.classList.contains('downvote');
            const voteContainer = this.closest('.post-votes, .comment-vote');
            const voteCount = voteContainer.querySelector('.vote-count');
            const upvoteBtn = voteContainer.querySelector('.upvote');
            const downvoteBtn = voteContainer.querySelector('.downvote');

            let currentCount = parseInt(voteCount.textContent.replace(/[^\d]/g, '')) || 0;

            // Check if already voted
            const wasUpvoted = upvoteBtn.classList.contains('upvoted');
            const wasDownvoted = downvoteBtn.classList.contains('downvoted');

            // Reset both buttons
            upvoteBtn.classList.remove('upvoted');
            downvoteBtn.classList.remove('downvoted');

            if (isUpvote) {
                if (!wasUpvoted) {
                    this.classList.add('upvoted');
                    currentCount += wasDownvoted ? 2 : 1;
                } else {
                    currentCount -= 1;
                }
            } else if (isDownvote) {
                if (!wasDownvoted) {
                    this.classList.add('downvoted');
                    currentCount -= wasUpvoted ? 2 : 1;
                } else {
                    currentCount += 1;
                }
            }

            voteCount.textContent = formatCount(currentCount);
        });
    });

    // Comment compose functionality
    const composeTextarea = document.querySelector('.compose-textarea');
    const composeBtn = document.querySelector('.compose-btn');

    if (composeTextarea && composeBtn) {
        composeTextarea.addEventListener('input', function() {
            composeBtn.disabled = this.value.trim().length === 0;
        });

        composeBtn.addEventListener('click', function() {
            if (!this.disabled) {
                const commentText = composeTextarea.value.trim();
                if (commentText) {
                    addNewComment(commentText);
                    composeTextarea.value = '';
                    this.disabled = true;
                }
            }
        });
    }

    // Sort comments functionality
    const sortSelect = document.querySelector('.sort-select');
    if (sortSelect) {
        sortSelect.addEventListener('change', function() {
            const sortType = this.value;
            sortComments(sortType);
        });
    }

    // Join/Leave subreddit functionality
    const joinBtn = document.querySelector('.join-btn');
    if (joinBtn) {
        joinBtn.addEventListener('click', function() {
            if (this.classList.contains('joined')) {
                this.classList.remove('joined');
                this.textContent = 'Join';
                this.style.backgroundColor = '#0079d3';
                showToast('Left r/AskReddit');
            } else {
                this.classList.add('joined');
                this.textContent = 'Joined';
                this.style.backgroundColor = '#46d160';
                showToast('Joined r/AskReddit');
            }
        });
    }

    // Bell button functionality
    const bellBtn = document.querySelector('.bell-btn');
    if (bellBtn) {
        bellBtn.addEventListener('click', function() {
            const isActive = this.style.color === 'rgb(255, 196, 0)'; // gold color

            if (isActive) {
                this.style.color = '#818384';
                showToast('Notifications disabled for r/AskReddit');
            } else {
                this.style.color = '#ffc400';
                showToast('Notifications enabled for r/AskReddit');
            }
        });
    }

    // Comment action buttons
    const commentActionButtons = document.querySelectorAll('.comment-action-btn');
    commentActionButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            const actionText = this.querySelector('.action-text').textContent;
            const commentElement = this.closest('.comment');

            switch (actionText) {
                case 'Reply':
                    showReplyBox(commentElement);
                    break;
                case 'Award':
                    showAwardModal();
                    break;
                case 'Share':
                    shareComment(commentElement);
                    break;
                default:
                    showCommentOptionsMenu(this);
                    break;
            }
        });
    });

    // Post action buttons
    const postActionButtons = document.querySelectorAll('.original-post .action-btn');
    postActionButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            const actionText = this.querySelector('.action-text').textContent;

            if (actionText.includes('Comments')) {
                // Scroll to comments
                document.querySelector('.comments-section').scrollIntoView({ behavior: 'smooth' });
            } else if (actionText === 'Share') {
                if (navigator.share) {
                    navigator.share({
                        title: document.querySelector('.post-title').textContent,
                        url: window.location.href
                    });
                } else {
                    navigator.clipboard.writeText(window.location.href);
                    showToast('Link copied to clipboard!');
                }
            } else if (actionText === 'Save') {
                this.querySelector('.action-text').textContent =
                    actionText === 'Save' ? 'Saved' : 'Save';
                showToast(actionText === 'Save' ? 'Post saved!' : 'Post unsaved!');
            }
        });
    });

    // Load more comments
    const loadMoreBtn = document.querySelector('.load-more-btn');
    if (loadMoreBtn) {
        loadMoreBtn.addEventListener('click', function() {
            loadMoreComments();
        });
    }

    // Trending posts navigation
    const trendingPosts = document.querySelectorAll('.trending-post');
    trendingPosts.forEach(post => {
        post.addEventListener('click', function() {
            const title = this.querySelector('.trending-title').textContent;
            alert(`Navigate to post: "${title}"`);
        });
    });

    // Related communities
    const communityItems = document.querySelectorAll('.community-item');
    communityItems.forEach(item => {
        const joinBtn = item.querySelector('.join-btn');
        if (joinBtn) {
            joinBtn.addEventListener('click', function(e) {
                e.stopPropagation();

                if (this.textContent === 'Join') {
                    this.textContent = 'Joined';
                    this.style.backgroundColor = '#46d160';
                } else {
                    this.textContent = 'Join';
                    this.style.backgroundColor = '#0079d3';
                }
            });
        }

        item.addEventListener('click', function() {
            const communityName = this.querySelector('.community-name').textContent;
            alert(`Navigate to ${communityName}`);
        });
    });

    // Moderator items
    const moderatorItems = document.querySelectorAll('.moderator-item');
    moderatorItems.forEach(item => {
        item.addEventListener('click', function() {
            const modName = this.querySelector('.mod-name').textContent;
            alert(`View profile of ${modName}`);
        });
    });

    // Search functionality
    const searchInput = document.querySelector('.search-input');
    if (searchInput) {
        searchInput.addEventListener('focus', function() {
            this.style.backgroundColor = '#1a1a1b';
        });

        searchInput.addEventListener('blur', function() {
            if (!this.value) {
                this.style.backgroundColor = '#272729';
            }
        });

        searchInput.addEventListener('keypress', function(e) {
            if (e.key === 'Enter') {
                const query = this.value.trim();
                if (query) {
                    alert(`Searching for: ${query}`);
                }
            }
        });
    }

    // Header buttons
    const headerButtons = document.querySelectorAll('.header-btn');
    headerButtons.forEach(button => {
        button.addEventListener('click', function() {
            const icon = this.textContent;
            let action = '';

            switch (icon) {
                case '📈':
                    action = 'Popular/Trending';
                    break;
                case '💬':
                    action = 'Messages';
                    break;
                case '🔔':
                    action = 'Notifications';
                    break;
                case '➕':
                    action = 'Create Post';
                    break;
            }

            alert(`${action} functionality would open here`);
        });
    });

    // User menu
    const userMenu = document.querySelector('.user-menu');
    if (userMenu) {
        userMenu.addEventListener('click', function() {
            showUserMenu(this);
        });
    }
});

// Helper function to format numbers
function formatCount(count) {
    if (count >= 1000000) {
        return (count / 1000000).toFixed(1) + 'M';
    } else if (count >= 1000) {
        return (count / 1000).toFixed(1) + 'k';
    }
    return count.toString();
}

// Add new comment
function addNewComment(commentText) {
    const commentsContainer = document.querySelector('.comments-section');
    if (!commentsContainer) return;

    const newCommentHTML = `
        <div class="comment">
            <div class="comment-vote">
                <button class="vote-btn upvote">▲</button>
                <span class="vote-count">1</span>
                <button class="vote-btn downvote">▼</button>
            </div>
            <div class="comment-content">
                <div class="comment-header">
                    <img src="images/profile-avatar.jpg" alt="User" class="commenter-avatar">
                    <span class="commenter-name">u/johndoe</span>
                    <span class="comment-time">just now</span>
                </div>
                <div class="comment-text">
                    ${commentText}
                </div>
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
                    <button class="comment-action-btn">
                        <span class="action-icon">⋯</span>
                    </button>
                </div>
            </div>
        </div>
    `;

    const div = document.createElement('div');
    div.innerHTML = newCommentHTML;
    const newComment = div.firstElementChild;

    // Insert at the top of comments
    commentsContainer.insertBefore(newComment, commentsContainer.firstChild);

    // Add event listeners to the new comment
    addCommentEventListeners(newComment);

    // Update comment count
    updateCommentCount(1);

    showToast('Comment posted!');
}

// Add event listeners to a comment element
function addCommentEventListeners(commentElement) {
    // Vote buttons
    const voteButtons = commentElement.querySelectorAll('.vote-btn');
    voteButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();
            // Vote logic here (same as above)
        });
    });

    // Action buttons
    const actionButtons = commentElement.querySelectorAll('.comment-action-btn');
    actionButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();
            const actionText = this.querySelector('.action-text').textContent;

            switch (actionText) {
                case 'Reply':
                    showReplyBox(commentElement);
                    break;
                case 'Award':
                    showAwardModal();
                    break;
                case 'Share':
                    shareComment(commentElement);
                    break;
                default:
                    showCommentOptionsMenu(this);
                    break;
            }
        });
    });
}

// Show reply box
function showReplyBox(commentElement) {
    // Remove existing reply box
    const existingReplyBox = commentElement.querySelector('.reply-box');
    if (existingReplyBox) {
        existingReplyBox.remove();
        return;
    }

    const replyBoxHTML = `
        <div class="reply-box" style="margin-top: 12px; margin-left: 36px;">
            <div style="display: flex; gap: 12px;">
                <img src="images/profile-avatar.jpg" alt="Profile" style="width: 24px; height: 24px; border-radius: 50%;">
                <div style="flex: 1;">
                    <textarea placeholder="What are your thoughts?" style="width: 100%; background-color: #272729; border: 1px solid #343536; border-radius: 4px; padding: 8px; color: #d7dadc; font-size: 14px; resize: vertical; min-height: 80px; outline: none; font-family: inherit;" class="reply-textarea"></textarea>
                    <div style="display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px;">
                        <button style="background: none; border: 1px solid #343536; color: #d7dadc; border-radius: 20px; padding: 4px 16px; font-size: 12px; cursor: pointer;" class="cancel-reply">Cancel</button>
                        <button style="background-color: #0079d3; color: white; border: none; border-radius: 20px; padding: 4px 16px; font-size: 12px; cursor: pointer; opacity: 0.5;" class="submit-reply" disabled>Reply</button>
                    </div>
                </div>
            </div>
        </div>
    `;

    const commentContent = commentElement.querySelector('.comment-content');
    commentContent.insertAdjacentHTML('beforeend', replyBoxHTML);

    const replyBox = commentContent.querySelector('.reply-box');
    const replyTextarea = replyBox.querySelector('.reply-textarea');
    const submitBtn = replyBox.querySelector('.submit-reply');
    const cancelBtn = replyBox.querySelector('.cancel-reply');

    // Enable/disable submit button
    replyTextarea.addEventListener('input', function() {
        submitBtn.disabled = this.value.trim().length === 0;
        submitBtn.style.opacity = this.value.trim().length === 0 ? '0.5' : '1';
    });

    // Submit reply
    submitBtn.addEventListener('click', function() {
        const replyText = replyTextarea.value.trim();
        if (replyText) {
            addNestedComment(commentElement, replyText);
            replyBox.remove();
        }
    });

    // Cancel reply
    cancelBtn.addEventListener('click', function() {
        replyBox.remove();
    });

    // Focus textarea
    replyTextarea.focus();
}

// Add nested comment
function addNestedComment(parentComment, replyText) {
    const nestedCommentHTML = `
        <div class="comment nested" style="margin-top: 12px;">
            <div class="comment-vote">
                <button class="vote-btn upvote">▲</button>
                <span class="vote-count">1</span>
                <button class="vote-btn downvote">▼</button>
            </div>
            <div class="comment-content">
                <div class="comment-header">
                    <img src="images/profile-avatar.jpg" alt="User" class="commenter-avatar">
                    <span class="commenter-name">u/johndoe</span>
                    <span class="comment-time">just now</span>
                </div>
                <div class="comment-text">
                    ${replyText}
                </div>
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

    // Find or create nested comments container
    let nestedContainer = parentComment.querySelector('.nested-comments');
    if (!nestedContainer) {
        nestedContainer = document.createElement('div');
        nestedContainer.className = 'nested-comments';
        nestedContainer.style.marginTop = '12px';
        parentComment.querySelector('.comment-content').appendChild(nestedContainer);
    }

    const div = document.createElement('div');
    div.innerHTML = nestedCommentHTML;
    const newComment = div.firstElementChild;

    nestedContainer.appendChild(newComment);
    addCommentEventListeners(newComment);

    showToast('Reply posted!');
}

// Sort comments
function sortComments(sortType) {
    console.log(`Sorting comments by: ${sortType}`);
    showToast(`Comments sorted by ${sortType}`);
}

// Load more comments
function loadMoreComments() {
    const loadMoreBtn = document.querySelector('.load-more-btn');
    const commentsContainer = document.querySelector('.comments-section');

    loadMoreBtn.textContent = 'Loading...';
    loadMoreBtn.disabled = true;

    setTimeout(() => {
        // Add mock comments
        for (let i = 0; i < 3; i++) {
            const mockCommentHTML = `
                <div class="comment">
                    <div class="comment-vote">
                        <button class="vote-btn upvote">▲</button>
                        <span class="vote-count">${Math.floor(Math.random() * 100)}</span>
                        <button class="vote-btn downvote">▼</button>
                    </div>
                    <div class="comment-content">
                        <div class="comment-header">
                            <img src="images/user${i + 1}-avatar.jpg" alt="User" class="commenter-avatar">
                            <span class="commenter-name">u/random_user_${i + 1}</span>
                            <span class="comment-time">just loaded</span>
                        </div>
                        <div class="comment-text">
                            This is a dynamically loaded comment to demonstrate the load more functionality!
                        </div>
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

            const div = document.createElement('div');
            div.innerHTML = mockCommentHTML;
            const newComment = div.firstElementChild;

            commentsContainer.insertBefore(newComment, document.querySelector('.load-more'));
            addCommentEventListeners(newComment);
        }

        loadMoreBtn.textContent = 'Load 20 more comments';
        loadMoreBtn.disabled = false;

        showToast('More comments loaded!');
    }, 1000);
}

// Update comment count
function updateCommentCount(increment) {
    const commentButtons = document.querySelectorAll('.action-btn');
    commentButtons.forEach(button => {
        const actionText = button.querySelector('.action-text');
        if (actionText && actionText.textContent.includes('Comments')) {
            const currentCount = parseInt(actionText.textContent.match(/\d+/)[0]);
            actionText.textContent = `${currentCount + increment} Comments`;
        }
    });
}

// Show award modal
function showAwardModal() {
    alert('Award functionality would open a modal here');
}

// Share comment
function shareComment(commentElement) {
    const commentText = commentElement.querySelector('.comment-text').textContent;
    const commentUrl = `${window.location.href}#comment-${Date.now()}`;

    if (navigator.share) {
        navigator.share({
            title: 'Reddit Comment',
            text: commentText,
            url: commentUrl
        });
    } else {
        navigator.clipboard.writeText(commentUrl);
        showToast('Comment link copied to clipboard!');
    }
}

// Show comment options menu
function showCommentOptionsMenu(button) {
    alert('Comment options menu would open here');
}

// Show user menu (same as other files)
function showUserMenu(element) {
    // Remove existing menu
    const existingMenu = document.querySelector('.user-dropdown');
    if (existingMenu) {
        existingMenu.remove();
        return;
    }

    // Create dropdown menu
    const menu = document.createElement('div');
    menu.className = 'user-dropdown';
    menu.style.cssText = `
        position: absolute;
        top: 100%;
        right: 0;
        background: #1a1a1b;
        border: 1px solid #343536;
        border-radius: 4px;
        box-shadow: 0 2px 8px rgba(0,0,0,0.5);
        z-index: 1000;
        min-width: 200px;
        margin-top: 4px;
    `;

    const menuItems = [
        { text: 'My Profile', icon: '👤' },
        { text: 'User Settings', icon: '⚙️' },
        { text: 'Create Community', icon: '➕' },
        { text: 'Coins', icon: '🪙' },
        { text: 'Premium', icon: '⭐' },
        { text: 'Help', icon: '❓' },
        { text: 'Log Out', icon: '🚪' }
    ];

    menuItems.forEach((item, index) => {
        const menuItem = document.createElement('div');
        menuItem.innerHTML = `${item.icon} ${item.text}`;
        menuItem.style.cssText = `
            padding: 12px 16px;
            cursor: pointer;
            color: #d7dadc;
            font-size: 14px;
            display: flex;
            align-items: center;
            gap: 8px;
            ${index < menuItems.length - 1 ? 'border-bottom: 1px solid #343536;' : ''}
        `;

        menuItem.addEventListener('mouseenter', function() {
            this.style.backgroundColor = '#272729';
        });

        menuItem.addEventListener('mouseleave', function() {
            this.style.backgroundColor = 'transparent';
        });

        menuItem.addEventListener('click', function() {
            if (item.text === 'My Profile') {
                window.location.href = 'profile.html';
            } else {
                alert(`${item.text} functionality would be implemented here`);
            }
            menu.remove();
        });

        menu.appendChild(menuItem);
    });

    // Position menu relative to user menu
    element.style.position = 'relative';
    element.appendChild(menu);

    // Close menu when clicking outside
    setTimeout(() => {
        document.addEventListener('click', function closeMenu(e) {
            if (!element.contains(e.target)) {
                menu.remove();
                document.removeEventListener('click', closeMenu);
            }
        });
    }, 0);
}

// Show toast notification
function showToast(message) {
    const toast = document.createElement('div');
    toast.textContent = message;
    toast.style.cssText = `
        position: fixed;
        bottom: 20px;
        left: 50%;
        transform: translateX(-50%);
        background: #0079d3;
        color: white;
        padding: 12px 24px;
        border-radius: 4px;
        font-size: 14px;
        font-weight: 500;
        z-index: 10000;
        opacity: 0;
        transition: opacity 0.3s ease;
    `;

    document.body.appendChild(toast);

    // Show toast
    setTimeout(() => {
        toast.style.opacity = '1';
    }, 100);

    // Hide toast
    setTimeout(() => {
        toast.style.opacity = '0';
        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 300);
    }, 3000);
}