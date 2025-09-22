// X.com Feed JavaScript

document.addEventListener('DOMContentLoaded', function() {
    // Compose tweet functionality
    const composeTextarea = document.querySelector('.compose-textarea');
    const postBtn = document.querySelector('.post-btn');

    if (composeTextarea && postBtn) {
        composeTextarea.addEventListener('input', function() {
            postBtn.disabled = this.value.trim().length === 0;
        });
    }

    // Tweet actions
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
            if (!e.target.closest('.action-btn')) {
                window.location.href = 'post.html';
            }
        });
    });

    // Tab switching
    const tabs = document.querySelectorAll('.tab');
    tabs.forEach(tab => {
        tab.addEventListener('click', function() {
            tabs.forEach(t => t.classList.remove('active'));
            this.classList.add('active');
        });
    });

    // Follow buttons
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

// Infinite scroll simulation
let isLoading = false;

window.addEventListener('scroll', function() {
    if (isLoading) return;

    const { scrollTop, scrollHeight, clientHeight } = document.documentElement;

    if (scrollTop + clientHeight >= scrollHeight - 1000) {
        isLoading = true;

        // Simulate loading more tweets
        setTimeout(() => {
            const feed = document.querySelector('.feed');
            if (feed) {
                const newTweet = createMockTweet();
                feed.appendChild(newTweet);
            }
            isLoading = false;
        }, 1000);
    }
});

function createMockTweet() {
    const tweetHTML = `
        <div class="tweet">
            <img src="images/user1-avatar.jpg" alt="User" class="tweet-avatar">
            <div class="tweet-content">
                <div class="tweet-header">
                    <span class="tweet-name">Mock User</span>
                    <span class="tweet-handle">@mockuser</span>
                    <span class="tweet-time">· now</span>
                    <span class="tweet-more">⋯</span>
                </div>
                <div class="tweet-text">
                    This is a dynamically loaded tweet to demonstrate infinite scroll functionality! 🎉
                </div>
                <div class="tweet-actions">
                    <button class="action-btn">
                        <span class="action-icon">💬</span>
                        <span class="action-count">0</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">🔄</span>
                        <span class="action-count">0</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">❤️</span>
                        <span class="action-count">0</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">📊</span>
                        <span class="action-count">100</span>
                    </button>
                    <button class="action-btn">
                        <span class="action-icon">📤</span>
                    </button>
                </div>
            </div>
        </div>
    `;

    const div = document.createElement('div');
    div.innerHTML = tweetHTML;
    return div.firstElementChild;
}