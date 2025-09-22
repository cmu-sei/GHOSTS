// X.com Post JavaScript

document.addEventListener('DOMContentLoaded', function() {
    // Back button functionality
    const backBtn = document.querySelector('.back-btn');
    if (backBtn) {
        backBtn.addEventListener('click', function() {
            window.history.back();
        });
    }

    // Reply textarea functionality
    const replyTextarea = document.querySelector('.reply-textarea');
    const replyBtn = document.querySelector('.reply-btn');

    if (replyTextarea && replyBtn) {
        replyTextarea.addEventListener('input', function() {
            replyBtn.disabled = this.value.trim().length === 0;
        });

        replyBtn.addEventListener('click', function() {
            if (!this.disabled) {
                const replyText = replyTextarea.value.trim();
                if (replyText) {
                    addNewReply(replyText);
                    replyTextarea.value = '';
                    this.disabled = true;
                }
            }
        });
    }

    // Main post actions
    const postActions = document.querySelectorAll('.post-actions .action-btn');
    postActions.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            const icon = this.querySelector('.action-icon');

            if (icon) {
                const iconText = icon.textContent;

                switch (iconText) {
                    case '💬':
                        // Focus on reply textarea
                        if (replyTextarea) {
                            replyTextarea.focus();
                        }
                        break;
                    case '🔄':
                        // Toggle repost
                        this.classList.toggle('reposted');
                        updatePostStats('repost');
                        break;
                    case '❤️':
                        // Toggle like
                        this.classList.toggle('liked');
                        updatePostStats('like');
                        break;
                    case '🔖':
                        // Toggle bookmark
                        this.classList.toggle('bookmarked');
                        updatePostStats('bookmark');
                        break;
                    case '📤':
                        // Share functionality
                        if (navigator.share) {
                            navigator.share({
                                title: 'Check out this post',
                                url: window.location.href
                            });
                        } else {
                            // Fallback - copy to clipboard
                            navigator.clipboard.writeText(window.location.href);
                            showToast('Link copied to clipboard!');
                        }
                        break;
                }
            }
        });
    });

    // Reply actions
    const replyActions = document.querySelectorAll('.reply-actions .action-btn');
    replyActions.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            const icon = this.querySelector('.action-icon');
            const count = this.querySelector('.action-count');

            if (icon) {
                // Like button
                if (icon.textContent === '❤️') {
                    const isLiked = this.classList.contains('liked');
                    this.classList.toggle('liked');

                    if (count) {
                        let currentCount = parseInt(count.textContent.replace(/[^\d]/g, '')) || 0;
                        if (isLiked) {
                            currentCount--;
                        } else {
                            currentCount++;
                        }
                        count.textContent = formatCount(currentCount);
                    }
                }

                // Repost button
                if (icon.textContent === '🔄') {
                    const isReposted = this.classList.contains('reposted');
                    this.classList.toggle('reposted');

                    if (count) {
                        let currentCount = parseInt(count.textContent.replace(/[^\d]/g, '')) || 0;
                        if (isReposted) {
                            currentCount--;
                        } else {
                            currentCount++;
                        }
                        count.textContent = formatCount(currentCount);
                    }
                }

                // Reply to reply
                if (icon.textContent === '💬') {
                    const replyElement = this.closest('.reply');
                    const handle = replyElement.querySelector('.reply-handle').textContent;

                    if (replyTextarea) {
                        replyTextarea.value = `${handle} `;
                        replyTextarea.focus();
                        replyTextarea.setSelectionRange(replyTextarea.value.length, replyTextarea.value.length);
                    }
                }
            }
        });
    });

    // Follow button functionality
    const followButtons = document.querySelectorAll('.follow-btn');
    followButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            if (this.textContent === 'Follow') {
                this.textContent = 'Following';
                this.style.backgroundColor = '#16181c';
                this.style.color = '#e7e9ea';
                this.style.borderColor = '#536471';
            } else {
                this.textContent = 'Follow';
                this.style.backgroundColor = '#e7e9ea';
                this.style.color = '#0f1419';
                this.style.borderColor = 'transparent';
            }
        });
    });

    // Post stats click functionality
    const statItems = document.querySelectorAll('.post-stats .stat-item');
    statItems.forEach(item => {
        item.addEventListener('click', function() {
            const label = this.querySelector('.stat-label').textContent;
            alert(`${label} modal would open here showing detailed information`);
        });
    });

    // More options buttons
    const moreButtons = document.querySelectorAll('.post-more, .reply-more');
    moreButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();
            showOptionsMenu(this);
        });
    });

    // Search functionality
    const searchInput = document.querySelector('.search-bar input');
    if (searchInput) {
        searchInput.addEventListener('focus', function() {
            this.style.backgroundColor = '#000';
        });

        searchInput.addEventListener('blur', function() {
            if (!this.value) {
                this.style.backgroundColor = '#202327';
            }
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

// Update post stats
function updatePostStats(action) {
    const statsContainer = document.querySelector('.post-stats');
    if (!statsContainer) return;

    const statItems = statsContainer.querySelectorAll('.stat-item');

    statItems.forEach(item => {
        const label = item.querySelector('.stat-label').textContent.toLowerCase();
        const number = item.querySelector('.stat-number');

        if ((action === 'like' && label.includes('like')) ||
            (action === 'repost' && label.includes('repost')) ||
            (action === 'bookmark' && label.includes('bookmark'))) {

            let currentCount = parseInt(number.textContent.replace(/[^\d]/g, '')) || 0;
            currentCount++;
            number.textContent = formatCount(currentCount);
        }
    });
}

// Add new reply
function addNewReply(replyText) {
    const repliesContainer = document.querySelector('.replies');
    if (!repliesContainer) return;

    const newReplyHTML = `
        <div class="reply">
            <img src="images/profile-avatar.jpg" alt="User" class="reply-avatar">
            <div class="reply-content">
                <div class="reply-header">
                    <span class="reply-name">John Doe</span>
                    <span class="reply-handle">@johndoe</span>
                    <span class="reply-time">· now</span>
                    <span class="reply-more">⋯</span>
                </div>
                <div class="reply-text">
                    ${replyText}
                </div>
                <div class="reply-actions">
                    <button class="action-btn">
                        <span class="action-icon">💬</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">🔄</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">❤️</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">📤</span>
                    </button>
                </div>
            </div>
        </div>
    `;

    const div = document.createElement('div');
    div.innerHTML = newReplyHTML;
    const newReply = div.firstElementChild;

    // Insert at the top of replies
    repliesContainer.insertBefore(newReply, repliesContainer.firstChild);

    // Add event listeners to the new reply
    addReplyEventListeners(newReply);

    // Show success message
    showToast('Reply posted!');
}

// Add event listeners to a reply element
function addReplyEventListeners(replyElement) {
    const actionButtons = replyElement.querySelectorAll('.action-btn');
    actionButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            const icon = this.querySelector('.action-icon');
            if (icon && icon.textContent === '💬') {
                const handle = replyElement.querySelector('.reply-handle').textContent;
                const replyTextarea = document.querySelector('.reply-textarea');

                if (replyTextarea) {
                    replyTextarea.value = `${handle} `;
                    replyTextarea.focus();
                }
            }
        });
    });
}

