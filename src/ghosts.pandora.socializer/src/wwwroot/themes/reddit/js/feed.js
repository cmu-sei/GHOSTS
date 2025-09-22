// Reddit Feed JavaScript

document.addEventListener('DOMContentLoaded', function() {
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

    // Sort tab functionality
    const sortTabs = document.querySelectorAll('.sort-tab');
    sortTabs.forEach(tab => {
        tab.addEventListener('click', function() {
            sortTabs.forEach(t => t.classList.remove('active'));
            this.classList.add('active');

            // Simulate loading different content
            const sortType = this.textContent.toLowerCase().replace('🔥 ', '').replace('🆕 ', '').replace('⭐ ', '').replace('📈 ', '');
            loadSortedContent(sortType);
        });
    });

    // View options
    const viewButtons = document.querySelectorAll('.view-btn');
    viewButtons.forEach(button => {
        button.addEventListener('click', function() {
            viewButtons.forEach(b => b.classList.remove('active'));
            this.classList.add('active');
        });
    });

    // Join button functionality
    const joinButtons = document.querySelectorAll('.join-btn');
    joinButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            if (this.textContent === 'Join') {
                this.textContent = 'Joined';
                this.style.backgroundColor = '#46d160';
            } else {
                this.textContent = 'Join';
                this.style.backgroundColor = '#0079d3';
            }
        });
    });

    // Post click to navigate
    const posts = document.querySelectorAll('.post');
    posts.forEach(post => {
        post.addEventListener('click', function(e) {
            if (!e.target.closest('.vote-btn') && !e.target.closest('.action-btn') && !e.target.closest('.join-btn')) {
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

    // Create post functionality
    const createInput = document.querySelector('.create-input');
    if (createInput) {
        createInput.addEventListener('click', function() {
            alert('Create post functionality would open here');
        });
    }

    const createButtons = document.querySelectorAll('.create-btn');
    createButtons.forEach(button => {
        button.addEventListener('click', function() {
            const buttonText = this.textContent;
            let actionText = '';

            switch (buttonText) {
                case '📝':
                    actionText = 'Create text post';
                    break;
                case '📷':
                    actionText = 'Create image post';
                    break;
                case '🔗':
                    actionText = 'Create link post';
                    break;
                case '📊':
                    actionText = 'Create poll';
                    break;
            }

            alert(`${actionText} functionality would open here`);
        });
    });

    // User menu
    const userMenu = document.querySelector('.user-menu');
    if (userMenu) {
        userMenu.addEventListener('click', function() {
            showUserMenu(this);
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

// Load different content based on sort type
function loadSortedContent(sortType) {
    // Simulate loading different posts
    console.log(`Loading ${sortType} posts...`);

    // Add a loading indicator
    const postsContainer = document.querySelector('.posts-feed');
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

// Show user menu
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

// Infinite scroll simulation
let isLoading = false;

window.addEventListener('scroll', function() {
    if (isLoading) return;

    const { scrollTop, scrollHeight, clientHeight } = document.documentElement;

    if (scrollTop + clientHeight >= scrollHeight - 1000) {
        isLoading = true;

        // Simulate loading more posts
        setTimeout(() => {
            const feed = document.querySelector('.posts-feed');
            if (feed) {
                const newPost = createMockPost();
                feed.appendChild(newPost);
            }
            isLoading = false;
        }, 1000);
    }
});

function createMockPost() {
    const postHTML = `
        <div class="post">
            <div class="post-votes">
                <button class="vote-btn upvote">▲</button>
                <span class="vote-count">${Math.floor(Math.random() * 1000)}</span>
                <button class="vote-btn downvote">▼</button>
            </div>
            <div class="post-content">
                <div class="post-header">
                    <img src="images/askreddit-icon.jpg" alt="subreddit" class="subreddit-icon">
                    <span class="subreddit-name">r/AskReddit</span>
                    <span class="post-meta">
                        • Posted by u/random_user
                        <span class="post-time">just now</span>
                    </span>
                </div>
                <h2 class="post-title">What's something interesting you learned today?</h2>
                <div class="post-text">
                    This is a dynamically loaded post to demonstrate infinite scroll functionality! Share your interesting discoveries.
                </div>
                <div class="post-actions">
                    <button class="action-btn">
                        <span class="action-icon">💬</span>
                        <span class="action-text">0 Comments</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">🔗</span>
                        <span class="action-text">Share</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">💾</span>
                        <span class="action-text">Save</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">⋯</span>
                    </button>
                </div>
            </div>
        </div>
    `;

    const div = document.createElement('div');
    div.innerHTML = postHTML;
    return div.firstElementChild;
}