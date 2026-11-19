// YouTube theme behaviour: guide, menus, Studio upload dialog, watch page player, likes, subscribe,
// comments, channel tabs and the inbox. Posting, liking and commenting still go through social.js;
// this file only redraws what social.js renders in its generic way.
(function () {
    'use strict';

    const body = document.body;
    const me = (document.querySelector('meta[name="current-user"]') || {}).content || '';

    function icon(name, cls) {
        return `<svg class="yt-icon${cls ? ' ' + cls : ''}"><use href="#yt-i-${name}"/></svg>`;
    }

    function compact(n) {
        if (n >= 1e6) return (n >= 1e7 ? Math.round(n / 1e6) : Math.round(n / 1e5) / 10) + 'M';
        if (n >= 1e3) return (n >= 1e4 ? Math.round(n / 1e3) : Math.round(n / 1e2) / 10) + 'K';
        return String(n);
    }

    // ---------- Snackbar ----------
    let toastTimer;
    function toast(message) {
        let el = document.querySelector('.yt-toast');
        if (!el) {
            el = document.createElement('div');
            el.className = 'yt-toast';
            el.setAttribute('role', 'status');
            body.appendChild(el);
        }
        el.textContent = message;
        requestAnimationFrame(() => el.classList.add('show'));
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => el.classList.remove('show'), 4000);
    }
    window.ytToast = toast;

    document.addEventListener('click', e => {
        const t = e.target.closest('[data-toast]');
        if (t) toast(t.dataset.toast);
    });

    // ---------- Guide ----------
    const drawerMode = () => body.classList.contains('yt-watch-page') || window.innerWidth < 1313;
    document.addEventListener('click', e => {
        if (e.target.closest('.yt-guide-btn')) {
            if (drawerMode()) body.classList.toggle('yt-guide-open');
            else body.classList.toggle('yt-guide-collapsed');
        } else if (e.target.closest('[data-guide-close]')) {
            body.classList.remove('yt-guide-open');
        }
        const more = e.target.closest('.yt-guide-more');
        if (more) {
            const hidden = more.parentElement.querySelectorAll('.yt-guide-channel[hidden]');
            if (hidden.length) {
                hidden.forEach(a => { a.hidden = false; a.dataset.extra = '1'; });
                more.innerHTML = icon('chevron-down') + '<span>Show fewer</span>';
                more.querySelector('svg').style.transform = 'rotate(180deg)';
            } else {
                more.parentElement.querySelectorAll('[data-extra]').forEach(a => { a.hidden = true; });
                more.innerHTML = icon('chevron-down') + '<span>Show more</span>';
            }
        }
    });

    // ---------- Popups (create, notifications, account, card menus) ----------
    function closePopups(except) {
        document.querySelectorAll('.yt-popup').forEach(p => { if (p !== except) p.hidden = true; });
        document.querySelectorAll('.yt-card-popup').forEach(p => { if (p !== except) p.remove(); });
    }

    document.addEventListener('click', e => {
        const opener = e.target.closest('[data-popup]');
        if (opener) {
            const popup = document.getElementById(opener.dataset.popup);
            const show = popup.hidden;
            closePopups(popup);
            popup.hidden = !show;
            if (opener.classList.contains('yt-notif-btn')) opener.querySelector('.yt-notif-badge')?.remove();
            return;
        }
        if (!e.target.closest('.yt-popup, .yt-card-popup') || e.target.closest('.yt-menu-item')) closePopups();
    });

    document.addEventListener('keydown', e => {
        if (e.key !== 'Escape') return;
        closePopups();
        body.classList.remove('yt-guide-open', 'yt-search-open');
        if (upload && !upload.hidden) closeUpload();
        document.querySelector('.yt-share-backdrop')?.remove();
    });

    // Video card "more" menus
    const cardMenu = [
        ['queue', 'Add to queue', 'Video added to queue'],
        ['clock', 'Save to Watch later', 'Saved to Watch later'],
        ['save', 'Save to playlist', 'Saved to Liked videos'],
        ['download', 'Download', 'Get YouTube Premium to download videos'],
        ['share', 'Share', 'share'],
        null,
        ['block', 'Not interested', 'hide'],
        ['minus-circle', "Don't recommend channel", "We won't recommend this channel to you again"],
        ['flag', 'Report', 'Thanks for reporting']
    ];

    document.addEventListener('click', e => {
        const btn = e.target.closest('.yt-card-menu');
        if (!btn) return;
        e.stopPropagation();
        const card = btn.closest('[data-post-id]');
        const open = document.querySelector('.yt-card-popup');
        closePopups();
        if (open && open.dataset.for === card.dataset.postId) return;

        const menu = document.createElement('div');
        menu.className = 'yt-popup yt-card-popup';
        menu.dataset.for = card.dataset.postId;
        cardMenu.forEach(item => {
            if (!item) { menu.insertAdjacentHTML('beforeend', '<div class="yt-menu-divider"></div>'); return; }
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'yt-menu-item';
            b.innerHTML = icon(item[0]) + item[1];
            b.addEventListener('click', () => {
                if (item[2] === 'share') share(card.dataset.postId);
                else if (item[2] === 'hide') hideCard(card);
                else toast(item[2]);
            });
            menu.appendChild(b);
        });
        body.appendChild(menu);
        const r = btn.getBoundingClientRect();
        const left = Math.min(r.right - 16, window.innerWidth - menu.offsetWidth - 8);
        let top = r.bottom + window.scrollY;
        if (r.bottom + menu.offsetHeight > window.innerHeight) top = r.top + window.scrollY - menu.offsetHeight;
        menu.style.left = Math.max(8, left) + 'px';
        menu.style.top = top + 'px';
    }, true);

    function hideCard(card) {
        card.style.display = 'none';
        toast('Video removed');
    }

    // Share dialog
    function share(postId) {
        document.querySelector('.yt-share-backdrop')?.remove();
        const url = `${location.origin}/posts/${postId}`;
        const targets = [['Embed', '#0f0f0f', '<>'], ['WhatsApp', '#25d366', 'W'], ['Facebook', '#1877f2', 'f'], ['X', '#000', 'X'], ['Email', '#888', '@'], ['Reddit', '#ff4500', 'r'], ['LinkedIn', '#0a66c2', 'in']];
        const wrap = document.createElement('div');
        wrap.className = 'yt-share-backdrop';
        wrap.innerHTML = `
            <div class="yt-share" role="dialog" aria-label="Share">
                <div class="yt-share-head"><span>Share</span><button class="yt-icon-btn" type="button" data-share-close aria-label="Close">${icon('close')}</button></div>
                <div class="yt-share-targets">${targets.map(t => `<button type="button" class="yt-share-target"><span style="background:${t[1]}">${t[2]}</span>${t[0]}</button>`).join('')}</div>
                <div class="yt-share-link"><input type="text" readonly value="${url}"><button class="yt-pill yt-pill-blue" type="button" data-share-copy>Copy</button></div>
            </div>`;
        body.appendChild(wrap);
        wrap.addEventListener('click', e => {
            if (e.target === wrap || e.target.closest('[data-share-close]')) wrap.remove();
            if (e.target.closest('[data-share-copy]')) {
                const input = wrap.querySelector('input');
                input.select();
                (navigator.clipboard ? navigator.clipboard.writeText(url) : Promise.reject()).catch(() => document.execCommand('copy'));
                toast('Link copied to clipboard');
            }
            if (e.target.closest('.yt-share-target')) toast('Sharing is unavailable on this network');
        });
    }

    // ---------- Search and chips ----------
    document.querySelector('.yt-mobile-search-btn')?.addEventListener('click', () => {
        body.classList.add('yt-search-open');
        document.querySelector('.yt-searchbox input')?.focus();
    });
    document.querySelector('.yt-search-back')?.addEventListener('click', () => body.classList.remove('yt-search-open'));
    document.querySelector('.yt-searchbox')?.addEventListener('submit', e => {
        if (!e.target.q.value.trim()) e.preventDefault();
    });
    document.querySelector('.yt-voice-btn')?.addEventListener('click', () => toast('Allow microphone access to search with your voice'));

    document.addEventListener('click', e => {
        const chip = e.target.closest('.yt-chip');
        if (!chip || chip.closest('.yt-channel-sort')) return;
        chip.parentElement.querySelectorAll('.yt-chip').forEach(c => c.classList.toggle('selected', c === chip));
        const grid = document.querySelector('.yt-rich-grid');
        if (grid && chip.closest('.yt-chips-bar')) {
            grid.style.opacity = '.4';
            setTimeout(() => { grid.style.opacity = ''; }, 350);
        }
    });

    const chipsBar = document.querySelector('.yt-chips-bar');
    if (chipsBar) {
        const chips = chipsBar.querySelector('.yt-chips');
        const next = chipsBar.querySelector('.yt-chips-next');
        const update = () => { next.hidden = chips.scrollLeft + chips.clientWidth >= chips.scrollWidth - 4; };
        next.addEventListener('click', () => { chips.scrollLeft += chips.clientWidth * .6; });
        chips.addEventListener('scroll', update);
        window.addEventListener('resize', update);
        update();
    }

    // ---------- Studio upload dialog ----------
    const upload = document.getElementById('yt-upload');
    let closeUpload = () => {};
    if (upload) {
        const dialog = upload.querySelector('.yt-upload-dialog');
        const form = dialog.querySelector('form');
        const heading = dialog.querySelector('#yt-upload-title');
        const title = dialog.querySelector('#yt-upload-name');
        const description = dialog.querySelector('#yt-upload-description');
        const fileInput = dialog.querySelector('.yt-upload-file');
        const drop = dialog.querySelector('.yt-upload-drop');
        const status = dialog.querySelector('.yt-upload-status');
        const progress = dialog.querySelector('.yt-upload-progress');
        const player = dialog.querySelector('.yt-upload-player');
        const error = dialog.querySelector('.yt-upload-error');
        const thumbUpload = dialog.querySelector('.yt-thumb-upload');
        const steps = ['details', 'elements', 'checks', 'visibility'];
        let timer;

        const setStep = step => {
            dialog.dataset.step = step;
            dialog.querySelectorAll('[data-panel]').forEach(p => { p.hidden = p.dataset.panel !== step; });
            const index = steps.indexOf(step);
            dialog.querySelectorAll('.yt-upload-stepper li').forEach((li, n) => {
                li.classList.toggle('done', n < index);
                li.classList.toggle('current', n === index);
            });
            dialog.querySelector('.yt-upload-main').scrollTop = 0;
        };

        const counters = () => dialog.querySelectorAll('[data-count-for]').forEach(c => {
            const field = document.getElementById(c.dataset.countFor);
            c.textContent = `${field.value.length}/${field.maxLength}`;
        });

        const randomId = n => Array.from({ length: n }, () => 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_'[Math.floor(Math.random() * 64)]).join('');

        const start = file => {
            const name = file.name.replace(/\.[^.]+$/, '');
            heading.textContent = name || 'Upload videos';
            title.value = name.slice(0, 100);
            counters();
            dialog.querySelector('.yt-upload-filename').textContent = file.name;
            const link = 'https://youtu.be/' + randomId(11);
            dialog.querySelector('.yt-upload-link').textContent = link;
            setStep('details');

            let pct = 0;
            const total = 6 + Math.floor(Math.random() * 6);
            status.classList.remove('complete');
            clearInterval(timer);
            timer = setInterval(() => {
                pct = Math.min(100, pct + 100 / (total * 4));
                if (pct < 100) {
                    const left = Math.ceil((100 - pct) / 100 * total);
                    progress.textContent = `Uploading ${Math.floor(pct)}% ... ${left} second${left === 1 ? '' : 's'} left`;
                    return;
                }
                clearInterval(timer);
                progress.textContent = 'Upload complete ... Processing will begin shortly';
                setTimeout(() => {
                    progress.textContent = 'Checks complete. No issues found.';
                    status.classList.add('complete');
                    status.querySelector('use').setAttribute('href', '#yt-i-check-circle');
                    player.classList.add('ready');
                }, 1500);
            }, 250);
        };

        const reset = () => {
            clearInterval(timer);
            form.reset();
            heading.textContent = 'Upload videos';
            progress.textContent = 'Uploading 0%';
            status.classList.remove('complete');
            status.querySelector('use').setAttribute('href', '#yt-i-upload-arrow');
            player.classList.remove('ready');
            thumbUpload.classList.remove('has-file');
            thumbUpload.querySelector('span').textContent = 'Upload file';
            error.hidden = true;
            title.closest('.yt-field').classList.remove('invalid');
            counters();
            setStep('select');
        };

        const openUpload = () => {
            closePopups();
            body.classList.remove('yt-guide-open');
            upload.hidden = false;
            body.style.overflow = 'hidden';
        };
        closeUpload = () => {
            upload.hidden = true;
            body.style.overflow = '';
            reset();
        };

        document.addEventListener('click', e => {
            if (e.target.closest('[data-open-upload]')) openUpload();
            else if (e.target.closest('[data-close-upload]')) closeUpload();
        });

        fileInput.addEventListener('change', () => { if (fileInput.files[0]) start(fileInput.files[0]); });
        drop.addEventListener('dragover', e => { e.preventDefault(); drop.classList.add('dragover'); });
        drop.addEventListener('dragleave', () => drop.classList.remove('dragover'));
        drop.addEventListener('drop', e => {
            e.preventDefault();
            drop.classList.remove('dragover');
            if (e.dataTransfer.files[0]) start(e.dataTransfer.files[0]);
        });

        title.addEventListener('input', () => {
            counters();
            if (title.value.trim()) { error.hidden = true; title.closest('.yt-field').classList.remove('invalid'); }
        });
        description.addEventListener('input', counters);

        thumbUpload.querySelector('input').addEventListener('change', e => {
            const file = e.target.files[0];
            thumbUpload.classList.toggle('has-file', !!file);
            thumbUpload.querySelector('span').textContent = file ? file.name : 'Upload file';
            dialog.querySelectorAll('.yt-thumb-auto').forEach(b => b.classList.toggle('selected', false));
        });
        dialog.querySelectorAll('.yt-thumb-auto').forEach(b => b.addEventListener('click', () => {
            dialog.querySelectorAll('.yt-thumb-auto').forEach(x => x.classList.toggle('selected', x === b));
        }));

        const needTitle = () => {
            if (title.value.trim()) return false;
            setStep('details');
            error.hidden = false;
            title.closest('.yt-field').classList.add('invalid');
            title.focus();
            return true;
        };

        dialog.querySelector('.yt-upload-next').addEventListener('click', () => {
            const step = dialog.dataset.step;
            if (step === 'details' && needTitle()) return;
            setStep(steps[Math.min(steps.length - 1, steps.indexOf(step) + 1)]);
        });
        dialog.querySelector('.yt-upload-back').addEventListener('click', () => {
            setStep(steps[Math.max(0, steps.indexOf(dialog.dataset.step) - 1)]);
        });
        dialog.querySelectorAll('.yt-upload-stepper li').forEach(li => li.addEventListener('click', () => {
            if (dialog.dataset.step === 'details' && needTitle()) return;
            setStep(li.dataset.for);
        }));

        // The native form post still sends theme, user and message; the description rides on the message
        form.addEventListener('submit', e => {
            if (needTitle()) { e.preventDefault(); return; }
            const name = title.value.trim();
            const text = description.value.trim();
            if (text) title.value = name + (/[.!?]$/.test(name) ? ' ' : '. ') + text;
            const link = dialog.querySelector('.yt-upload-link').textContent;
            const watch = dialog.querySelector('.yt-upload-watch');
            watch.textContent = link;
            watch.href = '/u/' + encodeURIComponent(me);
            heading.textContent = 'Video published';
            setTimeout(() => setStep('published'), 0);
        });

        // When the new upload comes back over SignalR, point the published dialog at its watch page
        if (window.connection && window.connection.on) {
            window.connection.on('SendMessage', (id, user) => {
                if (user === me) dialog.querySelector('.yt-upload-watch').href = '/posts/' + id;
            });
        }
    }

    // ---------- Likes and dislikes ----------
    function renderLike(btn) {
        const liked = btn.classList.contains('liked');
        btn.setAttribute('aria-pressed', liked);
        btn.innerHTML = icon(liked ? 'like-fill' : 'like') + `<span>${compact(Number(btn.dataset.count))}</span>`;
    }

    $(function () {
        // social.js has already posted the like and toggled .liked; redraw after its handlers run
        $(document).on('click', '.yt-like-btn.like-it', function () {
            const btn = this;
            const was = btn.dataset.liked === 'true';
            requestAnimationFrame(() => {
                const liked = btn.classList.contains('liked');
                if (liked !== was) {
                    btn.dataset.count = Number(btn.dataset.count) + (liked ? 1 : -1);
                    btn.dataset.liked = liked;
                }
                renderLike(btn);
                if (liked) {
                    const dislike = btn.parentElement.querySelector('.yt-dislike-btn');
                    if (dislike) { dislike.classList.remove('pressed'); dislike.innerHTML = icon('dislike'); }
                }
            });
        });
    });

    document.addEventListener('click', e => {
        const dislike = e.target.closest('.yt-dislike-btn');
        if (!dislike) return;
        const pressed = dislike.classList.toggle('pressed');
        dislike.innerHTML = icon(pressed ? 'dislike-fill' : 'dislike');
        const like = dislike.parentElement.querySelector('.yt-like-btn');
        if (pressed && like && like.classList.contains('liked')) {
            like.classList.remove('liked');
            like.dataset.count = Number(like.dataset.count) - 1;
            like.dataset.liked = 'false';
            renderLike(like);
        }
        if (pressed) toast('You disliked this video');
    });

    // ---------- Subscribe ----------
    function renderSubscribe(btn) {
        const on = btn.dataset.subscribed === 'true';
        btn.classList.toggle('yt-pill-dark', !on);
        btn.innerHTML = on ? icon('bell') + '<span>Subscribed</span>' + icon('chevron-down', 'yt-sub-chevron') : '<span>Subscribe</span>';
    }

    function follow(btn, on) {
        const data = new FormData();
        data.append('username', btn.dataset.username);
        btn.disabled = true;
        fetch(`/api/relationships/${on ? 'follow' : 'unfollow'}`, { method: 'POST', body: data, credentials: 'same-origin' })
            .then(r => {
                if (!r.ok) throw new Error(r.status);
                document.querySelectorAll(`.yt-subscribe[data-username="${CSS.escape(btn.dataset.username)}"]`).forEach(b => {
                    b.dataset.subscribed = on;
                    renderSubscribe(b);
                });
                toast(on ? 'Subscription added' : 'Subscription removed');
            })
            .catch(() => toast("Couldn't update your subscription. Try again."))
            .finally(() => { btn.disabled = false; });
    }

    document.querySelectorAll('.yt-subscribe').forEach(renderSubscribe);

    document.addEventListener('click', e => {
        const btn = e.target.closest('.yt-subscribe');
        if (!btn) return;
        e.stopPropagation();
        if (btn.dataset.subscribed !== 'true') { follow(btn, true); return; }

        closePopups();
        const menu = document.createElement('div');
        menu.className = 'yt-popup yt-card-popup yt-sub-menu';
        menu.innerHTML = `
            <button class="yt-menu-item checked" type="button" data-toast="You'll get all notifications">${icon('bell')}All</button>
            <button class="yt-menu-item" type="button" data-toast="You'll get personalized notifications">${icon('bell')}Personalized</button>
            <button class="yt-menu-item" type="button" data-toast="You won't get notifications">${icon('bell')}None</button>
            <button class="yt-menu-item" type="button" data-unsubscribe>${icon('minus-circle')}Unsubscribe</button>`;
        body.appendChild(menu);
        const r = btn.getBoundingClientRect();
        menu.style.left = Math.max(8, Math.min(r.left, window.innerWidth - menu.offsetWidth - 8)) + 'px';
        menu.style.top = (r.bottom + window.scrollY + 4) + 'px';
        menu.querySelector('[data-unsubscribe]').addEventListener('click', () => follow(btn, false));
    }, true);

    // ---------- Watch page ----------
    const description = document.querySelector('.yt-description');
    if (description) {
        description.addEventListener('click', e => {
            if (e.target.closest('a')) return;
            if (e.target.closest('.yt-description-less')) { description.classList.remove('expanded'); e.stopPropagation(); return; }
            description.classList.add('expanded');
        });
    }

    document.querySelector('.yt-action-share')?.addEventListener('click', e => share(e.currentTarget.dataset.id));

    const playerEl = document.querySelector('.yt-player');
    if (playerEl) initPlayer(playerEl);

    function clock(s) {
        s = Math.floor(s);
        const h = Math.floor(s / 3600), m = Math.floor(s % 3600 / 60), sec = String(s % 60).padStart(2, '0');
        return h ? `${h}:${String(m).padStart(2, '0')}:${sec}` : `${m}:${sec}`;
    }

    function initPlayer(p) {
        const video = p.querySelector('video');
        const duration = Number(p.dataset.duration) || 600;
        const played = p.querySelector('.yt-progress-played');
        const scrubber = p.querySelector('.yt-progress-scrubber');
        const progressBar = p.querySelector('.yt-progress');
        const time = p.querySelector('.yt-time-current');
        const playBtn = p.querySelector('.yt-play-btn');
        const muteBtn = p.querySelector('.yt-mute-btn');
        let position = 0, playing = false, last = 0, idle;

        video.addEventListener('error', () => {}, true);

        const draw = () => {
            const pct = Math.min(100, position / duration * 100);
            played.style.width = pct + '%';
            scrubber.style.left = pct + '%';
            time.textContent = clock(position);
        };

        const tick = now => {
            if (!playing) return;
            position = Math.min(duration, position + (now - last) / 1000);
            last = now;
            draw();
            if (position >= duration) { pause(); p.classList.add('ended'); return; }
            requestAnimationFrame(tick);
        };

        const play = () => {
            if (p.classList.contains('ended')) { position = 0; p.classList.remove('ended'); }
            p.classList.add('started', 'buffering');
            const result = video.play();
            if (result && result.catch) result.catch(() => {});
            setTimeout(() => {
                p.classList.remove('buffering');
                playing = true;
                p.classList.add('playing');
                playBtn.innerHTML = icon('pause');
                last = performance.now();
                requestAnimationFrame(tick);
            }, 700);
        };

        const pause = () => {
            playing = false;
            p.classList.remove('playing');
            playBtn.innerHTML = icon('play');
            try { video.pause(); } catch (err) { /* nothing is playing */ }
        };

        const toggle = () => (playing ? pause() : play());

        p.querySelector('.yt-player-surface').addEventListener('click', toggle);
        p.querySelector('.yt-large-play').addEventListener('click', e => { e.stopPropagation(); play(); });
        playBtn.addEventListener('click', toggle);

        progressBar.addEventListener('click', e => {
            const r = progressBar.getBoundingClientRect();
            position = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width)) * duration;
            p.classList.add('started');
            p.classList.remove('ended');
            draw();
        });

        muteBtn.addEventListener('click', () => {
            video.muted = !video.muted;
            muteBtn.innerHTML = icon(video.muted ? 'mute' : 'volume');
            p.classList.toggle('muted', video.muted);
        });

        p.querySelector('.yt-cc-btn').addEventListener('click', e => {
            e.currentTarget.classList.toggle('on');
            toast(e.currentTarget.classList.contains('on') ? 'Subtitles/closed captions: English (auto-generated)' : 'Subtitles/closed captions turned off');
        });
        p.querySelector('.yt-autoplay').addEventListener('click', e => {
            const on = e.currentTarget.classList.toggle('on');
            toast(on ? 'Autoplay is on' : 'Autoplay is off');
        });
        p.querySelector('.yt-settings-btn').addEventListener('click', e => {
            e.stopPropagation();
            const menu = p.querySelector('.yt-player-settings');
            menu.hidden = !menu.hidden;
        });
        p.querySelector('.yt-theater-btn').addEventListener('click', () => body.classList.toggle('yt-theater'));
        p.querySelector('.yt-fullscreen-btn').addEventListener('click', () => {
            if (document.fullscreenElement) document.exitFullscreen();
            else if (p.requestFullscreen) p.requestFullscreen().catch(() => {});
        });
        p.querySelector('.yt-miniplayer-btn').addEventListener('click', () => toast('Miniplayer opens when you browse away from this video'));

        p.addEventListener('mousemove', () => {
            p.classList.remove('idle');
            clearTimeout(idle);
            idle = setTimeout(() => { if (playing) p.classList.add('idle'); }, 3000);
        });
        p.addEventListener('mouseleave', () => { if (playing) p.classList.add('idle'); });

        document.addEventListener('keydown', e => {
            if (e.target.closest('input, textarea, [contenteditable]')) return;
            if (e.key === 'k' || e.key === ' ') { e.preventDefault(); toggle(); }
            if (e.key === 'm') muteBtn.click();
            if (e.key === 'f') p.querySelector('.yt-fullscreen-btn').click();
        });

        draw();
    }

    // ---------- Comments ----------
    const thread = document.querySelector('.comments-list');
    const addBox = document.querySelector('.yt-add-comment');
    if (addBox) {
        const input = addBox.querySelector('.comment-input');
        const submit = addBox.querySelector('.comment-post-btn');
        const count = document.querySelector('.yt-comments-count');
        let pending = '';

        const sync = () => { submit.disabled = !input.value.trim(); };
        input.addEventListener('focus', () => addBox.classList.add('active'));
        input.addEventListener('input', sync);
        addBox.querySelector('.yt-comment-cancel').addEventListener('click', () => {
            input.value = '';
            sync();
            addBox.classList.remove('active');
        });
        // Remember the text before social.js clears the box, so the redrawn comment shows it
        submit.addEventListener('click', () => { pending = input.value.trim(); }, true);

        // social.js prepends a generic comment; replace it with YouTube's comment markup
        new MutationObserver(records => records.forEach(r => r.addedNodes.forEach(node => {
            if (node.nodeType !== 1 || !node.classList.contains('comment')) {
                if (node.nodeType === 3 && !node.textContent.trim()) node.remove();
                return;
            }
            const comment = buildComment(me, pending || node.querySelector('.comment-text')?.textContent.trim() || '', 'Just now');
            node.replaceWith(comment);
            pending = '';
            sync();
            addBox.classList.remove('active');
            if (count) {
                const n = (parseInt(count.dataset.count, 10) || 0) + 1;
                count.dataset.count = n;
                count.textContent = `${n.toLocaleString('en-US')} Comment${n === 1 ? '' : 's'}`;
            }
        }))).observe(thread, { childList: true });
    }

    function buildComment(user, text, when) {
        const el = document.createElement('div');
        el.className = 'yt-comment';
        el.dataset.created = new Date().toISOString();
        el.dataset.likes = '0';
        el.innerHTML = `
            <a class="yt-comment-avatar" href="/u/${encodeURIComponent(user)}"><img src="/u/${encodeURIComponent(user)}/avatar" alt=""></a>
            <div class="yt-comment-body">
                <div class="yt-comment-head"><a class="yt-comment-author" href="/u/${encodeURIComponent(user)}"></a><span class="yt-comment-time"></span></div>
                <div class="yt-comment-text"></div>
                <div class="yt-comment-toolbar">
                    <button class="yt-icon-btn yt-comment-like" type="button" aria-label="Like this comment">${icon('like')}</button><span class="yt-comment-likes"></span>
                    <button class="yt-icon-btn yt-comment-dislike" type="button" aria-label="Dislike this comment">${icon('dislike')}</button>
                    <button class="yt-text-btn yt-comment-reply" type="button">Reply</button>
                </div>
            </div>
            <button class="yt-icon-btn yt-comment-menu" type="button" aria-label="Action menu">${icon('more-vert')}</button>`;
        el.querySelector('.yt-comment-author').textContent = '@' + user;
        el.querySelector('.yt-comment-time').textContent = when;
        el.querySelector('.yt-comment-text').textContent = text;
        return el;
    }

    document.addEventListener('click', e => {
        const like = e.target.closest('.yt-comment-like');
        if (like) {
            const comment = like.closest('.yt-comment');
            const on = like.classList.toggle('pressed');
            const n = Math.max(0, Number(comment.dataset.likes) + (on ? 1 : -1));
            comment.dataset.likes = n;
            like.innerHTML = icon(on ? 'like-fill' : 'like');
            comment.querySelector('.yt-comment-likes').textContent = n ? compact(n) : '';
            return;
        }
        const dislike = e.target.closest('.yt-comment-dislike');
        if (dislike) {
            const on = dislike.classList.toggle('pressed');
            dislike.innerHTML = icon(on ? 'dislike-fill' : 'dislike');
            return;
        }
        if (e.target.closest('.yt-comment-reply')) {
            const input = document.querySelector('.yt-add-comment .comment-input');
            const author = e.target.closest('.yt-comment').querySelector('.yt-comment-author').textContent;
            if (input) {
                input.value = author + ' ';
                input.focus();
                input.dispatchEvent(new Event('input'));
                input.scrollIntoView({ block: 'center', behavior: 'smooth' });
            }
            return;
        }
        if (e.target.closest('.yt-comment-menu')) toast('Thanks for reporting');
    });

    const sortBtn = document.querySelector('.yt-comments-sort');
    if (sortBtn && thread) {
        sortBtn.addEventListener('click', e => {
            e.stopPropagation();
            const menu = document.getElementById('yt-sort-menu');
            const show = menu.hidden;
            closePopups(menu);
            menu.hidden = !show;
        });
        document.querySelectorAll('#yt-sort-menu [data-sort]').forEach(item => item.addEventListener('click', () => {
            document.querySelectorAll('#yt-sort-menu [data-sort]').forEach(i => i.classList.toggle('checked', i === item));
            const comments = [...thread.querySelectorAll(':scope > .yt-comment')];
            const key = item.dataset.sort === 'newest'
                ? c => -Date.parse(c.dataset.created)
                : c => -Number(c.dataset.likes);
            comments.sort((a, b) => key(a) - key(b)).forEach(c => thread.appendChild(c));
        }));
    }

    // ---------- Channel page ----------
    document.querySelectorAll('.yt-tab[data-tab]').forEach(tab => tab.addEventListener('click', () => {
        document.querySelectorAll('.yt-tab[data-tab]').forEach(t => t.classList.toggle('active', t === tab));
        document.querySelectorAll('.yt-tab-panel').forEach(panel => { panel.hidden = panel.dataset.panel !== tab.dataset.tab; });
    }));
    document.querySelectorAll('[data-tab-link]').forEach(link => link.addEventListener('click', () => {
        document.querySelector(`.yt-tab[data-tab="${link.dataset.tabLink}"]`)?.click();
    }));

    document.querySelectorAll('.yt-channel-sort .yt-chip').forEach(chip => chip.addEventListener('click', () => {
        const bar = chip.parentElement;
        bar.querySelectorAll('.yt-chip').forEach(c => c.classList.toggle('selected', c === chip));
        const grid = bar.nextElementSibling;
        const cards = [...grid.children];
        const by = {
            latest: c => -Date.parse(c.dataset.created),
            popular: c => -Number(c.dataset.views),
            oldest: c => Date.parse(c.dataset.created)
        }[chip.dataset.sort];
        cards.sort((a, b) => by(a) - by(b)).forEach(c => grid.appendChild(c));
    }));

    document.querySelector('.yt-channel-desc')?.addEventListener('click', e => e.currentTarget.classList.toggle('expanded'));

    // ---------- Inbox ----------
    const chatScroll = document.querySelector('.yt-chat-scroll');
    if (chatScroll) chatScroll.scrollTop = chatScroll.scrollHeight;

    document.querySelectorAll('.yt-chat-form textarea').forEach(area => {
        const grow = () => { area.style.height = 'auto'; area.style.height = Math.min(area.scrollHeight, 120) + 'px'; };
        area.addEventListener('input', grow);
        area.addEventListener('keydown', e => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                if (area.value.trim()) area.form.requestSubmit();
            }
        });
    });

    document.querySelector('.yt-compose')?.addEventListener('submit', e => {
        const to = e.target.querySelector('input[name="toUsername"]');
        to.value = to.value.trim().replace(/^@/, '');
    });

    document.querySelectorAll('.yt-compose-person').forEach(person => person.addEventListener('click', () => {
        const to = document.querySelector('.yt-compose input[name="toUsername"]');
        to.value = person.dataset.username;
        document.querySelector('.yt-compose textarea[name="message"]')?.focus();
    }));
})();