// Show options menu
function showOptionsMenu(button) {
    // Remove existing menu
    const existingMenu = document.querySelector('.options-menu');
    if (existingMenu) {
        existingMenu.remove();
    }

    // Create options menu
    const menu = document.createElement('div');
    menu.className = 'options-menu';
    menu.style.cssText = `
        position: absolute;
        background: #16181c;
        border: 1px solid #2f3336;
        border-radius: 8px;
        box-shadow: 0 2px 8px rgba(0,0,0,0.5);
        z-index: 1000;
        min-width: 200px;
    `;

    const options = [
        'Copy link to post',
        'Embed post',
        'Report post',
        'Block user',
        'Mute user'
    ];

    options.forEach(option => {
        const item = document.createElement('div');
        item.textContent = option;
        item.style.cssText = `
            padding: 12px 16px;
            cursor: pointer;
            color: #e7e9ea;
            font-size: 15px;
        `;

        item.addEventListener('mouseenter', function() {
            this.style.backgroundColor = 'rgba(231, 233, 234, 0.03)';
        });

        item.addEventListener('mouseleave', function() {
            this.style.backgroundColor = 'transparent';
        });

        item.addEventListener('click', function() {
            alert(`${option} functionality would be implemented here`);
            menu.remove();
        });

        menu.appendChild(item);
    });

    // Position menu
    const rect = button.getBoundingClientRect();
    menu.style.top = `${rect.bottom + 5}px`;
    menu.style.left = `${rect.left}px`;

    document.body.appendChild(menu);

    // Close menu when clicking outside
    setTimeout(() => {
        document.addEventListener('click', function closeMenu(e) {
            if (!menu.contains(e.target)) {
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
        background: #1d9bf0;
        color: white;
        padding: 12px 24px;
        border-radius: 20px;
        font-size: 15px;
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