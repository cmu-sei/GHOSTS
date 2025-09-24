// Facebook Template JavaScript

document.addEventListener('DOMContentLoaded', function() {
    initializeFacebookFeatures();
});

function initializeFacebookFeatures() {
    initializeLikeButtons();
    initializeShareButtons();
    initializePostMenus();
    initializeSearch();
    initializeProfilePictureHovers();
    initializeImageLightbox();
}

// Like Button Functionality
function initializeLikeButtons() {
    const likeButtons = document.querySelectorAll('.like-btn');

    likeButtons.forEach(button => {
        button.addEventListener('click', function() {
            const isLiked = this.classList.contains('liked');
            const post = this.closest('.post');
            const postStats = post.querySelector('.post-stats span:first-child');

            if (isLiked) {
                this.classList.remove('liked');
                this.innerHTML = '👍 Like';
                updateLikeCount(postStats, -1);
            } else {
                this.classList.add('liked');
                this.innerHTML = '👍 Liked';
                updateLikeCount(postStats, 1);

                // Add subtle animation
                this.style.transform = 'scale(1.1)';
                setTimeout(() => {
                    this.style.transform = 'scale(1)';
                }, 150);
            }
        });
    });
}

function updateLikeCount(statsElement, change) {
    const text = statsElement.textContent;
    const numberMatch = text.match(/(\d+)/);
    if (numberMatch) {
        const currentCount = parseInt(numberMatch[1]);
        const newCount = currentCount + change;
        statsElement.textContent = text.replace(/\d+/, newCount);
    }
}

// Share Button Functionality
function initializeShareButtons() {
    const shareButtons = document.querySelectorAll('.share-btn');

    shareButtons.forEach(button => {
        button.addEventListener('click', function() {
            const post = this.closest('.post');
            const postText = post.querySelector('.post-text')?.textContent || '';

            // Simple share simulation
            if (navigator.share) {
                navigator.share({
                    title: 'Facebook Post',
                    text: postText.substring(0, 100) + '...',
                    url: window.location.href
                });
            } else {
                // Fallback: copy to clipboard
                const textToCopy = `${postText.substring(0, 100)}... - ${window.location.href}`;
                navigator.clipboard.writeText(textToCopy).then(() => {
                    showToast('Link copied to clipboard!');
                });
            }
        });
    });
}

// Post Menu Functionality
function initializePostMenus() {
    const postMenus = document.querySelectorAll('.post-menu');

    postMenus.forEach(menu => {
        menu.addEventListener('click', function(e) {
            e.stopPropagation();
            showPostMenu(this);
        });
    });

    // Close menu when clicking outside
    document.addEventListener('click', function() {
        const existingMenu = document.querySelector('.post-dropdown-menu');
        if (existingMenu) {
            existingMenu.remove();
        }
    });
}

function showPostMenu(button) {
    // Remove existing menu
    const existingMenu = document.querySelector('.post-dropdown-menu');
    if (existingMenu) {
        existingMenu.remove();
    }

    // Create menu
    const menu = document.createElement('div');
    menu.className = 'post-dropdown-menu';
    menu.style.cssText = `
        position: absolute;
        background: white;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        padding: 8px 0;
        z-index: 1000;
        min-width: 200px;
        top: ${button.offsetTop + 30}px;
        right: 16px;
    `;

    menu.innerHTML = `
        <div class="menu-item" style="padding: 8px 16px; cursor: pointer; hover:background-color: #f0f2f5;">Save post</div>
        <div class="menu-item" style="padding: 8px 16px; cursor: pointer; hover:background-color: #f0f2f5;">Hide post</div>
        <div class="menu-item" style="padding: 8px 16px; cursor: pointer; hover:background-color: #f0f2f5;">Report post</div>
    `;

    button.closest('.post').style.position = 'relative';
    button.closest('.post').appendChild(menu);

    // Add hover effects
    menu.querySelectorAll('.menu-item').forEach(item => {
        item.addEventListener('mouseenter', function() {
            this.style.backgroundColor = '#f0f2f5';
        });
        item.addEventListener('mouseleave', function() {
            this.style.backgroundColor = 'transparent';
        });
        item.addEventListener('click', function() {
            showToast(`${this.textContent} clicked`);
            menu.remove();
        });
    });
}


// Search Functionality
function initializeSearch() {
    const searchBar = document.querySelector('.search-bar');

    if (searchBar) {
        searchBar.addEventListener('input', function() {
            const query = this.value.trim();
            if (query.length > 2) {
                // Simulate search suggestions
                showSearchSuggestions(query, this);
            } else {
                hideSearchSuggestions();
            }
        });

        searchBar.addEventListener('keypress', function(e) {
            if (e.key === 'Enter') {
                const query = this.value.trim();
                if (query) {
                    showToast(`Searching for: ${query}`);
                }
            }
        });
    }
}

