// X theme behaviour: counts, menus, follow buttons, composer, tabs and DMs.
// Posting, liking and commenting themselves stay with /js/social.js.
(function () {
    'use strict';

    const me = (document.querySelector('meta[name="current-user"]') || {}).content || '';
    const names = window.xNames || {};

    function esc(value) {
        return String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
    }

    function compact(n) {
        if (n >= 1e6) return (n >= 1e7 ? Math.floor(n / 1e6) : Math.round(n / 1e5) / 10) + 'M';
        if (n >= 1e3) return (n >= 1e4 ? Math.floor(n / 1e3) : Math.round(n / 1e2) / 10) + 'K';
        return n > 0 ? String(n) : '';
    }

    function toast(text) {
        document.querySelectorAll('.x-toast').forEach(t => t.remove());
        const t = document.createElement('div');
        t.className = 'x-toast';
        t.textContent = text;
        document.body.appendChild(t);
        setTimeout(() => t.remove(), 3000);
    }

    function setCount(button, delta) {
        const n = Math.max(0, (parseInt(button.dataset.count, 10) || 0) + delta);
        button.dataset.count = n;
        const label = button.querySelector('.ab-n');
        if (label) label.textContent = compact(n);
    }

    function post(url, data) {
        const body = new FormData();
        Object.entries(data).forEach(([k, v]) => body.append(k, v));
        return fetch(url, { method: 'POST', body, credentials: 'same-origin' });
    }

    function autosize(area) {
        area.style.height = 'auto';
        area.style.height = area.scrollHeight + 'px';
    }

    // ---------- likes: social.js toggles .liked and saves; this keeps the count in step ----------
    let likeBefore = null;
    document.addEventListener('click', e => {
        const like = e.target.closest('.like-it');
        if (like) likeBefore = { el: like, liked: like.classList.contains('liked') };
    }, true);
    document.addEventListener('click', () => {
        if (!likeBefore) return;
        const { el, liked } = likeBefore;
        likeBefore = null;
        setTimeout(() => {
            const now = el.classList.contains('liked');
            if (now !== liked) setCount(el, now ? 1 : -1);
        }, 0);
    });

    // ---------- whole rows that open a page ----------
    document.addEventListener('click', e => {
        const row = e.target.closest('[data-href]');
        if (!row || e.defaultPrevented) return;
        if (e.target.closest('a, button, input, textarea, form, label, .x-menu')) return;
        if (String(window.getSelection())) return;
        window.location.href = row.dataset.href;
    });

    // ---------- repost, bookmark, share ----------
    document.addEventListener('click', e => {
        const repost = e.target.closest('.act-repost');
        if (repost) {
            const on = repost.classList.toggle('on');
            setCount(repost, on ? 1 : -1);
            return;
        }
        const bookmark = e.target.closest('.act-bookmark');
        if (bookmark) {
            const on = bookmark.classList.toggle('on');
            if (bookmark.dataset.count !== undefined) setCount(bookmark, on ? 1 : -1);
            toast(on ? 'Added to your Bookmarks' : 'Removed from your Bookmarks');
            return;
        }
        const share = e.target.closest('.act-share');
        if (share) {
            const card = share.closest('[data-post-id]');
            const url = card ? window.location.origin + '/posts/' + card.dataset.postId : window.location.href;
            openMenu(share, [
                { icon: 'x-link', text: 'Copy link', run: () => copy(url) },
                { icon: 'x-mail', text: 'Send via Direct Message', run: () => { window.location.href = '/messages'; } }
            ]);
        }
    });

    function copy(text) {
        if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(text).catch(() => {});
        toast('Copied to clipboard');
    }

    // ---------- follow buttons ----------
    function setFollowing(username, following) {
        document.querySelectorAll('[data-x-follow]').forEach(button => {
            if (button.dataset.xFollow.toLowerCase() !== username.toLowerCase()) return;
            button.classList.toggle('following', following);
            button.textContent = following ? 'Following' : 'Follow';
        });
        if (window.refreshConnections) window.refreshConnections(username);
    }

    function follow(username, following) {
        return post(following ? '/api/relationships/unfollow' : '/api/relationships/follow', { username })
            .then(r => { if (!r.ok) throw new Error(r.status); });
    }

    document.addEventListener('click', e => {
        const button = e.target.closest('[data-x-follow]');
        if (!button) return;
        const following = button.classList.contains('following');
        button.disabled = true;
        follow(button.dataset.xFollow, following)
            .then(() => setFollowing(button.dataset.xFollow, !following))
            .catch(() => toast('Something went wrong. Try again.'))
            .finally(() => { button.disabled = false; });
    });
    document.addEventListener('mouseover', e => {
        const button = e.target.closest('.x-follow.following');
        if (button) button.textContent = 'Unfollow';
    });
    document.addEventListener('mouseout', e => {
        const button = e.target.closest('.x-follow.following');
        if (button && !button.contains(e.relatedTarget)) button.textContent = 'Following';
    });

    // ---------- popup menus ----------
    let menu = null;
    let menuAnchor = null;
    function closeMenu() {
        if (menu) menu.remove();
        menu = menuAnchor = null;
    }

    function openMenu(anchor, items, above) {
        closeMenu();
        menuAnchor = anchor;
        menu = document.createElement('div');
        menu.className = 'x-menu';
        items.forEach(item => {
            const b = document.createElement('button');
            b.type = 'button';
            b.innerHTML = (item.icon ? `<svg><use href="#${item.icon}"/></svg>` : '') + `<span>${esc(item.text)}</span>`;
            b.addEventListener('click', ev => { ev.stopPropagation(); closeMenu(); if (item.run) item.run(); });
            menu.appendChild(b);
        });
        document.body.appendChild(menu);
        const r = anchor.getBoundingClientRect();
        const w = menu.offsetWidth;
        let left = r.right + window.scrollX - w;
        if (left < 8) left = Math.min(r.left + window.scrollX, window.innerWidth - w - 8);
        const top = above ? r.top + window.scrollY - menu.offsetHeight - 8 : r.top + window.scrollY;
        menu.style.left = Math.max(8, left) + 'px';
        menu.style.top = Math.max(8, top) + 'px';
    }

    function hideUser(username) {
        document.querySelectorAll(`.tweet[data-username="${CSS.escape(username)}"]`).forEach(t => t.remove());
    }

    const menus = {
        post(button) {
            const card = button.closest('[data-username]');
            const user = card ? card.dataset.username : '';
            const handle = '@' + user;
            if (user.toLowerCase() === me.toLowerCase()) {
                const link = card && card.dataset.postId ? window.location.origin + '/posts/' + card.dataset.postId : window.location.href;
                return [{ icon: 'x-link', text: 'Copy link to post', run: () => copy(link) }];
            }
            return [
                { icon: 'x-sad', text: 'Not interested in this post', run: () => { card.remove(); toast('Thanks. X will use this to make your timeline better.'); } },
                { icon: 'x-follow', text: 'Follow ' + handle, run: () => follow(user, false).then(() => setFollowing(user, true)).catch(() => {}) },
                { icon: 'x-mute', text: 'Mute ' + handle, run: () => { hideUser(user); toast('You muted ' + handle); } },
                { icon: 'x-block', text: 'Block ' + handle, run: () => { hideUser(user); toast(handle + ' has been blocked'); } },
                { icon: 'x-flag', text: 'Report post', run: () => toast('Thanks for reporting this post.') }
            ];
        },
        more: () => [
            { icon: 'x-lists', text: 'Lists' },
            { icon: 'x-briefcase', text: 'Jobs' },
            { icon: 'x-communities', text: 'Spaces' },
            { icon: 'x-gear', text: 'Settings and privacy' }
        ],
        account: button => [
            { text: 'Add an existing account' },
            { text: 'Log out ' + button.dataset.handle }
        ],
        trend: () => ['The associated content is not relevant', 'This trend is spam', 'This trend is abusive or harmful', 'Not interested in this']
            .map(text => ({ icon: 'x-sad', text, run: () => toast('Thanks. Refresh this page to update these trends.') })),
        footer: () => [{ text: 'About' }, { text: 'Download the X app' }, { text: 'Grok' }, { text: 'Careers' }, { text: 'Developers' }],
        dm: () => [{ icon: 'x-gear', text: 'Message settings' }],
        profile: button => {
            const handle = '@' + button.dataset.user;
            return [
                { icon: 'x-lists', text: 'Add/remove from Lists' },
                { icon: 'x-link', text: 'Copy link to profile', run: () => copy(window.location.origin + '/u/' + button.dataset.user) },
                { icon: 'x-mute', text: 'Mute ' + handle, run: () => toast('You muted ' + handle) },
                { icon: 'x-block', text: 'Block ' + handle, run: () => toast(handle + ' has been blocked') },
                { icon: 'x-flag', text: 'Report ' + handle, run: () => toast('Thanks for reporting ' + handle + '.') }
            ];
        }
    };

    document.addEventListener('click', e => {
        const button = e.target.closest('[data-x-menu]');
        if (button) {
            const kind = button.dataset.xMenu;
            openMenu(button, menus[kind](button), kind === 'account' || kind === 'more');
            return;
        }
        if (menu && !e.target.closest('.x-menu') && !menuAnchor.contains(e.target)) closeMenu();
    });
    document.addEventListener('keydown', e => {
        if (e.key !== 'Escape') return;
        closeMenu();
        document.querySelectorAll('.x-modal').forEach(m => { m.hidden = true; });
    });
    window.addEventListener('resize', closeMenu);

    // ---------- features this range does not offer fail the way X does when its backend is down ----------
    document.addEventListener('click', e => {
        if (e.target.closest('button[data-x-unavailable]')) toast('Something went wrong. Try reloading.');
    });
    document.addEventListener('submit', e => {
        if (!e.target.matches('form[data-x-unavailable]')) return;
        e.preventDefault();
        toast('Something went wrong. Try reloading.');
    });

    // ---------- composer ----------
    const composer = document.querySelector('.compose-tweet');
    if (composer) {
        const area = composer.querySelector('.tweet-input');
        const toolbar = composer.querySelector('.tweet-toolbar');
        const bar = composer.querySelector('.x-counter-bar');
        const refresh = () => {
            autosize(area);
            const len = area.value.length;
            toolbar.toggleAttribute('data-empty', !area.value.trim());
            toolbar.toggleAttribute('data-count', len > 0);
            if (bar) {
                bar.style.strokeDashoffset = 56.55 * (1 - Math.min(len, 280) / 280);
                bar.style.stroke = len >= 280 ? '#f4212e' : len >= 260 ? '#ffd400' : '';
            }
        };
        area.addEventListener('input', refresh);
        area.addEventListener('focus', () => composer.setAttribute('data-open', ''));
        composer.querySelector('form').addEventListener('submit', () => {
            // the browser has already read the form by the time this runs
            setTimeout(() => { area.value = ''; refresh(); toast('Your post was sent.'); }, 0);
        });
        refresh();
    }

    document.addEventListener('click', e => {
        const button = e.target.closest('[data-x-compose]');
        if (!button) return;
        const area = document.querySelector('.compose-tweet .tweet-input');
        if (!area) return; // no composer on this page: the link goes to the home timeline
        e.preventDefault();
        window.scrollTo({ top: 0 });
        area.focus();
    });

    // ---------- For you / Following ----------
    document.querySelectorAll('[data-x-tab]').forEach(tab => tab.addEventListener('click', e => {
        e.preventDefault();
        document.querySelectorAll('[data-x-tab]').forEach(t => t.classList.toggle('active', t === tab));
        const tweets = document.querySelectorAll('#newsfeed-items-grid .tweet');
        const empty = document.getElementById('x-following-empty');
        if (tab.dataset.xTab !== 'following') {
            tweets.forEach(t => { t.hidden = false; });
            if (empty) empty.hidden = true;
            return;
        }
        fetch(`/api/relationships/${encodeURIComponent(me)}/connections`, { credentials: 'same-origin' })
            .then(r => r.ok ? r.json() : { following: [] })
            .catch(() => ({ following: [] }))
            .then(data => {
                const following = new Set((data.following || []).map(u => u.username.toLowerCase()));
                let shown = 0;
                tweets.forEach(t => {
                    t.hidden = !following.has((t.dataset.username || '').toLowerCase());
                    if (!t.hidden) shown++;
                });
                if (empty) empty.hidden = shown > 0;
            });
    }));

    // ---------- reply box on a post (social.js saves the reply) ----------
    const replyBox = document.querySelector('.x-reply-box');
    if (replyBox) {
        const area = replyBox.querySelector('.comment-input');
        const refresh = () => { autosize(area); replyBox.toggleAttribute('data-empty', !area.value.trim()); };
        area.addEventListener('input', refresh);
        area.addEventListener('focus', () => replyBox.setAttribute('data-open', ''));
        refresh();

        if (window.jQuery) {
            jQuery(document).ajaxSuccess((event, xhr, settings) => {
                if (!/\/posts\/[^/]+\/comments$/.test(settings.url || '') || !(settings.data instanceof FormData)) return;
                const text = settings.data.get('message') || '';
                const list = document.querySelector('.x-replies');
                if (list) list.insertAdjacentHTML('afterbegin', replyMarkup(text));
                const count = document.querySelector('.x-focal .act-reply');
                if (count) setCount(count, 1);
                document.querySelectorAll('.toast').forEach(t => t.remove());
                toast('Your post was sent.');
                refresh();
            });
        }
    }

    function replyMarkup(text) {
        const user = esc(me);
        return `
            <article class="tweet" data-username="${user}">
                <a href="/u/${user}" class="tweet-avatar-link"><img src="/u/${user}/avatar" alt="" class="tweet-avatar"></a>
                <div class="tweet-content">
                    <div class="tweet-header">
                        <a href="/u/${user}" class="tweet-name">${esc(names[me] || me)}</a>
                        <a href="/u/${user}" class="tweet-handle">@${user}</a>
                        <span class="tweet-dot">·</span>
                        <span class="tweet-time">now</span>
                    </div>
                    <div class="tweet-text">${esc(text)}</div>
                    <div class="tweet-actions" role="group">
                        <span class="action-btn act-reply"><span class="ab-ic"><svg><use href="#x-reply"/></svg></span><span class="ab-n"></span></span>
                        <button type="button" class="action-btn act-repost" data-count="0"><span class="ab-ic"><svg><use href="#x-repost"/></svg></span><span class="ab-n"></span></button>
                        <button type="button" class="action-btn act-like" data-count="0"><span class="ab-ic"><svg class="ic-off"><use href="#x-like"/></svg><svg class="ic-on"><use href="#x-like-fill"/></svg></span><span class="ab-n"></span></button>
                        <span class="action-btn act-views"><span class="ab-ic"><svg><use href="#x-views"/></svg></span><span class="ab-n"></span></span>
                        <div class="ab-end">
                            <button type="button" class="action-btn act-bookmark"><span class="ab-ic"><svg class="ic-off"><use href="#x-bookmark"/></svg><svg class="ic-on"><use href="#x-bookmark-fill"/></svg></span></button>
                            <button type="button" class="action-btn act-share"><span class="ab-ic"><svg><use href="#x-share"/></svg></span></button>
                        </div>
                    </div>
                </div>
            </article>`;
    }

    // likes on replies are not stored, so they only change on the page
    document.addEventListener('click', e => {
        const like = e.target.closest('.act-like');
        if (!like) return;
        const on = like.classList.toggle('liked');
        setCount(like, on ? 1 : -1);
    });

    // ---------- profile: tabs and follower lists ----------
    document.querySelectorAll('[data-x-pane]').forEach(tab => tab.addEventListener('click', e => {
        e.preventDefault();
        document.querySelectorAll('[data-x-pane]').forEach(t => t.classList.toggle('active', t === tab));
        document.querySelectorAll('.x-pane').forEach(p => { p.hidden = p.id !== tab.dataset.xPane; });
    }));
    document.querySelectorAll('[data-x-connections]').forEach(button => button.addEventListener('click', () => {
        const panel = document.getElementById('x-connections-' + button.dataset.xConnections);
        const open = panel.hidden;
        document.querySelectorAll('.x-connections').forEach(p => { p.hidden = true; });
        panel.hidden = !open;
    }));

    // ---------- messages ----------
    document.addEventListener('click', e => {
        const open = e.target.closest('[data-x-newdm]');
        if (open) {
            e.preventDefault();
            const modal = document.getElementById('x-newdm');
            modal.hidden = false;
            modal.querySelector('input[name="toUsername"]').focus();
            return;
        }
        if (e.target.closest('[data-x-close]') || e.target.classList.contains('x-modal')) {
            const modal = e.target.closest('.x-modal');
            if (modal) modal.hidden = true;
            return;
        }
        const pick = e.target.closest('[data-x-pick]');
        if (pick) {
            const modal = pick.closest('.x-modal');
            modal.querySelectorAll('[data-x-pick]').forEach(p => p.classList.toggle('picked', p === pick));
            modal.querySelector('input[name="toUsername"]').value = pick.dataset.xPick;
            modal.querySelector('textarea[name="message"]').focus();
        }
    });

    const people = document.querySelector('.x-modal-people');
    const to = document.querySelector('#x-newdm input[name="toUsername"]');
    if (people && to) {
        to.addEventListener('input', () => {
            const q = to.value.trim().toLowerCase();
            people.querySelectorAll('[data-x-pick]').forEach(p => { p.hidden = q !== '' && !p.textContent.toLowerCase().includes(q); });
        });
    }

    const filter = document.querySelector('[data-x-dm-filter]');
    if (filter) {
        filter.addEventListener('input', () => {
            const q = filter.value.trim().toLowerCase();
            document.querySelectorAll('.x-dm-row').forEach(r => { r.hidden = q !== '' && !r.textContent.toLowerCase().includes(q); });
        });
    }

    document.querySelectorAll('.x-chat-form').forEach(form => {
        const area = form.querySelector('textarea');
        const refresh = () => { autosize(area); form.toggleAttribute('data-empty', !area.value.trim()); };
        area.addEventListener('input', refresh);
        area.addEventListener('keydown', e => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                if (area.value.trim()) form.requestSubmit();
            }
        });
        refresh();
    });

    const chat = document.querySelector('.x-chat-scroll');
    if (chat) chat.scrollTop = chat.scrollHeight;
})();
