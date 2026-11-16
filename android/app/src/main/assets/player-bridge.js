/*
 * Player bridge — состояние и управление плеером YouTube Music.
 * Используется нативной частью (PlaybackService) для уведомления и медиа-сессии.
 *
 * API: window.__nothingPlayer.state() -> JSON-сериализуемый объект
 *      play() / pause() / toggle() / next() / prev() / seek(seconds)
 */
(function () {
    'use strict';

    if (window.__nothingPlayer) return;
    if (!/music\.youtube\.com$/i.test(location.hostname)) return;

    var lastArtwork = null;

    function videoEl() {
        return document.querySelector('video');
    }

    function norm(value) {
        return (value || '').toString().toLowerCase();
    }

    // Кнопки плеера ищем сначала по стабильным id, потом по подписи.
    // YouTube периодически меняет разметку, поэтому запасных вариантов несколько.
    function clickFirst(selectors, labels) {
        for (var i = 0; i < selectors.length; i++) {
            var el = document.querySelector(selectors[i]);
            if (el) {
                try { el.click(); return true; } catch (e) { /* пробуем дальше */ }
            }
        }

        var nodes = document.querySelectorAll('button, [role="button"], tp-yt-paper-icon-button, yt-icon-button');
        for (var j = 0; j < nodes.length; j++) {
            var node = nodes[j];
            var label = norm(node.getAttribute && (node.getAttribute('aria-label') || node.getAttribute('title')));
            if (!label) continue;
            for (var k = 0; k < labels.length; k++) {
                if (labels[k].test(label)) {
                    try { node.click(); return true; } catch (e) { /* ignore */ }
                }
            }
        }
        return false;
    }

    function readMediaSession() {
        try {
            var md = navigator.mediaSession && navigator.mediaSession.metadata;
            if (!md) return null;

            var artwork = '';
            if (md.artwork && md.artwork.length) {
                // последний элемент — обычно самый большой
                artwork = md.artwork[md.artwork.length - 1].src || '';
            }
            if (artwork) lastArtwork = artwork;

            return {
                title: md.title || '',
                artist: md.artist || '',
                album: md.album || '',
                artwork: artwork
            };
        } catch (e) {
            return null;
        }
    }

    function fallbackTitle() {
        try {
            var el = document.querySelector(
                'ytmusic-player-bar .title, ytmusic-player-bar .song-title, .ytmusic-player-bar .title'
            );
            if (el && el.textContent) return el.textContent.trim();
        } catch (e) { /* ignore */ }
        return document.title || '';
    }

    function safeNumber(value) {
        return (typeof value === 'number' && isFinite(value)) ? value : 0;
    }

    window.__nothingPlayer = {
        state: function () {
            var v = videoEl();
            var meta = readMediaSession() || {};

            return {
                hasVideo: !!v,
                playing: v ? !v.paused : false,
                paused: v ? v.paused : true,
                position: v ? safeNumber(v.currentTime) : 0,
                duration: v ? safeNumber(v.duration) : 0,
                title: meta.title || fallbackTitle(),
                artist: meta.artist || '',
                album: meta.album || '',
                artwork: meta.artwork || lastArtwork || '',
                volume: v ? safeNumber(v.volume) : 1
            };
        },

        play: function () {
            var v = videoEl();
            if (v) {
                var p = v.play();
                if (p && typeof p.catch === 'function') p.catch(function () { });
                return true;
            }
            return clickFirst(['#play-pause-button'], [/^play$/i, /воспроизв/i]);
        },

        pause: function () {
            var v = videoEl();
            if (v) {
                try { v.pause(); } catch (e) { /* ignore */ }
                return true;
            }
            return false;
        },

        toggle: function () {
            var v = videoEl();
            if (!v) return false;
            if (v.paused) return this.play();
            return this.pause();
        },

        next: function () {
            return clickFirst(
                ['#next-button', 'ytmusic-player-bar #next-button', '.next-button'],
                [/next/i, /следующ/i]
            );
        },

        prev: function () {
            return clickFirst(
                ['#previous-button', 'ytmusic-player-bar #previous-button', '.previous-button'],
                [/previous/i, /предыдущ/i, /назад/i]
            );
        },

        seek: function (seconds) {
            var v = videoEl();
            if (!v) return false;
            var target = Number(seconds);
            if (!isFinite(target) || target < 0) return false;
            try {
                v.currentTime = target;
                return true;
            } catch (e) {
                return false;
            }
        }
    };
})();
