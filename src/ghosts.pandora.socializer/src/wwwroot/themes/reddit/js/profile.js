// Reddit Profile JavaScript

document.addEventListener('DOMContentLoaded', function() {
    // Profile tabs functionality
    const tabButtons = document.querySelectorAll('.tab-btn');
    tabButtons.forEach(tab => {
        tab.addEventListener('click', function() {
            tabButtons.forEach(t => t.classList.remove('active'));
            this.classList.add('active');

            const tabName = this.textContent.toLowerCase();
            loadTabContent(tabName);
        });
    });

    // Sort tabs functionality
    const sortTabs = document.querySelectorAll('.sort-tab');
    sortTabs.forEach(tab => {
        tab.addEventListener('click', function() {
            sortTabs.forEach(t => t.classList.remove('active'));
            this.classList.add('active');

            const sortType = this.textContent.toLowerCase().replace('🔥 ', '').replace('🆕 ', '').replace('⭐ ', '');
            loadSortedContent(sortType);
        });
    });

    // Follow button functionality
    const followBtn = document.querySelector('.follow-btn');
    if (followBtn) {
        followBtn.addEventListener('click', function() {
            if (this.textContent === 'Follow') {
                this.textContent = 'Following';
                this.style.backgroundColor = '#46d160';
                showToast('Now following u/johndoe');
            } else {
                this.textContent = 'Follow';
                this.style.backgroundColor = '#0079d3';
                showToast('Unfollowed u/johndoe');
            }
        });
    }

    // Chat button functionality
    const chatBtn = document.querySelector('.chat-btn');
    if (chatBtn) {
        chatBtn.addEventListener('click', function() {
            alert('Chat functionality would open here');
        });
    }

    // More button functionality
    const moreBtn = document.querySelector('.more-btn');
    if (moreBtn) {
        moreBtn.addEventListener('click', function() {
            showProfileOptionsMenu(this);
        });
    }

    // Vote functionality
    const voteButtons = document.querySelectorAll('.vote-btn');
    voteButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            const isUpvote = this.classList.contains('upvote');
            const isDownvote = this.classList.contains('downvote');
            const voteContainer = this.closest('.post-votes');
            const voteCount = voteContainer.querySelector('.vote-count');
            const upvoteBtn = voteContainer.querySelector('.upvote');
            const downvoteBtn = voteContainer.querySelector('.downvote');

            let currentCount = parseInt(voteCount.textContent.replace(/[^\d]/g, '')) || 0;

            // Reset both buttons
            upvoteBtn.classList.remove('upvoted');
            downvoteBtn.classList.remove('downvoted');

            if (isUpvote) {
                this.classList.add('upvoted');
                currentCount += 1;
            } else if (isDownvote) {
                this.classList.add('downvoted');
                currentCount -= 1;
            }

            voteCount.textContent = formatCount(currentCount);
        });
    });

    // Post click to navigate
    const posts = document.querySelectorAll('.post');
    posts.forEach(post => {
        post.addEventListener('click', function(e) {
            if (!e.target.closest('.vote-btn') && !e.target.closest('.action-btn')) {
                window.location.href = 'post.html';
            }
        });
    });

    // Action buttons
    const actionButtons = document.querySelectorAll('.action-btn');
    actionButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            const actionText = this.querySelector('.action-text').textContent;

            if (actionText.includes('Comments')) {
                window.location.href = 'post.html';
            } else if (actionText === 'Share') {
                if (navigator.share) {
                    navigator.share({
                        title: 'Check out this Reddit post',
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

    // Community item clicks
    const communityItems = document.querySelectorAll('.community-item');
    communityItems.forEach(item => {
        item.addEventListener('click', function() {
            const communityName = this.querySelector('.community-name').textContent;
            alert(`Navigate to ${communityName} would happen here`);
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

// Load different content based on tab selection
function loadTabContent(tabName) {
    const postsContainer = document.querySelector('.posts-feed');
    if (!postsContainer) return;

    if (tabName === 'posts') {
        // Keep existing posts
        return;
    }

    // Clear and show placeholder for other tabs
    postsContainer.innerHTML = `
        <div style="padding: 40px; text-align: center; color: #818384;">
            <h3>${tabName.charAt(0).toUpperCase() + tabName.slice(1)} content would appear here</h3>
            <p>This is where ${tabName} would be displayed</p>
        </div>
    `;
}

// Load different content based on sort type
function loadSortedContent(sortType) {
    const postsContainer = document.querySelector('.posts-feed');
    if (!postsContainer) return;

    // Add a loading indicator
    const loadingElement = document.createElement('div');
    loadingElement.textContent = `Loading ${sortType} posts...`;
    loadingElement.style.cssText = `
        text-align: center;
        padding: 40px;
        color: #818384;
        font-style: italic;
    `;

    postsContainer.appendChild(loadingElement);

    // Remove loading after 1 second
    setTimeout(() => {
        loadingElement.remove();
    }, 1000);
}

// Show profile options menu
function showProfileOptionsMenu(button) {
    // Remove existing menu
    const existingMenu = document.querySelector('.profile-options-menu');
    if (existingMenu) {
        existingMenu.remove();
        return;
    }

    // Create options menu
    const menu = document.createElement('div');
    menu.className = 'profile-options-menu';
    menu.style.cssText = `
        position: absolute;
        background: #1a1a1b;
        border: 1px solid #343536;
        border-radius: 4px;
        box-shadow: 0 2px 8px rgba(0,0,0,0.5);
        z-index: 1000;
        min-width: 200px;
        top: 100%;
        right: 0;
        margin-top: 4px;
    `;

    const options = [
        'Send a private message',
        'Block user',
        'Report user',
        'Copy link to profile'
    ];

    options.forEach((option, index) => {
        const item = document.createElement('div');
        item.textContent = option;
        item.style.cssText = `
            padding: 12px 16px;
            cursor: pointer;
            color: #d7dadc;
            font-size: 14px;
            ${index < options.length - 1 ? 'border-bottom: 1px solid #343536;' : ''}
        `;

        item.addEventListener('mouseenter', function() {
            this.style.backgroundColor = '#272729';
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
    button.style.position = 'relative';
    button.appendChild(menu);

    // Close menu when clicking outside
    setTimeout(() => {
        document.addEventListener('click', function closeMenu(e) {
            if (!button.contains(e.target)) {
                menu.remove();
                document.removeEventListener('click', closeMenu);
            }
        });
    }, 0);
}

// Show user menu (same as feed.js)
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