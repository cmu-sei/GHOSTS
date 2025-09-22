// Instagram Feed JavaScript
document.addEventListener('DOMContentLoaded', function() {
    // Like button functionality
    const likeButtons = document.querySelectorAll('.like-btn');
    likeButtons.forEach(button => {
        button.addEventListener('click', function() {
            this.classList.toggle('liked');
            this.style.color = this.classList.contains('liked') ? '#ed4956' : '#262626';
        });
    });

    // Follow button functionality
    const followButtons = document.querySelectorAll('.follow-btn');
    followButtons.forEach(button => {
        button.addEventListener('click', function() {
            if (this.textContent === 'Follow') {
                this.textContent = 'Following';
                this.style.color = '#262626';
            } else {
                this.textContent = 'Follow';
                this.style.color = '#0095f6';
            }
        });
    });

    // Story click functionality
    const storyItems = document.querySelectorAll('.story-item');
    storyItems.forEach(story => {
        story.addEventListener('click', function() {
            const username = this.querySelector('.story-username').textContent;
            alert(`Opening story for ${username}`);
        });
    });

    // Comment functionality
    const commentInputs = document.querySelectorAll('.comment-input');
    commentInputs.forEach(input => {
        const postButton = input.nextElementSibling;

        input.addEventListener('input', function() {
            if (this.value.trim()) {
                postButton.style.opacity = '1';
                postButton.style.pointerEvents = 'auto';
            } else {
                postButton.style.opacity = '0.3';
                postButton.style.pointerEvents = 'none';
            }
        });

        postButton.addEventListener('click', function() {
            if (input.value.trim()) {
                console.log('Posting comment:', input.value);
                input.value = '';
                this.style.opacity = '0.3';
                this.style.pointerEvents = 'none';
            }
        });
    });
});