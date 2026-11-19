// Instagram theme behaviour: dialogs, panels, stories, counts and the small touches of instagram.com.
// Posting, liking and commenting stay with social.js; this file only reacts around them.
(function () {
    'use strict';

    const $ = (sel, root) => (root || document).querySelector(sel);
    const $$ = (sel, root) => Array.from((root || document).querySelectorAll(sel));
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
    const me = (document.querySelector('meta[name="current-user"]') || {}).content || '';

    // ---- What SocialPresentation computes on the server, for posts that arrive over SignalR ----

    const photos = [
        ...[1, 2, 3, 4, 7, 8, 9, 10].map(i => `/img/last-photo${i}-large.jpg`),
        ...Array.from({ length: 10 }, (_, i) => `/img/photo-album${i + 1}.jpg`),
        ...Array.from({ length: 11 }, (_, i) => `/img/photo-item${i + 1}.jpg`),
        ...Array.from({ length: 16 }, (_, i) => `/img/post${i + 1}.jpg`),
        ...Array.from({ length: 7 }, (_, i) => `/img/post-photo${i + 1}.jpg`),
        ...Array.from({ length: 17 }, (_, i) => `/img/video${i + 1}.jpg`)
    ];
    const uploaded = /(?:<br\s*\/?>)?<img src="(\/images\/[0-9a-f-]{36}\/image\.(?:jpg|png))"\s*\/?>/i;

    function fnv(s) {
        let h = 0x811c9dc5;
        for (let i = 0; i < s.length; i++) {
            h ^= s.charCodeAt(i);
            h = Math.imul(h, 16777619);
        }
        return h >>> 0;
    }

    window.igPhoto = (id, message) => {
        const m = uploaded.exec(message || '');
        return m ? m[1] : photos[fnv(`${String(id).toLowerCase()}:0`) % photos.length];
    };
    window.igScore = id => {
        const unit = (fnv(`${String(id).toLowerCase()}:score`) % 10000) / 10000;
        return 2 + Math.floor(898 * unit * unit * unit);
    };
    window.igCaption = message => String(message || '').replace(uploaded, '');

    // ---- Toasts and dialogs ----

    function toast(text) {
        $$('.ig-toast').forEach(t => t.remove());
        const t = document.createElement('div');
        t.className = 'ig-toast';
        t.textContent = text;
        document.body.appendChild(t);
        setTimeout(() => t.remove(), 3000);
    }
    window.igToast = toast;

    function modal(html, cls) {
        closeModal();
        const back = document.createElement('div');
        back.className = 'ig-modal-backdrop';
        back.innerHTML = `<div class="ig-modal ${cls || ''}" role="dialog">${html}</div>`;
        back.addEventListener('click', e => { if (e.target === back) closeModal(); });
        document.body.appendChild(back);
        return back.firstElementChild;
    }
    // dialogs built here are thrown away; the server-rendered New message dialog is only hidden
    function closeModal() { $$('.ig-modal-backdrop').forEach(m => { if (m.id) m.hidden = true; else m.remove(); }); }

    function copyLink(url) {
        const done = () => toast('Link copied to clipboard.');
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(url).then(done, done);
            return;
        }
        const ta = document.createElement('textarea');
        ta.value = url;
        document.body.appendChild(ta);
        ta.select();
        try { document.execCommand('copy'); } catch (e) { /* nothing more to try */ }
        ta.remove();
        done();
    }

    function post(url, fields) {
        const body = new FormData();
        Object.entries(fields).forEach(([k, v]) => body.append(k, v));
        return fetch(url, { method: 'POST', body, credentials: 'same-origin' });
    }
    const sendDm = (to, message) => post('/messages/send', { toUsername: to, message, theme: 'instagram' });

    let people = null;
    function loadPeople() {
        if (!people) {
            people = fetch('/api/users').then(r => r.ok ? r.json() : []).then(list => {
                const seen = new Set();
                return list.filter(u => u.theme === 'instagram' && u.username !== me && !seen.has(u.username) && seen.add(u.username));
            }).catch(() => []);
        }
        return people;
    }

    function pickerList(list, filter) {
        const q = (filter || '').toLowerCase();
        const rows = list.filter(u => !q || u.username.toLowerCase().includes(q)).slice(0, 30).map(u => `
            <button type="button" class="ig-pick" data-username="${esc(u.username)}">
                <img src="/u/${encodeURIComponent(u.username)}/avatar" alt="" class="ig-avatar-44">
                <span class="ig-pick-who"><span class="ig-strong">${esc(u.username)}</span></span>
                <span class="ig-pick-circle"></span>
            </button>`).join('');
        return rows || '<div class="ig-search-empty">No account found.</div>';
    }

    function shareDialog(postId) {
        const url = `${location.origin}/posts/${postId}`;
        const box = modal(`
            <div class="ig-modal-head">Share<button type="button" class="ig-modal-x" aria-label="Close"><svg class="ig-i"><use href="#ig-close"/></svg></button></div>
            <label class="ig-modal-to">To: <input type="text" placeholder="Search..." autocomplete="off"></label>
            <div class="ig-modal-list"><h4>Suggested</h4><div class="ig-pick-list"></div></div>
            <div class="ig-share-links"><button type="button" data-copy><span class="ig-share-circle"><svg class="ig-i"><use href="#ig-share"/></svg></span>Copy link</button></div>
            <div class="ig-modal-foot"><button type="button" class="ig-btn ig-btn-blue" disabled>Send</button></div>`, 'ig-modal-wide');
        const listEl = $('.ig-pick-list', box), input = $('input', box), send = $('.ig-modal-foot button', box);
        const picked = new Set();
        loadPeople().then(list => {
            const draw = () => {
                listEl.innerHTML = pickerList(list, input.value);
                $$('.ig-pick', listEl).forEach(b => b.classList.toggle('is-picked', picked.has(b.dataset.username)));
            };
            input.addEventListener('input', draw);
            draw();
        });
        listEl.addEventListener('click', e => {
            const b = e.target.closest('.ig-pick');
            if (!b) return;
            picked.has(b.dataset.username) ? picked.delete(b.dataset.username) : picked.add(b.dataset.username);
            b.classList.toggle('is-picked');
            send.disabled = picked.size === 0;
        });
        $('[data-copy]', box).addEventListener('click', () => { closeModal(); copyLink(url); });
        $('.ig-modal-x', box).addEventListener('click', closeModal);
        send.addEventListener('click', () => {
            send.disabled = true;
            Promise.all([...picked].map(u => sendDm(u, url))).then(() => { closeModal(); toast('Sent'); }, () => toast("Couldn't send. Try again."));
        });
    }

    function optionsDialog(postId, user) {
        const mine = user === me;
        const box = modal(`<div class="ig-options">
            <button type="button" class="is-danger" data-act="report">Report</button>
            ${mine ? '' : '<button type="button" class="is-danger" data-act="unfollow">Unfollow</button>'}
            <button type="button" data-act="fav">Add to favorites</button>
            <a href="/posts/${esc(postId)}">Go to post</a>
            <button type="button" data-act="share">Share to...</button>
            <button type="button" data-act="copy">Copy link</button>
            <button type="button" data-act="close">Embed</button>
            <a href="/u/${esc(user)}">About this account</a>
            <button type="button" data-act="close">Cancel</button></div>`);
        box.addEventListener('click', e => {
            const act = (e.target.closest('[data-act]') || {}).dataset?.act;
            if (!act) return;
            closeModal();
            if (act === 'report') toast('Thanks for letting us know');
            if (act === 'fav') toast('Added to Favorites');
            if (act === 'share') shareDialog(postId);
            if (act === 'copy') copyLink(`${location.origin}/posts/${postId}`);
            if (act === 'unfollow') post('/api/relationships/unfollow', { username: user }).finally(() => toast(`Unfollowed ${user}`));
        });
    }

    // ---- Likes: social.js posts the like and flips the class; keep the count and animation in step ----

    function syncLikes(btn) {
        const card = btn.closest('[data-post-id]');
        const count = card && $('.ig-like-count', card);
        const liked = btn.classList.contains('liked');
        if (count) {
            const n = Number(count.dataset.base || 0) + (liked ? 1 : 0);
            count.textContent = n.toLocaleString('en-US');
            count.nextSibling && (count.nextSibling.textContent = n === 1 ? ' like' : ' likes');
        }
        btn.classList.remove('ig-pop');
        if (liked) { void btn.offsetWidth; btn.classList.add('ig-pop'); }
    }

    document.addEventListener('click', e => {
        const like = e.target.closest('.post-actions .like-it');
        if (like) setTimeout(() => syncLikes(like), 0);
    });

    document.addEventListener('dblclick', e => {
        const media = e.target.closest('[data-ig-dbl-like]');
        if (!media) return;
        const card = media.closest('[data-post-id]');
        const burst = $('.ig-burst', media);
        if (burst) { burst.classList.remove('is-on'); void burst.offsetWidth; burst.classList.add('is-on'); }
        const btn = card && $('.post-actions .like-it', card);
        if (btn && !btn.classList.contains('liked')) btn.click();
    });

    // ---- Every other click the theme owns ----

    document.addEventListener('click', e => {
        const t = e.target;

        if (t.closest('[data-ig-inert]')) { e.preventDefault(); return; }

        const save = t.closest('.ig-save');
        if (save) {
            const on = save.classList.toggle('is-saved');
            $('use', save).setAttribute('href', on ? '#ig-save-fill' : '#ig-save');
            save.setAttribute('aria-label', on ? 'Remove' : 'Save');
            return;
        }

        const more = t.closest('.ig-post-more');
        if (more) { optionsDialog(more.closest('[data-post-id]').dataset.postId, more.dataset.user); return; }

        const share = t.closest('.ig-share');
        if (share) { shareDialog(share.closest('[data-post-id]').dataset.postId); return; }

        const clamp = t.closest('.ig-more-link');
        if (clamp) { clamp.closest('.post-caption').classList.remove('is-clamped'); clamp.remove(); return; }

        const follow = t.closest('.ig-follow, .ig-follow-inline, .ig-follow-btn');
        if (follow) { e.preventDefault(); toggleFollow(follow); return; }

        const commentLike = t.closest('.comment-like-btn');
        if (commentLike) { commentLike.classList.toggle('liked'); return; }

        const reply = t.closest('.comment-reply');
        if (reply) {
            const i = $('.comment-input');
            const who = reply.closest('.comment').querySelector('.comment-username');
            if (i && who) { i.value = `@${who.textContent.trim()} `; i.focus(); i.dispatchEvent(new Event('input', { bubbles: true })); }
            return;
        }

        const commentIcon = t.closest('[data-ig-focus-comment]');
        if (commentIcon) { e.preventDefault(); const i = $('.comment-input'); i && i.focus(); return; }

        // social.js posts the comment and clears the box; on the feed there is no comment list, so show it under the post
        const postComment = t.closest('.comment-post-btn');
        if (postComment) {
            const input = postComment.parentElement.querySelector('.comment-input');
            const text = input.value.trim();
            const holder = postComment.closest('.feed') && $('.ig-recent-comments', postComment.closest('[data-post-id]'));
            if (text && holder) holder.insertAdjacentHTML('beforeend', `<div><b>${esc(me)}</b>${esc(text)}</div>`);
            setTimeout(() => postComment.parentElement.classList.toggle('has-text', !!input.value.trim()), 600);
            return;
        }
    });

    document.addEventListener('input', e => {
        const t = e.target;
        if (t.matches('.add-comment .comment-input')) t.parentElement.classList.toggle('has-text', !!t.value.trim());
        if (t.matches('.chat-input')) {
            t.closest('form').classList.toggle('has-text', !!t.value.trim());
            t.style.height = 'auto';
            t.style.height = Math.min(t.scrollHeight, 100) + 'px';
        }
        if (t.matches('.ig-create .post-input')) {
            const c = $('.ig-create-count');
            if (c) c.textContent = `${t.value.length.toLocaleString('en-US')}/2,200`;
        }
    });

    // ---- Follow and unfollow ----

    function toggleFollow(btn) {
        const user = btn.dataset.username;
        const following = btn.classList.contains('is-following');
        const isProfile = btn.classList.contains('ig-follow-btn');
        const buttons = isProfile ? $$('.ig-follow-btn').filter(b => b.dataset.username === user) : [btn];
        buttons.forEach(b => { b.disabled = true; });
        post(following ? '/api/relationships/unfollow' : '/api/relationships/follow', { username: user })
            .then(r => {
                if (!r.ok) throw new Error();
                buttons.forEach(b => {
                    b.classList.toggle('is-following', !following);
                    b.textContent = following ? 'Follow' : 'Following';
                    if (isProfile) b.classList.toggle('ig-btn-blue', following);
                });
                const n = isProfile && $('[data-followers-count]');
                if (n) n.textContent = Math.max(0, Number(n.textContent.replace(/,/g, '')) + (following ? -1 : 1)).toLocaleString('en-US');
            })
            .catch(() => toast("Couldn't follow. Try again."))
            .finally(() => buttons.forEach(b => { b.disabled = false; }));
    }

    // ---- Captions longer than two lines get "… more" ----

    function clampCaptions(root) {
        $$('.feed .post-caption:not([data-clamped])', root).forEach(c => {
            c.dataset.clamped = '1';
            c.classList.add('is-clamped');
            if (c.scrollHeight - c.clientHeight > 2) {
                const m = document.createElement('button');
                m.type = 'button';
                m.className = 'ig-more-link';
                m.textContent = '… more';
                c.after(m);
            } else {
                c.classList.remove('is-clamped');
            }
        });
    }

    // ---- Composer: the feed card doubles as the "Create new post" dialog ----

    function openCreate() {
        if (!$('#ig-create')) { location.href = '/#create'; return; }
        document.body.classList.add('ig-create-open');
        setTimeout(() => $('#ig-create .post-input').focus(), 0);
    }
    function closeCreate() {
        document.body.classList.remove('ig-create-open');
        if (location.hash === '#create') history.replaceState(null, '', location.pathname + location.search);
    }

    // ---- Panels and the More menu ----

    function closePanels() {
        $$('.ig-panel').forEach(p => { p.hidden = true; });
        $$('.ig-panel-on').forEach(i => i.classList.remove('ig-panel-on'));
        document.body.classList.remove('ig-panel-open');
    }
    function togglePanel(name, trigger) {
        const panel = $(`#ig-panel-${name}`);
        if (!panel) return;
        const open = panel.hidden;
        closePanels();
        if (!open) return;
        panel.hidden = false;
        document.body.classList.add('ig-panel-open');
        trigger.classList.add('ig-panel-on');
        const field = $('input', panel);
        field && field.focus();
    }

    document.addEventListener('click', e => {
        const t = e.target;
        const create = t.closest('[data-ig-create]');
        if (create) { e.preventDefault(); openCreate(); return; }
        if (t.closest('.ig-create-close') || (document.body.classList.contains('ig-create-open') && t.classList.contains('ig-create'))) { closeCreate(); return; }

        const panelBtn = t.closest('[data-ig-panel]');
        if (panelBtn) { e.preventDefault(); togglePanel(panelBtn.dataset.igPanel, panelBtn); return; }
        if (!t.closest('.ig-panel')) closePanels();

        const moreBtn = t.closest('[data-ig-more]');
        const menu = $('.ig-more-menu');
        if (moreBtn && menu) { e.preventDefault(); menu.hidden = !menu.hidden; moreBtn.classList.toggle('is-active', !menu.hidden); return; }
        if (menu && !t.closest('.ig-more-menu')) { menu.hidden = true; $$('[data-ig-more]').forEach(b => b.classList.remove('is-active')); }
    });

    document.addEventListener('keydown', e => {
        if (e.key !== 'Escape') return;
        closeModal();
        closePanels();
        closeCreate();
        closeViewer();
    });

    // ---- Stories ----

    let viewer = null, viewerIndex = 0, viewerTimer = null;

    function closeViewer() {
        clearTimeout(viewerTimer);
        if (viewer) { viewer.remove(); viewer = null; }
    }

    function showStory(i) {
        const stories = $$('.ig-story');
        if (i < 0 || i >= stories.length) { closeViewer(); return; }
        viewerIndex = i;
        const s = stories[i];
        s.classList.add('is-seen');
        if (!viewer) {
            viewer = document.createElement('div');
            viewer.className = 'ig-viewer';
            viewer.innerHTML = `
                <img class="ig-viewer-logo" src="/themes/instagram/images/wordmark.png" alt="Instagram">
                <button type="button" class="ig-viewer-x" aria-label="Close"><svg class="ig-i"><use href="#ig-close"/></svg></button>
                <div class="ig-viewer-stage">
                    <button type="button" class="ig-viewer-nav ig-viewer-prev" aria-label="Previous"><svg class="ig-i ig-i-16" style="transform:rotate(180deg)"><use href="#ig-arrow"/></svg></button>
                    <div class="ig-viewer-card">
                        <div class="ig-viewer-bars"><span><i></i></span></div>
                        <div class="ig-viewer-head"><img alt=""><b></b><span></span></div>
                        <img class="ig-viewer-photo" alt="">
                        <form class="ig-viewer-reply"><input type="text" autocomplete="off"><svg class="ig-i"><use href="#ig-heart"/></svg><svg class="ig-i"><use href="#ig-share"/></svg></form>
                    </div>
                    <button type="button" class="ig-viewer-nav ig-viewer-next" aria-label="Next"><svg class="ig-i ig-i-16"><use href="#ig-arrow"/></svg></button>
                </div>`;
            document.body.appendChild(viewer);
            $('.ig-viewer-x', viewer).addEventListener('click', closeViewer);
            $('.ig-viewer-prev', viewer).addEventListener('click', () => showStory(viewerIndex - 1));
            $('.ig-viewer-next', viewer).addEventListener('click', () => showStory(viewerIndex + 1));
            $('.ig-viewer-photo', viewer).addEventListener('click', e => {
                const r = e.currentTarget.getBoundingClientRect();
                showStory(viewerIndex + (e.clientX - r.left < r.width / 3 ? -1 : 1));
            });
            const reply = $('.ig-viewer-reply', viewer);
            const input = $('input', reply);
            input.addEventListener('focus', () => clearTimeout(viewerTimer));
            reply.addEventListener('submit', e => {
                e.preventDefault();
                const text = input.value.trim();
                const to = $$('.ig-story')[viewerIndex].dataset.user;
                if (!text) return;
                input.value = '';
                sendDm(to, text).then(() => toast('Sent'), () => toast("Couldn't send. Try again."));
                showStory(viewerIndex + 1);
            });
        }
        const user = s.dataset.user;
        $('.ig-viewer-head img', viewer).src = `/u/${encodeURIComponent(user)}/avatar`;
        $('.ig-viewer-head b', viewer).textContent = user;
        $('.ig-viewer-head span', viewer).textContent = s.dataset.ago;
        $('.ig-viewer-photo', viewer).src = s.dataset.photo;
        $('.ig-viewer-reply input', viewer).placeholder = `Reply to ${user}...`;
        const bar = $('.ig-viewer-bars i', viewer);
        bar.style.transition = 'none';
        bar.style.width = '0';
        void bar.offsetWidth;
        bar.style.transition = 'width 5s linear';
        bar.style.width = '100%';
        clearTimeout(viewerTimer);
        viewerTimer = setTimeout(() => showStory(viewerIndex + 1), 5000);
    }

    document.addEventListener('click', e => {
        const story = e.target.closest('.ig-story');
        if (story) showStory($$('.ig-story').indexOf(story));

        const arrow = e.target.closest('.ig-stories-arrow');
        if (arrow) {
            const list = $('.ig-stories-list');
            const max = Math.max(0, list.scrollWidth - list.parentElement.clientWidth);
            const now = -(parseFloat(list.dataset.x || '0'));
            const next = Math.min(max, Math.max(0, now + (arrow.classList.contains('ig-stories-next') ? 320 : -320)));
            list.dataset.x = String(-next);
            list.style.transform = `translateX(${-next}px)`;
            $('.ig-stories-prev').hidden = next <= 0;
            $('.ig-stories-next').hidden = next >= max;
        }
    });

    // ---- Profile: tabs and follower lists ----

    document.addEventListener('click', e => {
        const tab = e.target.closest('.ig-tab[data-tab]');
        if (tab) {
            $$('.ig-tab').forEach(b => b.classList.toggle('is-active', b === tab));
            $$('.ig-tab-panel').forEach(p => { p.hidden = p.dataset.panel !== tab.dataset.tab; });
            return;
        }

        const list = e.target.closest('[data-ig-connections]');
        if (list) {
            const kind = list.dataset.igConnections;
            const user = list.dataset.username;
            const box = modal(`
                <div class="ig-modal-head">${kind === 'followers' ? 'Followers' : 'Following'}<button type="button" class="ig-modal-x" aria-label="Close"><svg class="ig-i"><use href="#ig-close"/></svg></button></div>
                <div class="ig-connections"><div class="connections-scroll"><div class="connection-empty">Loading…</div></div></div>`, 'ig-modal-wide');
            $('.ig-modal-x', box).addEventListener('click', closeModal);
            fetch(`/api/relationships/${encodeURIComponent(user)}/connections`)
                .then(r => r.ok ? r.json() : Promise.reject())
                .then(data => {
                    const users = data[kind] || [];
                    $('.connections-scroll', box).innerHTML = users.length === 0
                        ? `<div class="connection-empty">${kind === 'followers' ? 'No followers yet.' : 'Not following anyone yet.'}</div>`
                        : users.map(u => `<a class="connection-item" href="/u/${encodeURIComponent(u.username)}"><img class="connection-avatar" src="/u/${encodeURIComponent(u.username)}/avatar" alt=""><span class="connection-name">${esc(u.username)}</span></a>`).join('');
                })
                .catch(() => { $('.connections-scroll', box).innerHTML = '<div class="connection-empty">Couldn\'t load this list.</div>'; });
        }
    });

    // ---- Direct ----

    function initDirect() {
        const thread = $('.chat-messages');
        if (thread) thread.scrollTop = thread.scrollHeight;

        $$('.ig-chat-heart').forEach(heart => heart.addEventListener('click', () => {
            const input = heart.closest('form').querySelector('.chat-input');
            input.value = '\u2764\ufe0f';
            input.form.requestSubmit();
        }));

        $$('.chat-input').forEach(input => {
            input.addEventListener('keydown', e => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    if (input.value.trim()) input.form.requestSubmit();
                }
            });
        });

        const dialog = $('#ig-new-message');
        if (!dialog) return;
        const open = () => { dialog.hidden = false; $('input[name="toUsername"]', dialog).focus(); };
        $$('[data-ig-new-message]').forEach(b => b.addEventListener('click', e => { e.preventDefault(); open(); }));
        dialog.addEventListener('click', e => {
            if (e.target === dialog || e.target.closest('.ig-modal-x')) dialog.hidden = true;
            const pick = e.target.closest('.ig-pick');
            if (pick) {
                $$('.ig-pick', dialog).forEach(p => p.classList.toggle('is-picked', p === pick));
                $('input[name="toUsername"]', dialog).value = pick.dataset.username;
                $('textarea[name="message"]', dialog).focus();
            }
        });
        const to = $('input[name="toUsername"]', dialog);
        to.addEventListener('input', () => {
            const q = to.value.trim().toLowerCase();
            $$('.ig-pick', dialog).forEach(p => { p.hidden = q && !p.dataset.username.toLowerCase().includes(q); });
        });
    }

    // ---- Start ----

    document.addEventListener('DOMContentLoaded', () => {
        clampCaptions();
        initDirect();

        const feed = document.getElementById('newsfeed-items-grid');
        if (feed) new MutationObserver(() => clampCaptions(feed)).observe(feed, { childList: true });

        const form = $('#ig-create form');
        if (form) {
            form.addEventListener('submit', () => {
                // the browser has already read the fields; tidy up the way Instagram does after sharing
                setTimeout(() => {
                    form.reset();
                    const c = $('.ig-create-count');
                    if (c) c.textContent = '0/2,200';
                    if (document.body.classList.contains('ig-create-open')) closeCreate();
                    toast('Your post has been shared.');
                }, 400);
            });
        }
        if (location.hash === '#create') openCreate();

        const tab = new URLSearchParams(location.search).get('tab');
        const tabBtn = tab && $(`.ig-tab[data-tab="${CSS.escape(tab)}"]`);
        if (tabBtn) tabBtn.click();
    });
})();
