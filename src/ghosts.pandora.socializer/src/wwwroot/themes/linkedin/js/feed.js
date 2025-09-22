// LinkedIn Feed JavaScript
document.addEventListener('DOMContentLoaded', function() {
    // Post actions
    const postActions = document.querySelectorAll('.post-action');
    postActions.forEach(action => {
        action.addEventListener('click', function() {
            const actionText = this.querySelector('.action-text').textContent;

            if (actionText === 'Like') {
                this.classList.toggle('liked');
                this.style.color = this.classList.contains('liked') ? '#0a66c2' : '#666666';

                // Update reaction count
                const post = this.closest('.post');
                const reactions = post.querySelector('.reactions');
                if (reactions) {
                    let count = parseInt(reactions.textContent.match(/\d+/)[0]);
                    reactions.textContent = reactions.textContent.replace(/\d+/, this.classList.contains('liked') ? count + 1 : count - 1);
                }
            } else if (actionText === 'Comment') {
                alert('Comment functionality would open here');
            } else if (actionText === 'Repost') {
                alert('Repost options would open here');
            } else if (actionText === 'Send') {
                alert('Send message functionality would open here');
            }
        });
    });

    // Connect buttons
    const connectButtons = document.querySelectorAll('.connect-btn');
    connectButtons.forEach(button => {
        button.addEventListener('click', function() {
            if (this.textContent === 'Connect') {
                this.textContent = 'Pending';
                this.style.backgroundColor = '#f3f2ef';
                this.style.color = '#666666';
                this.style.borderColor = '#e6e6e6';
            }
        });
    });

    // Start post button
    const startPostBtn = document.querySelector('.start-post-btn');
    if (startPostBtn) {
        startPostBtn.addEventListener('click', function() {
            alert('Post composer would open here');
        });
    }

    // Composer actions
    const composerActions = document.querySelectorAll('.composer-action');
    composerActions.forEach(action => {
        action.addEventListener('click', function() {
            const actionText = this.querySelector('.action-text').textContent;
            alert(`${actionText} functionality would open here`);
        });
    });

    // Navigation
    const navItems = document.querySelectorAll('.nav-item');
    navItems.forEach(item => {
        item.addEventListener('click', function() {
            navItems.forEach(nav => nav.classList.remove('active'));
            this.classList.add('active');
        });
    });

    // News items
    const newsItems = document.querySelectorAll('.news-item');
    newsItems.forEach(item => {
        item.addEventListener('click', function() {
            const title = this.querySelector('.news-title').textContent;
            alert(`Opening news article: ${title}`);
        });
    });
});