function showSearchSuggestions(query, input) {
    hideSearchSuggestions();

    const suggestions = [
        'Sarah Wilson',
        'Mike Johnson',
        'Emma Davis',
        'Tech Company',
        'Beach Photos',
        'Marathon Training'
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
            showToast(`Selected: ${suggestion}`);
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

// Profile Picture Hover Effects
function initializeProfilePictureHovers() {
    const profilePics = document.querySelectorAll('.profile-pic, .profile-picture-large');

    profilePics.forEach(pic => {
        pic.addEventListener('mouseenter', function() {
            this.style.transform = 'scale(1.05)';
            this.style.transition = 'transform 0.2s ease';
        });

        pic.addEventListener('mouseleave', function() {
            this.style.transform = 'scale(1)';
        });
    });
}

// Image Lightbox
function initializeImageLightbox() {
    const postImages = document.querySelectorAll('.post-image');

    postImages.forEach(img => {
        img.addEventListener('click', function() {
            showImageLightbox(this.src, this.alt);
        });
    });
}

function showImageLightbox(src, alt) {
    const lightbox = document.createElement('div');
    lightbox.style.cssText = `
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0,0,0,0.9);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 10000;
    `;

    lightbox.innerHTML = `
        <img src="${src}" alt="${alt}" style="max-width: 90%; max-height: 90%; object-fit: contain;">
        <button onclick="this.closest('.lightbox').remove()" style="position: absolute; top: 20px; right: 20px; background: rgba(255,255,255,0.2); border: none; color: white; font-size: 24px; padding: 8px 12px; border-radius: 50%; cursor: pointer;">×</button>
    `;

    lightbox.className = 'lightbox';
    document.body.appendChild(lightbox);

    lightbox.addEventListener('click', function(e) {
        if (e.target === lightbox) {
            lightbox.remove();
        }
    });

    // ESC key to close
    document.addEventListener('keydown', function(e) {
        if (e.key === 'Escape' && document.querySelector('.lightbox')) {
            document.querySelector('.lightbox').remove();
        }
    });
}

// Comment Functionality
function initializeCommentInput() {
    const commentInputs = document.querySelectorAll('.comment-input input');

    commentInputs.forEach(input => {
        input.addEventListener('keypress', function(e) {
            if (e.key === 'Enter' && this.value.trim()) {
                addComment(this.value.trim());
                this.value = '';
            }
        });
    });
}

function addComment(text) {
    const commentsSection = document.querySelector('.comments-section');
    if (!commentsSection) return;

    const comment = document.createElement('div');
    comment.className = 'comment';
    comment.innerHTML = `
        <img src="images/profile-pic.jpg" alt="Your profile" class="profile-pic">
        <div>
            <div class="comment-content">
                <div class="comment-author">John Doe</div>
                <div class="comment-text">${text}</div>
            </div>
            <div class="comment-actions">
                <a href="#" class="comment-action">Like</a>
                <a href="#" class="comment-action">Reply</a>
                <span style="color: #65676b; font-size: 12px;">Just now</span>
            </div>
        </div>
    `;

    const commentInput = commentsSection.querySelector('.comment-input');
    commentsSection.insertBefore(comment, commentInput.nextSibling);

    showToast('Comment added!');
}

// Utility Functions
function showToast(message) {
    // Remove existing toast
    const existingToast = document.querySelector('.toast');
    if (existingToast) {
        existingToast.remove();
    }

    const toast = document.createElement('div');
    toast.className = 'toast';
    toast.style.cssText = `
        position: fixed;
        bottom: 20px;
        left: 50%;
        transform: translateX(-50%);
        background: #333;
        color: white;
        padding: 12px 24px;
        border-radius: 24px;
        z-index: 10000;
        animation: slideUp 0.3s ease;
    `;
    toast.textContent = message;

    document.body.appendChild(toast);

    setTimeout(() => {
        toast.remove();
    }, 3000);
}

// Add CSS animations
const style = document.createElement('style');
style.textContent = `
    @keyframes slideUp {
        from {
            opacity: 0;
            transform: translateX(-50%) translateY(20px);
        }
        to {
            opacity: 1;
            transform: translateX(-50%) translateY(0);
        }
    }
`;
document.head.appendChild(style);

// Initialize comment input if on post page
if (window.location.pathname.includes('post.html')) {
    document.addEventListener('DOMContentLoaded', function() {
        initializeCommentInput();
    });
}
