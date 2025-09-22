// X.com Profile JavaScript

document.addEventListener('DOMContentLoaded', function() {
    // Back button functionality
    const backBtn = document.querySelector('.back-btn');
    if (backBtn) {
        backBtn.addEventListener('click', function() {
            window.history.back();
        });
    }

    // Profile tabs
    const tabs = document.querySelectorAll('.profile-tabs .tab');
    tabs.forEach(tab => {
        tab.addEventListener('click', function() {
            tabs.forEach(t => t.classList.remove('active'));
            this.classList.add('active');

            // Simulate loading different content based on tab
            const tabName = this.textContent.toLowerCase();
            loadTabContent(tabName);
        });
    });

    // Edit profile button
    const editProfileBtn = document.querySelector('.edit-profile-btn');
    if (editProfileBtn) {
        editProfileBtn.addEventListener('click', function() {
            alert('Edit profile functionality would open here');
        });
    }

    // Follow/Following stats
    const statItems = document.querySelectorAll('.stat-item');
    statItems.forEach(item => {
        item.addEventListener('click', function(e) {
            e.preventDefault();
            const label = this.querySelector('.stat-label').textContent;
            alert(`${label} modal would open here`);
        });
    });

    // Tweet actions in profile
    const actionButtons = document.querySelectorAll('.action-btn');
    actionButtons.forEach(button => {
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
                            icon.textContent = '🤍';
                        } else {
                            currentCount++;
                            icon.textContent = '❤️';
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
            }
        });
    });

    // Tweet click to navigate
    const tweets = document.querySelectorAll('.tweet');
    tweets.forEach(tweet => {
        tweet.addEventListener('click', function(e) {
            if (!e.target.closest('.action-btn') && !e.target.closest('.pin-indicator')) {
                window.location.href = 'post.html';
            }
        });
    });

    // Follow buttons in sidebar
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

    // Profile action buttons
    const profileActionBtns = document.querySelectorAll('.profile-actions .action-btn');
    profileActionBtns.forEach(button => {
        button.addEventListener('click', function(e) {
            e.stopPropagation();

            const icon = this.textContent;
            if (icon === '⋯') {
                alert('Profile options menu would open here');
            } else if (icon === '✉️') {
                alert('Message functionality would open here');
            }
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

// Load different content based on tab selection
function loadTabContent(tabName) {
    const postsContainer = document.querySelector('.posts-feed');
    if (!postsContainer) return;

    // Clear existing content except pinned post for Posts tab
    if (tabName === 'posts') {
        // Keep pinned post, reload other posts
        const pinnedPost = postsContainer.querySelector('.tweet.pinned');
        postsContainer.innerHTML = '';
        if (pinnedPost) {
            postsContainer.appendChild(pinnedPost);
        }
        loadMorePosts();
    } else {
        // Clear all and show placeholder for other tabs
        postsContainer.innerHTML = `
            <div style="padding: 40px; text-align: center; color: #71767b;">
                <h3>${tabName.charAt(0).toUpperCase() + tabName.slice(1)} content would appear here</h3>
                <p>This is where ${tabName} would be displayed</p>
            </div>
        `;
    }
}

// Load more posts for infinite scroll
function loadMorePosts() {
    const postsContainer = document.querySelector('.posts-feed');
    if (!postsContainer) return;

    // Add some mock posts
    const mockPosts = [
        {
            name: 'John Doe',
            handle: '@johndoe',
            time: '5h',
            text: 'Working late tonight on some exciting new features. Can\'t wait to share what we\'re building! 💻✨'
        },
        {
            name: 'John Doe',
            handle: '@johndoe',
            time: '1d',
            text: 'Great discussion at today\'s tech meetup about the future of web development. So many brilliant minds in one room! 🧠'
        },
        {
            name: 'John Doe',
            handle: '@johndoe',
            time: '2d',
            text: 'Nothing beats the feeling of solving a complex bug that\'s been bothering you for days. Time for a well-deserved coffee break ☕️'
        }
    ];

    mockPosts.forEach(post => {
        const postElement = createPostElement(post);
        postsContainer.appendChild(postElement);
    });
}

function createPostElement(post) {
    const postHTML = `
        <div class="tweet">
            <img src="images/profile-avatar.jpg" alt="User" class="tweet-avatar">
            <div class="tweet-content">
                <div class="tweet-header">
                    <span class="tweet-name">${post.name}</span>
                    <span class="tweet-handle">${post.handle}</span>
                    <span class="tweet-time">· ${post.time}</span>
                    <span class="tweet-more">⋯</span>
                </div>
                <div class="tweet-text">
                    ${post.text}
                </div>
                <div class="tweet-actions">
                    <button class="action-btn">
                        <span class="action-icon">💬</span>
                        <span class="action-count">${Math.floor(Math.random() * 50)}</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">🔄</span>
                        <span class="action-count">${Math.floor(Math.random() * 100)}</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">❤️</span>
                        <span class="action-count">${Math.floor(Math.random() * 200)}</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">📊</span>
                        <span class="action-count">${Math.floor(Math.random() * 1000)}</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">📤</span>
                    </button>
                </div>
            </div>
        </div>
    `;

    const div = document.createElement('div');
    div.innerHTML = postHTML;
    return div.firstElementChild;
}