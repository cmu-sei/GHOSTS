// Reddit theme behaviour: menus, votes, feed sorting, comment threads and chat.
// Posting, liking and commenting stay with /js/social.js; this file only presents them the way Reddit does.
(function () {
    'use strict';

    const $ = (selector, root = document) => root.querySelector(selector);
    const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));
    const me = ($('meta[name="current-user"]') || {}).content || ($('.user-menu .username') || {}).textContent || 'user';
    const store = {
        get(key, fallback) { try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch (e) { return fallback; } },
        set(key, value) { try { localStorage.setItem(key, JSON.stringify(value)); } catch (e) { } }
    };

    function compact(n) {
        const short = (value, digits) => value.toFixed(digits).replace(/\.0$/, '');
        if (n >= 1e6) return short(n / 1e6, n >= 1e7 ? 0 : 1) + 'M';
        if (n >= 1e3) return short(n / 1e3, n >= 1e4 ? 0 : 1) + 'K';
        return String(n);
    }

    function toast(text) {
        $$('.r-toast').forEach(t => t.remove());
        const t = document.createElement('div');
        t.className = 'r-toast';
        t.setAttribute('role', 'status');
        t.textContent = text;
        document.body.append(t);
        setTimeout(() => t.remove(), 3200);
    }

    // ---------- Floating menus ----------
    function closeMenus() {
        $$('.r-pop').forEach(m => m.remove());
        $$('.r-menu[data-open]').forEach(m => m.removeAttribute('data-open'));
        $$('[data-r="menu"]').forEach(b => b.setAttribute('aria-expanded', 'false'));
    }

    function popMenu(anchor, items) {
        closeMenus();
        const menu = document.createElement('div');
        menu.className = 'r-pop';
        menu.setAttribute('role', 'menu');
        items.forEach(([icon, label, action]) => {
            const item = document.createElement('button');
            item.type = 'button';
            item.className = 'r-pop-item';
            item.setAttribute('role', 'menuitem');
            item.innerHTML = `<svg class="ri"><use href="#${icon}"/></svg>`;
            item.append(label);
            item.addEventListener('click', e => { e.stopPropagation(); closeMenus(); action(); });
            menu.append(item);
        });
        document.body.append(menu);
        const r = anchor.getBoundingClientRect();
        const width = document.documentElement.clientWidth;
        menu.style.top = (r.bottom + window.scrollY + 4) + 'px';
        menu.style.left = Math.max(8, Math.min(r.right + window.scrollX - menu.offsetWidth, width - menu.offsetWidth - 8)) + 'px';
    }

    function copyLink(path) {
        const url = new URL(path || location.pathname, location.origin).href;
        const done = () => toast('Link copied to clipboard');
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(url).then(done, done);
        } else {
            const field = document.createElement('textarea');
            field.value = url;
            document.body.append(field);
            field.select();
            try { document.execCommand('copy'); } catch (e) { }
            field.remove();
            done();
        }
    }

    // ---------- Votes ----------
    // The pill keeps the score without the reader's vote in data-base and the vote in data-vote
    function setVote(pill, vote) {
        pill.dataset.vote = vote || '';
        const base = parseInt(pill.dataset.base, 10) || 0;
        const score = base + (vote === 'up' ? 1 : 0) - (vote === 'down' ? 1 : 0);
        const label = $('.r-score', pill);
        if (label) label.textContent = compact(Math.max(0, score));
        const up = pill.querySelector('button');
        if (up) up.setAttribute('aria-pressed', vote === 'up' ? 'true' : 'false');
    }

    function onVote(button) {
        const pill = button.closest('.r-vote');
        const down = button.dataset.r === 'down';
        const current = pill.dataset.vote || '';
        setVote(pill, down ? (current === 'down' ? '' : 'down') : (current === 'up' ? '' : 'up'));
        if (button.classList.contains('like-it')) {
            // social.js has already sent the like; it marks the button with a class, which would
            // break the exact-class XPath NPC clients use, so the state lives on the pill instead
            setTimeout(() => button.classList.remove('voted', 'liked'), 0);
        }
    }

    // ---------- Feed ----------
    function sortFeed(how) {
        const feed = $('#newsfeed-items-grid');
        if (!feed) return;
        const items = $$('.r-item', feed);
        items.forEach((item, i) => { if (!item.dataset.order) item.dataset.order = String(i); });
        const now = Date.now() / 1000;
        const score = item => parseInt(item.dataset.score, 10) || 0;
        const created = item => parseInt(item.dataset.created, 10) || now;
        const hot = item => score(item) / Math.pow((now - created(item)) / 3600 + 2, 1.5);
        const order = {
            best: (a, b) => a.dataset.order - b.dataset.order,
            hot: (a, b) => hot(b) - hot(a),
            new: (a, b) => created(b) - created(a),
            top: (a, b) => score(b) - score(a)
        }[how];
        items.sort(order).forEach(item => feed.append(item));
    }

    function setLayout(layout) {
        const feed = $('#newsfeed-items-grid');
        if (feed) feed.dataset.layout = layout;
        store.set('r-layout', layout);
    }

    function joined() { return new Set(store.get('r-joined', [])); }

    function paintJoin(button, isJoined) {
        button.textContent = isJoined ? 'Joined' : 'Join';
        if (isJoined) button.dataset.joined = ''; else delete button.dataset.joined;
    }

    // ---------- Comments ----------
    function commentElement(author, text) {
        const el = document.createElement('div');
        el.className = 'r-comment';
        el.dataset.score = '1';
        el.dataset.created = String(Math.floor(Date.now() / 1000));
        el.innerHTML = `
            <div class="r-c-head"><button type="button" class="r-c-expand" aria-label="Expand comment">+</button><img alt=""><a class="r-c-author"></a><span class="r-dot">&bull;</span><time>just now</time></div>
            <div class="r-c-body">
                <button type="button" class="r-c-line" aria-label="Collapse comment"></button>
                <div class="r-c-text"></div>
                <div class="r-c-actions">
                    <span class="r-vote" data-vote="up" data-base="0"><button type="button" aria-label="Upvote" aria-pressed="true"><svg class="ri"><use href="#i-up"/></svg></button><span class="r-score">1</span><button type="button" aria-label="Downvote" data-r="down"><svg class="ri"><use href="#i-down"/></svg></button></span>
                    <button type="button" class="r-c-btn" data-r="reply"><svg class="ri ri-sm"><use href="#i-comment"/></svg>Reply</button>
                    <button type="button" class="r-c-btn" aria-label="Give award"><svg class="ri ri-sm"><use href="#i-award"/></svg><span class="r-c-label">Award</span></button>
                    <button type="button" class="r-c-btn" data-r="share"><svg class="ri ri-sm"><use href="#i-share"/></svg>Share</button>
                    <button type="button" class="r-c-btn" aria-label="More options" data-r="more"><svg class="ri ri-sm"><use href="#i-more"/></svg></button>
                </div>
                <div class="r-c-children"></div>
            </div>`;
        $('img', el).src = `/u/${encodeURIComponent(author)}/avatar`;
        const link = $('.r-c-author', el);
        link.href = `/u/${encodeURIComponent(author)}`;
        link.textContent = author;
        $('.r-c-text', el).textContent = text;
        return el;
    }

    // social.js adds a freshly posted comment with its generic Reddit markup; swap it for the real thread markup
    function watchComments() {
        const section = $('.comments-section');
        if (!section) return;
        new MutationObserver(mutations => mutations.forEach(m => m.addedNodes.forEach(node => {
            if (node.nodeType !== 1 || !node.classList.contains('comment')) return;
            const text = ($('.comment-text', node) || node).textContent.trim();
            node.replaceWith(commentElement(me, text));
            const count = $('[data-r-comment-count]');
            if (count) {
                const n = (parseInt(count.dataset.rCommentCount, 10) || 0) + 1;
                count.dataset.rCommentCount = String(n);
                count.textContent = compact(n);
            }
            const box = $('.comment-input-container');
            if (box) box.removeAttribute('data-open');
        }))).observe(section, { childList: true });
    }

    function sortComments(how) {
        const section = $('.comments-section');
        if (!section) return;
        const score = c => parseInt(c.dataset.score, 10) || 0;
        const created = c => parseInt(c.dataset.created, 10) || 0;
        const order = {
            best: (a, b) => score(b) - score(a),
            top: (a, b) => score(b) - score(a),
            new: (a, b) => created(b) - created(a),
            old: (a, b) => created(a) - created(b),
            controversial: (a, b) => score(a) - score(b)
        }[how];
        $$(':scope > .r-comment', section).sort(order).forEach(c => section.append(c));
    }

    function openComposer() {
        const box = $('.comment-input-container');
        if (!box) return;
        box.dataset.open = '';
        const input = $('.comment-input', box);
        input.focus();
        box.scrollIntoView({ block: 'center', behavior: 'smooth' });
    }

    // ---------- Clicks ----------
    document.addEventListener('click', e => {
        const target = e.target;
        const control = target.closest('[data-r]');
        const action = control && control.dataset.r;

        if (!target.closest('.r-pop, .r-menu') && action !== 'menu') closeMenus();

        if (target.closest('.r-vote button')) { e.preventDefault(); onVote(target.closest('.r-vote button')); return; }

        switch (action) {
            case 'menu': {
                const menu = control.parentElement.querySelector('.r-menu');
                const open = !menu.hasAttribute('data-open');
                closeMenus();
                if (open) { menu.dataset.open = ''; control.setAttribute('aria-expanded', 'true'); }
                return;
            }
            case 'nav':
                if (document.body.hasAttribute('data-nav-open')) document.body.removeAttribute('data-nav-open');
                else document.body.dataset.navOpen = '';
                return;
            case 'mode': {
                const dark = document.documentElement.dataset.mode !== 'dark';
                if (dark) document.documentElement.dataset.mode = 'dark'; else delete document.documentElement.dataset.mode;
                try { localStorage.setItem('r-mode', dark ? 'dark' : 'light'); } catch (err) { }
                return;
            }
            case 'create': {
                const title = $('#create-post .post-title');
                if (title) { e.preventDefault(); title.scrollIntoView({ block: 'center', behavior: 'smooth' }); title.focus({ preventScroll: true }); }
                return;
            }
            case 'more': {
                e.preventDefault();
                const item = control.closest('.r-item, .r-comment, .r-detail');
                popMenu(control, [
                    ['i-bookmark', 'Save', () => toast('Post saved')],
                    ['i-hide', 'Hide', () => { if (item && item.matches('.r-item')) item.remove(); toast('Post hidden'); }],
                    ['i-flag', 'Report', () => toast('Thanks for reporting. We’ll review it shortly.')]
                ]);
                return;
            }
            case 'share':
                e.preventDefault();
                popMenu(control, [
                    ['i-link', 'Copy link', () => copyLink(control.dataset.url)],
                    ['i-share', 'Crosspost', () => { location.href = '/#create-post'; }]
                ]);
                return;
            case 'join': {
                const set = joined();
                const name = control.dataset.community;
                if (set.has(name)) set.delete(name); else set.add(name);
                store.set('r-joined', Array.from(set));
                $$(`[data-r="join"]`).filter(b => b.dataset.community === name).forEach(b => paintJoin(b, set.has(name)));
                return;
            }
            case 'clear': {
                const card = control.closest('[data-r-recent]');
                if (card) card.remove();
                return;
            }
            case 'layout':
                popMenu(control, [
                    ['i-card', 'Card', () => setLayout('card')],
                    ['i-menu', 'Compact', () => setLayout('compact')]
                ]);
                return;
            case 'reply':
            case 'comment':
                e.preventDefault();
                openComposer();
                return;
            case 'cancel': {
                const box = control.closest('.comment-input-container');
                $('.comment-input', box).value = '';
                box.removeAttribute('data-open');
                return;
            }
            case 'back':
                if (history.length > 1 && document.referrer.startsWith(location.origin)) { e.preventDefault(); history.back(); }
                return;
        }

        const chip = target.closest('[data-sort]');
        if (chip) {
            $$('[data-sort]').forEach(c => c.setAttribute('aria-pressed', c === chip ? 'true' : 'false'));
            sortFeed(chip.dataset.sort);
            return;
        }

        const tab = target.closest('.post-type-tabs button');
        if (tab) {
            $$('button', tab.parentElement).forEach(t => t.setAttribute('aria-selected', t === tab ? 'true' : 'false'));
            return;
        }

        const line = target.closest('.r-c-line, .r-c-expand');
        if (line) {
            const comment = line.closest('.r-comment');
            if (comment.hasAttribute('data-collapsed')) comment.removeAttribute('data-collapsed'); else comment.dataset.collapsed = '';
            return;
        }

        if (target.closest('a[href="#"]')) { e.preventDefault(); return; }

        // A click anywhere else on a post card opens the post, as on Reddit
        const card = target.closest('[data-href]');
        if (card && !target.closest('a, button, input, textarea, select, label')) {
            location.href = card.dataset.href;
        }
    });

    document.addEventListener('keydown', e => { if (e.key === 'Escape') { closeMenus(); document.body.removeAttribute('data-nav-open'); } });

    document.addEventListener('focusin', e => {
        if (e.target.matches('.comment-input-container .comment-input')) e.target.closest('.comment-input-container').dataset.open = '';
    });

    document.addEventListener('input', e => {
        const t = e.target;
        if (t.matches('.post-composer .post-title')) {
            const count = t.parentElement.querySelector('.r-count');
            if (count) count.textContent = `${t.value.length}/300`;
        } else if (t.matches('.chat-input')) {
            t.style.height = 'auto';
            t.style.height = Math.min(t.scrollHeight, 120) + 'px';
        } else if (t.matches('#r-csearch')) {
            const q = t.value.trim().toLowerCase();
            $$('.comments-section > .r-comment').forEach(c => { c.hidden = q !== '' && !c.textContent.toLowerCase().includes(q); });
        }
    });

    // The post goes out as a normal form post (the server answers 204 and the feed gets it over SignalR);
    // once the browser has taken the values, empty the composer as Reddit does after posting
    document.addEventListener('submit', e => {
        const form = e.target;
        if (!form.closest('#create-post')) return;
        setTimeout(() => {
            form.reset();
            const count = form.querySelector('.r-count');
            if (count) count.textContent = '0/300';
        }, 0);
    });

    document.addEventListener('change', e => {
        if (e.target.matches('#r-csort')) sortComments(e.target.value);
    });

    document.addEventListener('keydown', e => {
        if (e.target.matches('.chat-input') && e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            if (e.target.value.trim()) e.target.form.requestSubmit();
        }
    });

    // ---------- Start ----------
    const set = joined();
    $$('[data-r="join"]').forEach(b => paintJoin(b, set.has(b.dataset.community)));
    setLayout(store.get('r-layout', 'card'));
    watchComments();
    const scroll = $('.r-chat-scroll');
    if (scroll) scroll.scrollTop = scroll.scrollHeight;
    if (location.hash === '#create-post') {
        const title = $('#create-post .post-title');
        if (title) title.focus();
    }
    if (location.hash === '#comments') openComposer();
})();
