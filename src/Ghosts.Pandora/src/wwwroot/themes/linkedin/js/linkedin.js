// LinkedIn theme behaviour: menus, the composer, "see more", reaction counts, follow buttons and toasts.
// Posting, liking and commenting themselves stay with /js/social.js; this only presents them the way LinkedIn does.
(function () {
    'use strict';

    const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
    const icon = (id, size) => `<svg width="${size || 24}" height="${size || 24}"><use href="#${id}"/></svg>`;

    let people = {};
    try {
        const data = document.getElementById('li-people');
        if (data) people = JSON.parse(data.textContent) || {};
    } catch (e) { people = {}; }

    // Used by the live post template in social.js
    window.LinkedInTheme = {
        name: user => escapeHtml((people[user] || [])[0] || user),
        headline: user => escapeHtml((people[user] || [])[1] || ''),
        icon
    };

    function toast(message) {
        document.querySelectorAll('.li-toast').forEach(t => t.remove());
        const el = document.createElement('div');
        el.className = 'li-toast';
        el.setAttribute('role', 'status');
        el.innerHTML = `<span class="li-toast__icon">${icon('li-check', 16)}</span><span class="li-toast__text">${escapeHtml(message)}</span>` +
            `<button type="button" class="li-iconbtn li-toast__close" aria-label="Dismiss">${icon('li-close', 16)}</button>`;
        document.body.appendChild(el);
        el.querySelector('.li-toast__close').addEventListener('click', () => el.remove());
        setTimeout(() => el.classList.add('li-toast--in'), 10);
        setTimeout(() => el.remove(), 5000);
    }
    window.LinkedInTheme.toast = toast;

    // ---- dropdown menus -------------------------------------------------------------------
    function closeMenus(except) {
        document.querySelectorAll('.li-popover').forEach(m => { if (m !== except) m.remove(); });
        document.querySelectorAll('[data-li-menu].li-open').forEach(m => { if (!m.contains(except)) m.classList.remove('li-open'); });
    }

    function popover(anchor, items) {
        closeMenus();
        const menu = document.createElement('div');
        menu.className = 'li-popover';
        menu.setAttribute('role', 'menu');
        items.forEach(item => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'li-popover__item';
            button.innerHTML = (item.icon ? icon(item.icon) : '') +
                `<span><strong>${escapeHtml(item.title)}</strong>${item.detail ? `<small>${escapeHtml(item.detail)}</small>` : ''}</span>`;
            button.addEventListener('click', e => {
                e.stopPropagation();
                menu.remove();
                item.run();
            });
            menu.appendChild(button);
        });
        anchor.parentElement.appendChild(menu);
        return menu;
    }

    function postUrl(post) {
        return `${location.origin}/posts/${post.dataset.postId}`;
    }

    function copyLink(post) {
        const url = postUrl(post);
        if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(url).catch(() => {});
        toast('Link copied to clipboard.');
    }

    function dismiss(post) {
        const author = post.querySelector('.li-post__display');
        const placeholder = document.createElement('div');
        placeholder.className = 'li-card li-post-hidden';
        placeholder.innerHTML = `<div><strong>Post removed</strong><span>You won’t see this post in your feed.</span></div><button type="button" class="li-btn li-btn--outline">Undo</button>`;
        placeholder.querySelector('button').addEventListener('click', () => {
            placeholder.replaceWith(post);
        });
        post.replaceWith(placeholder);
        if (author) placeholder.dataset.author = author.textContent;
    }

    document.addEventListener('click', e => {
        const target = e.target;

        if (target.closest('.li-inert')) {
            e.preventDefault();
        }

        const meMenu = target.closest('[data-li-menu]');
        if (meMenu && !target.closest('.li-menu a')) {
            const open = !meMenu.classList.contains('li-open');
            closeMenus();
            meMenu.classList.toggle('li-open', open);
            e.stopPropagation();
            return;
        }

        const post = target.closest('.li-post');

        const menuButton = target.closest('[data-li-postmenu]');
        if (menuButton && post) {
            e.stopPropagation();
            const name = (post.querySelector('.li-post__display') || {}).textContent || 'this person';
            const own = /You/.test((post.querySelector('.li-post__degree') || {}).textContent || '');
            if (own) {
                popover(menuButton, [
                    { icon: 'li-star', title: 'Feature on top of profile', run: () => toast('Post featured on your profile.') },
                    { icon: 'li-bookmark', title: 'Save', detail: 'Save for later', run: () => toast('Post saved. See your saved posts in My items.') },
                    { icon: 'li-send', title: 'Copy link to post', run: () => copyLink(post) },
                    { icon: 'li-globe', title: 'Who can see this post?', detail: 'Visible to anyone on or off LinkedIn', run: () => toast('This post is visible to anyone on or off LinkedIn.') }
                ]);
                return;
            }
            popover(menuButton, [
                { icon: 'li-bookmark', title: 'Save', detail: 'Save for later', run: () => toast('Post saved. See your saved posts in My items.') },
                { icon: 'li-send', title: 'Copy link to post', run: () => copyLink(post) },
                { icon: 'li-close', title: 'Not interested', detail: 'See fewer posts like this', run: () => dismiss(post) },
                { icon: 'li-network', title: `Unfollow ${name}`, detail: 'Stop seeing posts from this person but stay connected', run: () => { dismiss(post); toast(`You unfollowed ${name}.`); } },
                { icon: 'li-info', title: 'Report post', detail: 'This post is offensive or the account is hacked', run: () => toast('Thanks for letting us know. We’ll review this post.') }
            ]);
            return;
        }

        const dismissButton = target.closest('[data-li-dismiss]');
        if (dismissButton && post) {
            dismiss(post);
            return;
        }

        const repost = target.closest('[data-li-repost]');
        if (repost && post) {
            e.stopPropagation();
            popover(repost, [
                {
                    icon: 'li-pencil', title: 'Repost with your thoughts', detail: 'Create a new post with this post attached', run: () => {
                        const composer = document.querySelector('[data-li-composer] textarea');
                        if (composer) {
                            composer.focus();
                            composer.value = `${composer.value}${composer.value ? '\n' : ''}${postUrl(post)}`;
                            composer.dispatchEvent(new Event('input'));
                        } else {
                            toast('Reposted. Your network can now see this post.');
                        }
                    }
                },
                { icon: 'li-repost', title: 'Repost', detail: 'Instantly bring this post to others’ feeds', run: () => toast('Reposted successfully.') }
            ]);
            return;
        }

        const send = target.closest('[data-li-send]');
        if (send && post) {
            e.stopPropagation();
            popover(send, [
                { icon: 'li-messaging', title: 'Send in a private message', run: () => { location.href = '/messages'; } },
                { icon: 'li-send', title: 'Copy link to post', run: () => copyLink(post) }
            ]);
            return;
        }

        const profileMore = target.closest('[data-li-profilemore]');
        if (profileMore) {
            e.stopPropagation();
            const name = (document.querySelector('.li-profile-top h1') || {}).textContent || 'this member';
            popover(profileMore, [
                { icon: 'li-send', title: 'Send profile in a message', run: () => { location.href = '/messages'; } },
                { icon: 'li-article', title: 'Save to PDF', run: () => window.print() },
                { icon: 'li-info', title: 'About this profile', run: () => toast(`${name} joined LinkedIn and verified their workplace.`) },
                { icon: 'li-close', title: 'Report / Block', run: () => toast('Thanks for letting us know. We’ll review this profile.') }
            ]);
            return;
        }

        const activityAll = target.closest('[data-li-activity-all]');
        if (activityAll) {
            const list = document.querySelector('[data-li-activity]');
            if (list) list.classList.add('li-activity__list--all');
            activityAll.remove();
            return;
        }

        const seeMore = target.closest('[data-li-seemore]');
        if (seeMore) {
            seeMore.closest('.li-post__body').classList.remove('li-post__body--clamp');
            seeMore.remove();
            return;
        }

        const newsToggle = target.closest('[data-li-news-toggle]');
        if (newsToggle) {
            const card = newsToggle.closest('.li-news');
            const open = card.classList.toggle('li-news--open');
            newsToggle.firstChild.textContent = open ? 'Show less ' : 'Show more ';
            return;
        }

        const focusComment = target.closest('[data-li-focus-comment]');
        if (focusComment) {
            e.preventDefault();
            const input = document.querySelector('.comment-input');
            if (input) input.focus();
            return;
        }

        const commentLike = target.closest('.comments-list .like-action');
        if (commentLike) {
            commentLike.classList.toggle('liked');
            return;
        }

        const reply = target.closest('.comments-list .reply-action');
        if (reply) {
            const input = document.querySelector('.comment-input');
            const author = reply.closest('.comment').querySelector('.comment-author a, .comment-author');
            if (input) {
                const name = author ? author.textContent.trim() : '';
                if (name && !input.value.startsWith(name)) input.value = `${name} ${input.value}`;
                input.focus();
            }
            return;
        }

        if (target.closest('[data-li-compose]')) {
            const composer = document.querySelector('[data-li-composer] textarea');
            if (composer) composer.focus();
            return;
        }

        if (!target.closest('.li-popover') && !target.closest('[data-li-menu]')) closeMenus();
    });

    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') closeMenus();
    });

    // ---- composer -------------------------------------------------------------------------
    function initComposer() {
        const box = document.querySelector('[data-li-composer]');
        if (!box) return;
        const form = box.querySelector('form');
        const textarea = box.querySelector('textarea');

        const grow = () => {
            textarea.style.height = 'auto';
            if (box.classList.contains('li-composer--open')) textarea.style.height = Math.max(textarea.scrollHeight, 96) + 'px';
            else textarea.style.height = '';
        };
        const open = () => {
            box.classList.add('li-composer--open');
            textarea.placeholder = 'What do you want to talk about?';
            grow();
        };
        const close = () => {
            if (textarea.value.trim()) return;
            box.classList.remove('li-composer--open');
            textarea.placeholder = 'Start a post';
            grow();
        };

        textarea.addEventListener('focus', open);
        textarea.addEventListener('input', () => { open(); grow(); });
        textarea.addEventListener('blur', () => setTimeout(() => { if (!box.contains(document.activeElement)) close(); }, 150));
        if (location.hash === '#li-composer') textarea.focus();

        // The form posts as before and the server answers 204, so the page stays; the new post arrives live
        form.addEventListener('submit', () => {
            setTimeout(() => {
                textarea.value = '';
                if (document.activeElement && box.contains(document.activeElement)) document.activeElement.blur();
                close();
                toast('Your post is now live.');
            }, 50);
        });
    }

    // ---- post text: hashtags as links -----------------------------------------------------
    function linkHashtags(root) {
        (root || document).querySelectorAll('.li-text:not([data-li-tags])').forEach(text => {
            text.dataset.liTags = '1';
            const walker = document.createTreeWalker(text, NodeFilter.SHOW_TEXT);
            const nodes = [];
            while (walker.nextNode()) nodes.push(walker.currentNode);
            nodes.forEach(node => {
                if (!/#\w/.test(node.nodeValue)) return;
                const fragment = document.createDocumentFragment();
                node.nodeValue.split(/(#\w+)/).forEach(part => {
                    if (/^#\w+$/.test(part)) {
                        const a = document.createElement('a');
                        a.className = 'li-hashtag';
                        a.href = `/search?q=${encodeURIComponent(part)}`;
                        a.textContent = part;
                        fragment.appendChild(a);
                    } else if (part) {
                        fragment.appendChild(document.createTextNode(part));
                    }
                });
                node.replaceWith(fragment);
            });
        });
    }

    // ---- reactions: after social.js toggles "liked", show LinkedIn's label and counts -------
    function syncLike(button) {
        const liked = button.classList.contains('liked');
        const label = button.querySelector('.action-text');
        if (label) label.textContent = 'Like';
        const post = button.closest('.li-post');
        const counts = post && post.querySelector('[data-li-counts]');
        if (!counts) return;
        const others = parseInt(counts.dataset.others, 10) || 0;
        const total = others + (liked ? 1 : 0);
        const count = counts.querySelector('[data-li-reactcount]');
        if (count) count.textContent = liked ? (others === 0 ? 'You' : `You and ${others.toLocaleString('en-US')} other${others === 1 ? '' : 's'}`) : total.toLocaleString('en-US');
        counts.dataset.liked = liked ? 'true' : 'false';
        counts.querySelector('.li-post__reactions').classList.toggle('li-post__reactions--none', total === 0);
        const engage = counts.querySelector('.li-post__engage');
        counts.classList.toggle('li-post__counts--empty', total === 0 && !(engage && engage.textContent.trim()));
    }

    document.addEventListener('click', e => {
        const like = e.target.closest('.like-it');
        if (like) setTimeout(() => syncLike(like), 0);
    });

    // ---- follow buttons -------------------------------------------------------------------
    document.addEventListener('submit', e => {
        const form = e.target.closest('.li-follow');
        if (!form) return;
        e.preventDefault();
        const button = form.querySelector('button');
        if (button.disabled || form.classList.contains('li-follow--done')) return;
        button.disabled = true;
        const toggle = form.classList.contains('li-follow--toggle');
        const unfollowing = toggle && form.classList.contains('li-follow--on');
        fetch(form.getAttribute('action'), { method: 'POST', body: new FormData(form), credentials: 'same-origin' })
            .then(resp => {
                if (!resp.ok) throw new Error();
                const holder = form.closest('li, .li-post, .li-profile-top');
                const who = holder && holder.querySelector('.li-follow-list__name, .li-post__display, h1');
                const name = who ? who.firstChild.textContent.trim() : 'them';
                if (toggle) {
                    form.classList.toggle('li-follow--on', !unfollowing);
                    form.setAttribute('action', unfollowing ? '/api/relationships/follow' : '/api/relationships/unfollow');
                    toast(unfollowing ? `You unfollowed ${name}.` : `You’re now following ${name}.`);
                    return;
                }
                form.classList.add('li-follow--done');
                if (form.dataset.liDone) {
                    button.innerHTML = `<span>${escapeHtml(form.dataset.liDone)}</span>`;
                    toast(`Your invitation to ${name} was sent.`);
                } else {
                    button.innerHTML = `${icon('li-check', 16)}<span>Following</span>`;
                    toast(`You’re now following ${name}.`);
                }
            })
            .catch(() => toast('Something went wrong. Please try again.'))
            .finally(() => { button.disabled = false; });
    });

    // ---- comments added by social.js: give them the member's name and headline ------------
    function initComments() {
        const list = document.querySelector('.comments-list');
        if (!list) return;
        const me = (document.querySelector('meta[name="current-user"]') || {}).content || '';
        const name = (document.querySelector('meta[name="current-user-name"]') || {}).content || me;
        const headline = (document.querySelector('meta[name="current-user-headline"]') || {}).content || '';
        new MutationObserver(records => {
            records.forEach(record => record.addedNodes.forEach(node => {
                if (!(node instanceof HTMLElement) || !node.classList.contains('comment') || node.dataset.liReady) return;
                node.dataset.liReady = '1';
                const author = node.querySelector('.comment-author');
                if (author) author.innerHTML = `<a href="/u/${encodeURIComponent(me)}">${escapeHtml(name)}</a><span class="comment-degree"> &bull; You</span>`;
                const title = node.querySelector('.comment-title');
                if (title) title.textContent = headline;
                const time = node.querySelector('.comment-time');
                if (time) time.textContent = 'now';
                const menu = node.querySelector('.comment-menu');
                if (menu) menu.innerHTML = icon('li-more', 16);
                const avatar = node.querySelector('.comment-avatar');
                if (avatar) avatar.alt = '';
                const count = document.querySelector('[data-li-comment-count]');
                if (count) {
                    const n = (parseInt(count.dataset.liCommentCount, 10) || 0) + 1;
                    count.dataset.liCommentCount = n;
                    count.textContent = `${n} comment${n === 1 ? '' : 's'}`;
                    count.classList.remove('li-hide');
                    const counts = count.closest('[data-li-counts]');
                    if (counts) counts.classList.remove('li-post__counts--empty');
                }
            }));
        }).observe(list, { childList: true });
    }

    // ---- live posts from SignalR: wire them up like the rest ------------------------------
    function initLiveFeed() {
        const feed = document.getElementById('newsfeed-items-grid');
        if (!feed) return;
        new MutationObserver(() => linkHashtags(feed)).observe(feed, { childList: true });
    }

    // ---- messaging: Enter sends, the thread opens at its newest message, search filters the list
    function initMessaging() {
        const scroll = document.querySelector('[data-li-msgscroll]');
        if (scroll) scroll.scrollTop = scroll.scrollHeight;

        document.querySelectorAll('[data-li-msgform] textarea').forEach(textarea => {
            textarea.addEventListener('keydown', e => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    if (textarea.value.trim()) textarea.form.requestSubmit();
                }
            });
        });
        const thread = document.querySelector('.li-msg__thread [data-li-msgform]:not(.li-msg__compose--new) textarea');
        if (thread && window.matchMedia('(min-width: 768px)').matches) thread.focus();

        const search = document.querySelector('[data-li-msg-search]');
        if (search) {
            search.addEventListener('input', () => {
                const q = search.value.trim().toLowerCase();
                document.querySelectorAll('[data-li-thread]').forEach(item => {
                    item.hidden = q && !item.dataset.liThread.includes(q);
                });
            });
        }
    }

    function init() {
        initMessaging();
        initComposer();
        linkHashtags();
        initComments();
        initLiveFeed();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
