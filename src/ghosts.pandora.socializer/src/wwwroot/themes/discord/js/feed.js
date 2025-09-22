// Discord Feed JavaScript
document.addEventListener('DOMContentLoaded', function() {
    // Server switching
    const serverItems = document.querySelectorAll('.server-item');
    serverItems.forEach(item => {
        item.addEventListener('click', function() {
            serverItems.forEach(s => s.classList.remove('active'));
            this.classList.add('active');
        });
    });

    // Channel switching
    const channelItems = document.querySelectorAll('.channel-item');
    channelItems.forEach(item => {
        item.addEventListener('click', function() {
            channelItems.forEach(c => c.classList.remove('active'));
            this.classList.add('active');
        });
    });

    // Message reactions
    const reactions = document.querySelectorAll('.reaction');
    reactions.forEach(reaction => {
        reaction.addEventListener('click', function() {
            const count = this.querySelector('.reaction-count');
            let currentCount = parseInt(count.textContent);
            count.textContent = currentCount + 1;
        });
    });

    // Message input
    const messageInput = document.querySelector('.message-input');
    if (messageInput) {
        messageInput.addEventListener('keypress', function(e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                // Send message logic here
                console.log('Sending message:', this.value);
                this.value = '';
            }
        });
    }

    // Member list interactions
    const memberItems = document.querySelectorAll('.member-item');
    memberItems.forEach(item => {
        item.addEventListener('click', function() {
            const memberName = this.querySelector('.member-name').textContent;
            alert(`Opening DM with ${memberName}`);
        });
    });
});