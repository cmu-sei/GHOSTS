// Facebook theme behaviour: header menus, the composer, likes, feed comments, follow buttons and Messenger.
// Posting, liking and commenting go to the same endpoints and fields as before; social.js still sends likes.
(function () {
    'use strict';

    const names = window.FB_NAMES || {};

    function escapeText(value) {
        return String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
    }

    function compact(n) {
        if (n >= 1000000) return (n / 1000000).toFixed(n >= 10000000 ? 0 : 1).replace(/\.0$/, '') + 'M';
        if (n >= 1000) return (n / 1000).toFixed(n >= 10000 ? 0 : 1).replace(/\.0$/, '') + 'K';
        return String(n);
    }

    // The name shown for a username, as SocialPresentation.DisplayName gives it on the server.
    // Takes and returns HTML-escaped text; social.js uses it in the live post template.
    window.fbDisplayName = function (user) {
        const raw = String(user ?? '');
        const known = names[raw.toLowerCase()];
        if (known) return escapeText(known);
        const words = raw.split(/[._-]+|(?<=[a-z])(?=[0-9])/).filter(w => w && !/^\d+$/.test(w));
        return words.length ? words.map(w => w[0].toUpperCase() + w.slice(1)).join(' ') : raw;
    };

    function send(form) {
        return fetch(form.action, { method: 'POST', body: new URLSearchParams(new FormData(form)), credentials: 'same-origin' })
            .then(response => {
                if (!response.ok) throw new Error('HTTP ' + response.status);
                return response;
            });
    }

    // ---------- clicks ----------

    document.addEventListener('click', function (e) {
        const target = e.target;

        // header menus
        const toggle = target.closest('[data-fb-menu-toggle]');
        if (!toggle && target.closest('.fb-dropdown') && !target.closest('a[href]:not([href="#"])')) {
            const chip = target.closest('[data-fb-note-filter]');
            if (chip) filterNotes(chip);
            return;
        }
        document.querySelectorAll('[data-fb-menu].open').forEach(menu => {
            if (!toggle || !menu.contains(toggle)) menu.classList.remove('open');
        });
        if (toggle) {
            toggle.closest('[data-fb-menu]').classList.toggle('open');
            return;
        }

        // links the real site would open in a dialog or another app stay put
        const inert = target.closest('a[href="#"]');
        if (inert) {
            e.preventDefault();
            return;
        }

        if (target.closest('[data-fb-focus-composer]')) {
            const input = document.querySelector('.create-post-input');
            if (input) input.focus();
            return;
        }

        const reply = target.closest('[data-fb-reply]');
        if (reply) {
            const box = reply.closest('.post')?.querySelector('.fb-comment-composer input[name="message"]');
            if (box) box.focus();
            return;
        }

        const hide = target.closest('.fb-post-hide');
        if (hide) {
            hidePost(hide.closest('.post'));
            return;
        }

        const next = target.closest('[data-fb-stories-next]');
        if (next) {
            const row = next.closest('.fb-stories');
            const step = row.clientWidth * 0.6;
            row.scrollTo({ left: row.scrollLeft + step >= row.scrollWidth - row.clientWidth + 1 ? 0 : row.scrollLeft + step, behavior: 'smooth' });
            return;
        }

        const chatFilter = target.closest('[data-fb-chat-filter]');
        if (chatFilter) {
            chatFilter.parentElement.querySelectorAll('.fb-chip').forEach(c => c.classList.toggle('active', c === chatFilter));
            filterChats();
            return;
        }

        const thumb = target.closest('.fb-send-btn');
        if (thumb) {
            const input = thumb.form.querySelector('input[name="message"]');
            if (!input.value.trim()) input.value = '\u{1F44D}';
        }
    });

    function filterNotes(chip) {
        const unreadOnly = chip.dataset.fbNoteFilter === 'unread';
        chip.parentElement.querySelectorAll('.fb-chip').forEach(c => c.classList.toggle('active', c === chip));
        document.querySelectorAll('.fb-note').forEach(note => {
            note.hidden = unreadOnly && !note.querySelector('.fb-unread-dot');
        });
    }

    function hidePost(post) {
        if (!post) return;
        const note = document.createElement('div');
        note.className = 'fb-card fb-hidden-note';
        note.innerHTML = '<div><strong>Hidden</strong><span>Hiding posts helps Facebook personalize your Feed.</span></div>' +
            '<button type="button" class="fb-btn fb-btn-secondary">Undo</button>';
        post.hidden = true;
        post.after(note);
        note.querySelector('button').addEventListener('click', () => {
            post.hidden = false;
            note.remove();
        });
    }

    // ---------- post menu ----------
    // Runs before social.js's own menu (which positions itself off the card) and replaces it.

    document.addEventListener('click', function (e) {
        const button = e.target.closest('.post-menu');
        if (!button) return;
        e.preventDefault();
        e.stopPropagation();
        const post = button.closest('.post');
        const open = post.querySelector('.fb-post-menu');
        closePostMenus();
        if (!open) openPostMenu(post, button);
    }, true);

    function closePostMenus() {
        document.querySelectorAll('.fb-post-menu').forEach(menu => menu.remove());
    }

    function openPostMenu(post, button) {
        const first = (post.querySelector('.post-user-name a')?.textContent || '').trim().split(' ')[0];
        const saved = post.dataset.fbSaved === '1';
        const item = (act, icon, title, sub) =>
            `<button type="button" data-fb-post-act="${act}"><svg><use href="#${icon}"></use></svg><span><strong>${title}</strong>${sub ? `<small>${sub}</small>` : ''}</span></button>`;
        const menu = document.createElement('div');
        menu.className = 'fb-dropdown fb-post-menu';
        menu.setAttribute('role', 'menu');
        menu.innerHTML =
            item('save', 'fb-bookmark', saved ? 'Unsave post' : 'Save post', saved ? 'Remove this from your saved items.' : 'Add this to your saved items.') +
            '<div class="fb-account-sep"></div>' +
            item('hide', 'fb-hide', 'Hide post', 'See fewer posts like this.') +
            item('snooze', 'fb-clock', `Snooze ${escapeText(first)} for 30 days`, 'Temporarily stop seeing posts.') +
            item('report', 'fb-flag', 'Report post', `We won't let ${escapeText(first)} know who reported this.`) +
            '<div class="fb-account-sep"></div>' +
            item('copy', 'fb-link', 'Copy link', '');
        menu.style.top = (button.offsetTop + button.offsetHeight + 4) + 'px';
        post.appendChild(menu);
    }

    document.addEventListener('click', function (e) {
        const act = e.target.closest('[data-fb-post-act]');
        if (!act) {
            if (!e.target.closest('.fb-post-menu')) closePostMenus();
            return;
        }
        const post = act.closest('.post');
        switch (act.dataset.fbPostAct) {
            case 'save':
                post.dataset.fbSaved = post.dataset.fbSaved === '1' ? '0' : '1';
                break;
            case 'hide':
            case 'snooze':
            case 'report':
                hidePost(post);
                break;
            case 'copy':
                try { navigator.clipboard?.writeText(`${location.origin}/posts/${post.dataset.postId}`); } catch { /* not allowed on this page */ }
                break;
        }
        closePostMenus();
    });

    // ---------- likes ----------
    // social.js sends the like and rewrites the button as text; this runs after it and puts the icon back.
    // A like is kept once per person on the server, so a liked post stays liked.

    const likeSelector = '.post-action.like-it';
    let pressedLike = null;

    document.addEventListener('click', function (e) {
        pressedLike = e.target.closest(likeSelector);
        if (pressedLike) pressedLike.dataset.fbWasLiked = pressedLike.classList.contains('liked') ? '1' : '0';
    }, true);

    window.addEventListener('click', function () {
        const button = pressedLike;
        pressedLike = null;
        if (!button) return;

        button.classList.add('liked');
        button.innerHTML = '<svg class="fb-i"><use href="#fb-like-fill"></use></svg><span>Like</span>';
        if (button.dataset.fbWasLiked === '1') return;

        const post = button.closest('.post');
        const stats = post?.querySelector('.post-stats');
        const box = post?.querySelector('.fb-reactions');
        if (!box) return;
        if (stats) stats.hidden = false;
        box.hidden = false;
        const total = (parseInt(box.dataset.total || '0', 10) || 0) + 1;
        box.dataset.total = String(total);
        const others = total - 1;
        box.querySelector('.fb-reaction-text').textContent =
            others > 0 ? `You and ${compact(others)} ${others === 1 ? 'other' : 'others'}` : 'You';
    });

    // ---------- forms ----------

    document.addEventListener('submit', function (e) {
        const form = e.target;

        if (form.matches('.create-post form')) {
            e.preventDefault();
            const input = form.querySelector('.create-post-input');
            if (!input.value.trim()) return;
            send(form)
                .then(() => {
                    input.value = '';
                    input.blur();
                    // the new post arrives over SignalR on the feed; elsewhere show it by reloading
                    const live = document.getElementById('newsfeed-items-grid') && window.connection && window.connection.state === 'Connected';
                    if (!live) location.reload();
                })
                .catch(() => HTMLFormElement.prototype.submit.call(form));
            return;
        }

        if (form.matches('.fb-comment-form')) {
            e.preventDefault();
            const input = form.querySelector('input[name="message"]');
            const text = input.value.trim();
            if (!text) return;
            send(form)
                .then(() => {
                    input.value = '';
                    addComment(form, text);
                })
                .catch(() => HTMLFormElement.prototype.submit.call(form));
            return;
        }

        if (form.matches('.fb-follow-form')) {
            e.preventDefault();
            const button = form.querySelector('button');
            button.disabled = true;
            send(form)
                .then(() => {
                    const following = form.dataset.following !== 'true';
                    form.dataset.following = String(following);
                    form.setAttribute('action', following ? '/api/relationships/unfollow' : '/api/relationships/follow');
                    button.className = following ? 'fb-btn fb-btn-secondary' : 'fb-btn fb-btn-primary';
                    button.innerHTML = following
                        ? '<svg><use href="#fb-person-check"></use></svg><span>Friends</span>'
                        : '<svg><use href="#fb-person-add"></use></svg><span>Add friend</span>';
                })
                .finally(() => { button.disabled = false; });
        }
    });

    function addComment(form, text) {
        const me = form.querySelector('input[name="user"]').value;
        const link = '/u/' + encodeURIComponent(me);
        const comment = document.createElement('div');
        comment.className = 'fb-comment';
        comment.innerHTML =
            `<a href="${link}" class="fb-comment-avatar"><img src="${link}/avatar" alt=""></a>` +
            '<div class="fb-comment-main"><div class="fb-comment-bubble">' +
            `<a href="${link}" class="fb-comment-author">${window.fbDisplayName(escapeText(me))}</a>` +
            '<div class="fb-comment-text"></div></div>' +
            '<div class="fb-comment-meta"><span>Just now</span><button type="button">Like</button><button type="button" data-fb-reply>Reply</button></div></div>';
        comment.querySelector('.fb-comment-text').textContent = text;
        form.closest('.fb-comments').querySelector('.fb-comment-list').appendChild(comment);

        const post = form.closest('.post');
        const count = post.querySelector('.fb-comment-count');
        if (count) {
            const n = (parseInt(count.dataset.count, 10) || 0) + 1;
            count.dataset.count = String(n);
            count.textContent = `${n} ${n === 1 ? 'comment' : 'comments'}`;
        }
    }

    // ---------- Messenger ----------

    function filterChats() {
        const query = (document.querySelector('.fb-chats-search input')?.value || '').trim().toLowerCase();
        const unreadOnly = document.querySelector('[data-fb-chat-filter="unread"].active') !== null;
        document.querySelectorAll('.fb-chat-item').forEach(item => {
            const name = item.querySelector('.fb-chat-name').textContent.toLowerCase();
            item.hidden = (query && !name.includes(query)) || (unreadOnly && !item.classList.contains('unread'));
        });
    }

    document.addEventListener('input', function (e) {
        if (e.target.matches('.fb-chats-search input')) filterChats();
    });

    document.querySelectorAll('.fb-thread-body').forEach(body => { body.scrollTop = body.scrollHeight; });
})();
