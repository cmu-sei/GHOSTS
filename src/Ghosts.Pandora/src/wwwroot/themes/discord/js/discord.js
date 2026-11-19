// Discord theme behaviour: tooltips, popovers and menus, channel switching, local timestamps, message
// grouping for live messages, replies, the expression picker, uploads and the friends and profile pages.
// Posting, liking, commenting and DMs stay with social.js and the page forms; this only adds the client.
(function () {
    'use strict';

    const $$ = (selector, root) => Array.from((root || document).querySelectorAll(selector));
    const body = document.body;
    const me = body.dataset.me || '';
    const meName = body.dataset.meName || me;
    const GROUP_MS = 7 * 60 * 1000;

    // ---------- timestamps, in the reader's time zone ----------

    const clock = d => d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' }).replace(/\u202f/g, ' ');
    const dayStart = d => new Date(d.getFullYear(), d.getMonth(), d.getDate());
    const longDate = d => d.toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' });

    function stamp(d) {
        const days = Math.round((dayStart(new Date()) - dayStart(d)) / 86400000);
        if (days === 0) return `Today at ${clock(d)}`;
        if (days === 1) return `Yesterday at ${clock(d)}`;
        return `${d.getMonth() + 1}/${d.getDate()}/${String(d.getFullYear()).slice(-2)}, ${clock(d)}`;
    }

    function ago(d) {
        const s = (Date.now() - d) / 1000;
        if (s < 60) return 'just now';
        if (s < 3600) return `${Math.floor(s / 60)}m ago`;
        if (s < 86400) return `${Math.floor(s / 3600)}h ago`;
        if (s < 7 * 86400) return `${Math.floor(s / 86400)}d ago`;
        return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }

    function formatTimes(root) {
        $$('time[data-f]', root).forEach(t => {
            const d = new Date(t.getAttribute('datetime'));
            if (isNaN(d)) return;
            const f = t.dataset.f;
            t.textContent = f === 'time' ? clock(d) : f === 'day' ? d.toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' }) : f === 'ago' ? ago(d) : stamp(d);
            if (f === 'stamp' || f === 'time') t.dataset.tip = `${longDate(d)} at ${clock(d)}`;
        });
    }

    // ---------- tooltips ----------

    const tooltip = document.createElement('div');
    tooltip.className = 'dc-tooltip';
    tooltip.hidden = true;
    body.appendChild(tooltip);

    document.addEventListener('mouseover', e => {
        const el = e.target.closest('[data-tip]');
        if (!el || el.classList.contains('open') || matchMedia('(hover: none)').matches) { tooltip.hidden = true; return; }
        tooltip.textContent = el.dataset.tip;
        tooltip.hidden = false;
        const r = el.getBoundingClientRect();
        const t = tooltip.getBoundingClientRect();
        let top, left;
        if (el.dataset.tipSide === 'right') {
            top = r.top + r.height / 2 - t.height / 2;
            left = r.right + 12;
        } else {
            top = r.top - t.height - 8;
            left = r.left + r.width / 2 - t.width / 2;
            if (top < 4) top = r.bottom + 8;
        }
        tooltip.style.top = `${Math.max(4, top)}px`;
        tooltip.style.left = `${Math.min(Math.max(4, left), innerWidth - t.width - 4)}px`;
    });
    document.addEventListener('mouseout', e => {
        if (e.target.closest('[data-tip]') && !e.relatedTarget?.closest?.('[data-tip]')) tooltip.hidden = true;
    });

    // ---------- popovers ----------

    let openTrigger = null;

    function closePopovers() {
        $$('.dc-popover').forEach(p => { p.hidden = true; });
        $$('.open').forEach(t => t.classList.remove('open'));
        $$('.message.menu-open').forEach(m => m.classList.remove('menu-open'));
        openTrigger = null;
    }

    function place(pop, trigger) {
        pop.hidden = false;
        const r = trigger.getBoundingClientRect();
        const w = pop.offsetWidth, h = pop.offsetHeight;
        let top = r.bottom + 8;
        if (top + h > innerHeight - 8) top = Math.max(8, r.top - h - 8);
        let left = r.left + r.width / 2 < innerWidth / 2 ? r.left : r.right - w;
        left = Math.min(Math.max(8, left), innerWidth - w - 8);
        pop.style.top = `${top}px`;
        pop.style.left = `${left}px`;
    }

    function openPopover(pop, trigger) {
        const wasOpen = openTrigger === trigger;
        closePopovers();
        if (wasOpen) return;
        tooltip.hidden = true;
        trigger.classList.add('open');
        openTrigger = trigger;
        place(pop, trigger);
    }

    document.addEventListener('click', e => {
        const inert = e.target.closest('[data-inert]');
        if (inert) e.preventDefault();

        const trigger = e.target.closest('[data-popover]');
        if (trigger) {
            e.preventDefault();
            openPopover(document.getElementById(trigger.dataset.popover), trigger);
            return;
        }
        if (!e.target.closest('.dc-popover') || inert || e.target.closest('.dc-menu-item')) closePopovers();
    });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') closePopovers(); });

    // radio items in a menu, such as the notification settings
    document.addEventListener('click', e => {
        const radio = e.target.closest('[data-radio]');
        if (!radio) return;
        $$('[data-radio]', radio.parentElement).forEach(r => r.classList.toggle('selected', r === radio));
    }, true);

    // plain tab strips; a tab with data-tab shows the matching [data-tab-panel]
    document.addEventListener('click', e => {
        const tab = e.target.closest('.dc-tab');
        if (!tab) return;
        $$('.dc-tab', tab.parentElement).forEach(t => t.classList.toggle('selected', t === tab));
        if (tab.dataset.tab) {
            const scope = tab.closest('[data-tabs-scope]') || document;
            $$('[data-tab-panel]', scope).forEach(p => { p.hidden = !p.dataset.tabPanel.split(' ').includes(tab.dataset.tab); });
            scope.dispatchEvent(new CustomEvent('dc-tab', { detail: tab.dataset.tab }));
        }
    });

    // ---------- sidebar, drawers, member list ----------

    document.addEventListener('click', e => {
        const head = e.target.closest('.dc-category-head');
        if (head && !e.target.closest('.dc-category-add')) head.parentElement.classList.toggle('collapsed');

        const pressed = e.target.closest('[data-toggle-pressed]');
        if (pressed) pressed.classList.toggle('pressed');

        const drawer = e.target.closest('[data-drawer]');
        if (drawer) document.querySelector('.dc-app').classList.toggle('nav-open');
        if (e.target.closest('[data-drawer-close]')) document.querySelector('.dc-app').classList.remove('nav-open', 'members-open');

        const members = e.target.closest('[data-toggle-members]');
        if (members) {
            if (matchMedia('(max-width: 860px)').matches) {
                document.querySelector('.dc-app').classList.toggle('members-open');
            } else {
                const chat = document.querySelector('.dc-chat');
                const hidden = chat.classList.toggle('members-hidden');
                members.classList.toggle('selected', !hidden);
                members.dataset.tip = members.dataset.tip.replace(hidden ? 'Hide' : 'Show', hidden ? 'Show' : 'Hide');
                tooltip.textContent = members.dataset.tip;
            }
        }
    });

    $$('.dc-search-input').forEach(input => input.addEventListener('keydown', e => {
        if (e.key === 'Enter' && input.value.trim()) location.href = `/search?q=${encodeURIComponent(input.value.trim())}`;
    }));

    // ---------- channels: the feed is #general; the other text channels have no history yet ----------

    const chat = document.querySelector('.dc-chat[data-channel]');

    function showChannel(link) {
        const key = link.dataset.channel;
        chat.dataset.channel = key;
        $$('.dc-channel').forEach(c => c.classList.toggle('selected', c === link));
        link.classList.remove('unread');
        chat.querySelector('.dc-chat-title').textContent = key;
        chat.querySelector('.dc-chat-icon use').setAttribute('href', `#${link.dataset.icon}`);
        chat.querySelector('.dc-chat-topic').textContent = link.dataset.topic;
        $$('.dc-channel-start-name', chat).forEach(n => { n.textContent = key; });
        const input = chat.querySelector('.message-input');
        if (input) input.placeholder = key === 'announcements' ? 'You do not have permission to send messages in this channel.' : `Message #${key}`;
        if (input) input.disabled = key === 'announcements';
        document.title = `Discord | #${key} | ${body.dataset.server || ''}`;
        document.querySelector('.dc-app').classList.remove('nav-open');
    }

    if (chat) {
        document.addEventListener('click', e => {
            const link = e.target.closest('.dc-channel[data-channel]');
            if (!link) return;
            e.preventDefault();
            showChannel(link);
            history.replaceState(null, '', link.dataset.channel === 'general' ? '/' : `/#${link.dataset.channel}`);
        });
        const fromHash = location.hash && document.querySelector(`.dc-channel[data-channel="${CSS.escape(location.hash.slice(1))}"]`);
        if (fromHash) showChannel(fromHash);
    }

    // ---------- messages ----------

    // the name a message shows under: its own header, or the header of the group it belongs to, which in the
    // column-reverse list comes later in the markup
    function authorName(message) {
        const start = message.parentElement.matches('.messages-list') ? message : message.parentElement;
        for (let n = start; n; n = n.nextElementSibling) {
            const name = (n.matches('.message') ? n : n.querySelector(':scope > .message'))?.querySelector('.message-author');
            if (name) return name.textContent;
        }
        return '';
    }

    function copy(text) {
        if (navigator.clipboard && isSecureContext) return navigator.clipboard.writeText(text);
        const area = document.createElement('textarea');
        area.value = text;
        body.appendChild(area);
        area.select();
        document.execCommand('copy');
        area.remove();
    }

    // reply: Discord shows "Replying to" above the composer; the reply itself posts like any message
    document.addEventListener('click', e => {
        const reply = e.target.closest('[data-reply]');
        if (reply) {
            const message = reply.closest('.message');
            const composer = document.querySelector('.message-input-container');
            if (!composer) return;
            composer.classList.add('replying');
            composer.querySelector('.dc-reply-bar').hidden = false;
            composer.querySelector('.dc-reply-name').textContent = authorName(message);
            composer.querySelector('.message-input, .comment-input, .conversation-input')?.focus();
        }
        if (e.target.closest('[data-reply-cancel]')) {
            const composer = e.target.closest('.message-input-container');
            composer.classList.remove('replying');
            composer.querySelector('.dc-reply-bar').hidden = true;
        }
    });

    const menu = document.createElement('div');
    menu.className = 'dc-popover dc-menu';
    menu.id = 'dc-message-menu';
    menu.hidden = true;
    body.appendChild(menu);

    document.addEventListener('click', e => {
        const more = e.target.closest('[data-message-menu]');
        if (!more) return;
        e.stopPropagation();
        const message = more.closest('.message');
        const id = message.dataset.postId;
        const items = [['Reply', 'i-reply', () => message.querySelector('[data-reply]').click()]];
        if (id) items.push([message.querySelector('.dc-thread-embed') ? 'Open Thread' : 'Create Thread', 'i-threads', () => { location.href = `/posts/${id}`; }]);
        items.push(['sep'], ['Copy Text', 'i-copy', () => copy(message.querySelector('.message-text').innerText)]);
        if (id) items.push(['Copy Message Link', 'i-link', () => copy(`${location.origin}/posts/${id}`)], ['Copy Message ID', 'i-id', () => copy(id)]);
        items.push(['sep'], ['Mark Unread', 'i-unread', () => {}]);
        menu.replaceChildren(...items.map(([label, icon, run]) => {
            if (label === 'sep') return Object.assign(document.createElement('div'), { className: 'dc-menu-sep' });
            const item = document.createElement('button');
            item.type = 'button';
            item.className = 'dc-menu-item';
            item.innerHTML = `<span></span><svg class="ic" width="18" height="18"><use href="#${icon}"/></svg>`;
            item.firstChild.textContent = label;
            item.addEventListener('click', run);
            return item;
        }));
        openPopover(menu, more);
        if (!menu.hidden) message.classList.add('menu-open');
    }, true);

    // a reaction chip adds your reaction through the message's like button, which social.js handles
    document.addEventListener('click', e => {
        const chip = e.target.closest('.reaction');
        if (!chip || chip.classList.contains('me')) return;
        chip.closest('.message')?.querySelector('.like-it')?.click();
    });
    document.addEventListener('click', e => {
        const like = e.target.closest('.message .like-it');
        if (!like) return;
        setTimeout(() => {
            const chip = like.closest('.message').querySelector('.reaction[data-emoji="❤️"]');
            if (chip) chip.classList.add('me');
        });
    });

    // ---------- grouping live messages (social.js prepends them to the column-reverse list) ----------

    function messageAfter(node) {
        for (let n = node.nextElementSibling; n; n = n.nextElementSibling) {
            if (n.matches('.dc-divider, .dc-channel-start')) return null;
            const m = n.matches('.message') ? n : n.querySelector(':scope > .message');
            if (m) return m;
        }
        return null;
    }

    function fitLiveMessage(wrapper) {
        const message = wrapper.querySelector('.message');
        if (!message) return;
        const user = message.dataset.author;
        const member = document.querySelector(`.dc-member[href="/u/${CSS.escape(user)}"] .dc-member-name`);
        const author = message.querySelector('.message-author');
        if (member && author) {
            author.textContent = member.textContent;
            author.className = member.className.replace('dc-member-name', 'message-author');
        }
        const when = new Date(message.dataset.time);
        const previous = messageAfter(wrapper);
        const previousTime = previous ? new Date(previous.dataset.time) : null;
        if (previousTime && dayStart(previousTime).getTime() !== dayStart(when).getTime()) {
            const divider = document.createElement('div');
            divider.className = 'dc-divider';
            divider.innerHTML = `<time data-f="day"></time>`;
            divider.firstChild.setAttribute('datetime', message.dataset.time);
            wrapper.after(divider);
        } else if (previous && previous.dataset.author === user && when - previousTime < GROUP_MS) {
            message.classList.add('grouped');
            message.querySelector('.message-avatar-link')?.remove();
            message.querySelector('.message-header')?.remove();
            const gutter = document.createElement('time');
            gutter.className = 'message-gutter-time';
            gutter.dataset.f = 'time';
            gutter.setAttribute('datetime', message.dataset.time);
            message.prepend(gutter);
        }
        formatTimes(wrapper.parentElement);
    }

    const feed = document.getElementById('newsfeed-items-grid');
    if (feed) {
        new MutationObserver(records => records.forEach(r => r.addedNodes.forEach(n => {
            if (n.nodeType === 1 && n.parentElement === feed && !n.matches('.message, .dc-divider')) fitLiveMessage(n);
        }))).observe(feed, { childList: true });
    }

    // ---------- thread replies: social.js posts the comment, this shows it ----------

    if (window.jQuery) {
        jQuery(document).ajaxSuccess((event, xhr, settings) => {
            const list = document.querySelector('.dc-thread-replies');
            if (!list || !/\/posts\/[^/]+\/comments$/.test(settings.url || '') || !(settings.data instanceof FormData)) return;
            const text = settings.data.get('message');
            const now = new Date();
            const first = list.firstElementChild;
            const grouped = first && first.matches('.message') && first.dataset.author === me && now - new Date(first.dataset.time) < GROUP_MS;
            const reply = document.createElement('div');
            reply.className = `message dc-reply${grouped ? ' grouped' : ''}`;
            reply.dataset.author = me;
            reply.dataset.time = now.toISOString();
            reply.innerHTML = (grouped
                ? `<time class="message-gutter-time" data-f="time"></time>`
                : `<a class="message-avatar-link"><img alt="" class="message-avatar"></a>`) +
                `<div class="message-content">${grouped ? '' : '<h3 class="message-header"><a class="message-author dc-name"></a><time class="message-timestamp" data-f="stamp"></time></h3>'}<div class="message-text"></div></div>` +
                `<div class="message-actions"><button type="button" class="message-action" data-reply data-tip="Reply"><svg class="ic" width="20" height="20"><use href="#i-reply"/></svg></button><button type="button" class="message-action" data-message-menu data-tip="More"><svg class="ic" width="20" height="20"><use href="#i-more"/></svg></button></div>`;
            reply.querySelectorAll('time').forEach(t => t.setAttribute('datetime', reply.dataset.time));
            reply.querySelector('.message-text').textContent = text;
            if (!grouped) {
                reply.querySelector('.message-avatar-link').href = `/u/${encodeURIComponent(me)}`;
                reply.querySelector('.message-avatar').src = `/u/${encodeURIComponent(me)}/avatar`;
                const name = reply.querySelector('.message-author');
                name.href = `/u/${encodeURIComponent(me)}`;
                name.textContent = meName;
                if (body.dataset.meRole) name.classList.add(body.dataset.meRole);
            }
            list.prepend(reply);
            formatTimes(reply);
            list.scrollTop = 0;
        });
    }

    // ---------- composer: uploads and the expression picker ----------

    document.addEventListener('click', e => {
        if (!e.target.closest('[data-upload]')) return;
        document.querySelector('.dc-file')?.click();
    });

    const file = document.querySelector('.dc-file');
    if (file) {
        const composer = file.closest('.message-input-container');
        const show = () => {
            const picked = file.files && file.files[0];
            // only a picked image becomes part of the post, as the "file" field the server reads
            if (picked) file.name = 'file'; else file.removeAttribute('name');
            composer.querySelector('.dc-attachments').hidden = !picked;
            composer.classList.toggle('attaching', !!picked);
            if (picked) composer.querySelector('.dc-attachment-name').textContent = picked.name;
        };
        file.addEventListener('change', show);
        composer.querySelector('[data-attachment-remove]').addEventListener('click', () => { file.value = ''; show(); });
    }

    const EMOJI = ['😀', '😃', '😄', '😁', '😆', '😅', '😂', '🤣', '😊', '🙂', '😉', '😍', '🥰', '😘', '😎', '🤓', '🤔', '🫡',
        '😐', '😑', '🙄', '😬', '😮‍💨', '😴', '😷', '🤒', '🥳', '😢', '😭', '😤', '😡', '🤯', '😱', '🫠', '👍', '👎',
        '👏', '🙌', '🙏', '💪', '👀', '🎉', '🔥', '💯', '✅', '❌', '⚠️', '❤️', '💙', '💚', '☕', '🍕', '🌮', '🎂'];
    const GIFS = ['Trending GIFs', 'agree', 'applause', 'awww', 'dance', 'deal with it', 'do not want', 'eww', 'eye roll', 'facepalm'];

    const picker = document.createElement('div');
    picker.className = 'dc-popover dc-picker';
    picker.hidden = true;
    picker.innerHTML = `<div class="dc-picker-head dc-tabs"><button type="button" class="dc-tab" data-p="gif">GIFs</button><button type="button" class="dc-tab" data-p="sticker">Stickers</button><button type="button" class="dc-tab" data-p="emoji">Emoji</button></div><input class="dc-picker-search" type="text"><div class="dc-picker-body"></div>`;
    body.appendChild(picker);
    let pickerInput = null;

    function fillPicker(kind) {
        $$('.dc-tab', picker).forEach(t => t.classList.toggle('selected', t.dataset.p === kind));
        const search = picker.querySelector('.dc-picker-search');
        search.placeholder = kind === 'gif' ? 'Search Tenor' : kind === 'sticker' ? 'Search for stickers' : 'Find the perfect emoji';
        const target = picker.querySelector('.dc-picker-body');
        if (kind === 'emoji') {
            target.innerHTML = `<div class="dc-picker-label">Frequently Used</div><div class="dc-emoji-grid"></div>`;
            target.lastChild.append(...EMOJI.map(emoji => Object.assign(document.createElement('button'), { type: 'button', textContent: emoji })));
        } else if (kind === 'gif') {
            target.innerHTML = `<div class="dc-gif-grid"></div>`;
            target.firstChild.append(...GIFS.map((label, i) => {
                const tile = document.createElement('button');
                tile.type = 'button';
                tile.innerHTML = `<img alt="" src="/img/photo-item${(i % 11) + 1}.jpg"><span></span>`;
                tile.lastChild.textContent = label;
                return tile;
            }));
        } else {
            target.innerHTML = `<div class="dc-empty"><strong>No stickers yet</strong><span>Servers you're in haven't added any stickers.</span></div>`;
        }
    }

    document.addEventListener('click', e => {
        const open = e.target.closest('[data-picker]');
        if (open) {
            pickerInput = open.closest('.message-input-container').querySelector('.message-input, .comment-input, .conversation-input');
            fillPicker(open.dataset.picker);
            openPopover(picker, open);
            return;
        }
        const tab = e.target.closest('.dc-picker .dc-tab');
        if (tab) fillPicker(tab.dataset.p);
        const emoji = e.target.closest('.dc-emoji-grid button');
        if (emoji && pickerInput) {
            pickerInput.value += emoji.textContent;
            pickerInput.focus();
        }
        if (e.target.closest('.dc-gif-grid button')) closePopovers();
    });

    // ---------- friends and profile: Add Friend sends a follow, as the other themes' Follow does ----------

    async function follow(username, unfollow) {
        const data = new FormData();
        data.append('username', username);
        const response = await fetch(`/api/relationships/${unfollow ? 'unfollow' : 'follow'}`, { method: 'POST', body: data, credentials: 'same-origin' });
        return response.ok;
    }

    document.addEventListener('click', async e => {
        const button = e.target.closest('[data-friend]');
        if (!button) return;
        e.preventDefault();
        const friends = button.dataset.friend === 'true';
        if (!await follow(button.dataset.username, friends)) return;
        button.dataset.friend = String(!friends);
        button.classList.toggle('pending', !friends);
        const label = button.querySelector('.dc-friend-label');
        if (label) label.textContent = friends ? 'Add Friend' : 'Friend Request Sent';
        button.dataset.tip = friends ? 'Add Friend' : 'Remove Friend';
    });

    const addFriend = document.querySelector('.dc-add-friend-form');
    if (addFriend) {
        addFriend.addEventListener('submit', async e => {
            e.preventDefault();
            const input = addFriend.querySelector('input');
            const name = input.value.trim().replace(/^@/, '');
            const note = addFriend.querySelector('.dc-add-friend-note');
            if (!name) return;
            const ok = await follow(name, false);
            note.hidden = false;
            note.className = `dc-add-friend-note ${ok ? 'success' : 'error'}`;
            note.textContent = ok ? `Success! Your friend request to ${name} was sent.` : "Hm, didn't work. Double check that the username is correct.";
            if (ok) input.value = '';
        });
        addFriend.querySelector('input').addEventListener('input', () => {
            addFriend.querySelector('button[type=submit]').disabled = !addFriend.querySelector('input').value.trim();
        });
    }

    // friends list: Online and All filter the rows, the search box filters by name
    const friendsList = document.querySelector('.dc-friends');
    if (friendsList) {
        let tab = 'online';
        const filter = () => {
            const q = (document.querySelector('.dc-friends-search input')?.value || '').toLowerCase();
            let shown = 0;
            $$('.dc-friend', friendsList).forEach(row => {
                const show = (tab === 'all' || row.dataset.presence !== 'offline') && row.textContent.toLowerCase().includes(q);
                row.hidden = !show;
                shown += show ? 1 : 0;
            });
            const head = document.querySelector('.dc-friends-count');
            if (head) head.textContent = `${tab === 'all' ? 'All friends' : 'Online'} — ${shown}`;
        };
        document.querySelector('[data-tabs-scope]')?.addEventListener('dc-tab', e => { if (e.detail === 'online' || e.detail === 'all') { tab = e.detail; filter(); } });
        document.querySelector('.dc-friends-search input')?.addEventListener('input', filter);
        filter();
    }

    document.querySelector('.dc-dm-find-input')?.addEventListener('input', e => {
        const q = e.target.value.toLowerCase();
        $$('.dc-dm').forEach(dm => { dm.hidden = !(dm.textContent + dm.getAttribute('href')).toLowerCase().includes(q); });
    });

    document.querySelector('.dc-new-dm-form input[name=toUsername]')?.addEventListener('input', e => {
        const q = e.target.value.toLowerCase();
        $$('.dc-new-dm-pick').forEach(p => { p.hidden = !p.textContent.toLowerCase().includes(q); });
    });
    document.addEventListener('click', e => {
        const pick = e.target.closest('.dc-new-dm-pick');
        if (!pick) return;
        const form = pick.closest('.dc-new-dm-form');
        form.querySelector('input[name=toUsername]').value = pick.dataset.username;
        form.querySelector('input[name=message]').focus();
    });

    document.addEventListener('click', e => {
        const button = e.target.closest('[data-copy]');
        if (button) copy(button.dataset.copy);
    });

    // conversation and channel lists open at the newest message
    $$('.dc-dm-messages').forEach(list => { list.scrollTop = list.scrollHeight; });

    formatTimes(document);
})();
