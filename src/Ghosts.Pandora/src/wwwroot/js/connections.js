(function () {
    'use strict';

    const STYLE_ID = 'connections-style';

    function ensureStyles() {
        if (document.getElementById(STYLE_ID)) {
            return;
        }

        const style = document.createElement('style');
        style.id = STYLE_ID;
        style.textContent = `
            .connections-wrapper,
            .connections-columns {
                display: grid;
                gap: 16px;
                grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
            }

            .connections-group,
            .connections-column {
                background: rgba(255,255,255,0.85);
                border-radius: 12px;
                padding: 16px;
                box-shadow: 0 2px 4px rgba(15, 23, 42, 0.08);
            }

            .connections-group h3,
            .connections-column h3 {
                margin: 0 0 12px;
                font-size: 15px;
                font-weight: 600;
            }

            .connection-list,
            .connections-scroll {
                display: flex;
                flex-direction: column;
                gap: 10px;
                max-height: 240px;
                overflow-y: auto;
            }

            .connection-item {
                display: flex;
                align-items: center;
                gap: 12px;
                text-decoration: none;
                color: inherit;
                padding: 6px 8px;
                border-radius: 10px;
                transition: background-color 0.15s ease;
            }

            .connection-item:hover {
                background: rgba(56, 189, 248, 0.15);
            }

            .connection-avatar {
                width: 40px;
                height: 40px;
                border-radius: 50%;
                object-fit: cover;
            }

            .connection-meta {
                display: flex;
                flex-direction: column;
                gap: 2px;
            }

            .connection-name {
                font-weight: 600;
            }

            .connection-bio {
                font-size: 12px;
                color: #64748b;
            }

            .connection-empty,
            .connection-error {
                font-size: 14px;
                color: #64748b;
            }
        `;

        document.head.appendChild(style);
    }

    function createCard(user) {
        const item = document.createElement('a');
        item.className = 'connection-item';
        item.href = `/u/${encodeURIComponent(user.username)}`;
        item.innerHTML = `
            <img src="${user.avatar}" alt="${user.username}" class="connection-avatar" />
            <div class="connection-meta">
                <span class="connection-name">${user.username}</span>
                ${user.bio ? `<span class="connection-bio">${user.bio}</span>` : ''}
            </div>`;
        return item;
    }

    function populateGroup(target, users, emptyState) {
        target.innerHTML = '';
        if (!users || users.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'connection-empty';
            empty.textContent = emptyState;
            target.appendChild(empty);
            return;
        }

        users.forEach(user => target.appendChild(createCard(user)));
    }

    function hydrate(container) {
        const username = container.dataset.username || container.getAttribute('data-profile-connections');
        if (!username) {
            return;
        }

        fetch(`/api/relationships/${encodeURIComponent(username)}/connections`)
            .then(resp => {
                if (!resp.ok) {
                    throw new Error('Failed to load connections');
                }
                return resp.json();
            })
            .then(data => {
                const followersTarget = container.querySelector('[data-followers-list]');
                const followingTarget = container.querySelector('[data-following-list]');
                const followerCounts = container.querySelectorAll('[data-followers-count]');
                const followingCounts = container.querySelectorAll('[data-following-count]');

                followerCounts.forEach(node => {
                    node.textContent = data.followers.length;
                });
                followingCounts.forEach(node => {
                    node.textContent = data.following.length;
                });

                if (followersTarget) {
                    populateGroup(followersTarget, data.followers, 'No followers yet');
                }
                if (followingTarget) {
                    populateGroup(followingTarget, data.following, 'Not following anyone yet');
                }
            })
            .catch(() => {
                const error = document.createElement('div');
                error.className = 'connection-error';
                error.textContent = 'Unable to load connections right now.';
                container.appendChild(error);
            });
    }

    function refresh(username) {
        if (!username) {
            return;
        }
        document
            .querySelectorAll(`[data-profile-connections][data-username="${username}"]`)
            .forEach(hydrate);
    }

    document.addEventListener('DOMContentLoaded', () => {
        ensureStyles();
        document.querySelectorAll('[data-profile-connections]').forEach(hydrate);
    });

    document.addEventListener('submit', event => {
        const form = event.target.closest('.follow-action-form');
        if (!form) {
            return;
        }

        event.preventDefault();

        const button = form.querySelector('button');
        const action = form.getAttribute('action');
        const targetUsername = form.dataset.followUsername;

        if (!action || !targetUsername || !button) {
            return;
        }

        const originalLabel = button.textContent;
        button.disabled = true;

        const formData = new FormData(form);

        fetch(action, {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        })
            .then(resp => resp.ok ? resp.json() : Promise.reject())
            .then(() => {
                const isCurrentlyFollowing = action === '/unfollow';
                const nextAction = isCurrentlyFollowing ? '/follow' : '/unfollow';
                const nextLabel = isCurrentlyFollowing ? 'Follow' : 'Unfollow';

                form.setAttribute('action', nextAction);
                form.dataset.following = (!isCurrentlyFollowing).toString();
                button.textContent = nextLabel;

                refresh(targetUsername);
            })
            .catch(() => {
                button.textContent = 'Try again';
            })
            .finally(() => {
                button.disabled = false;
                if (button.textContent === 'Try again') {
                    button.textContent = originalLabel;
                }
            });
    });

    window.refreshConnections = refresh;
})();
