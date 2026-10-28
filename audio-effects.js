/*
 * Audio Effects — Web Audio граф для YouTube Music.
 * Внедряется в страницу через AddScriptToExecuteOnDocumentCreated.
 *
 * API:  window.__nothingAudio.set({ speed: 0.8, reverb: 0.4, ... })
 *       window.__nothingAudio.get()
 *       window.__nothingAudio.reset()
 *       window.__nothingAudio.state()
 */
(function () {
    'use strict';

    if (window.__nothingAudio) return;
    if (!/music\.youtube\.com$/i.test(location.hostname)) return;

    var defaults = {
        speed: 1,
        preservePitch: true,
        bass: 0,
        mid: 0,
        treble: 0,
        underwater: false,
        underwaterDepth: 0.6,
        reverb: 0
    };

    var settings = Object.assign({}, defaults);
    var ctx = null;
    var nodes = null;
    var currentEl = null;

    function clamp(v, min, max) {
        return Math.min(max, Math.max(min, v));
    }

    // плавно подводим параметр — без щелчков при переключении эффектов
    function rampParam(param, target, context) {
        if (!param) return;
        try {
            if (context && typeof context.currentTime === 'number') {
                param.setTargetAtTime(target, context.currentTime, 0.05);
            } else {
                param.value = target;
            }
        } catch (e) {
            try { param.value = target; } catch (ignored) { }
        }
    }

    /* ---------- аудио-граф ---------- */

    function makeImpulse(context, seconds, decay) {
        var rate = context.sampleRate;
        var length = Math.max(1, Math.floor(rate * seconds));
        var buffer = context.createBuffer(2, length, rate);

        for (var channel = 0; channel < 2; channel++) {
            var data = buffer.getChannelData(channel);
            for (var i = 0; i < length; i++) {
                var t = i / length;
                data[i] = (Math.random() * 2 - 1) * Math.pow(1 - t, decay);
            }
        }
        return buffer;
    }

    function ensureGraph() {
        if (nodes) return nodes;

        var AudioCtor = window.AudioContext || window.webkitAudioContext;
        if (!AudioCtor) return null;

        try {
            ctx = new AudioCtor();
        } catch (e) {
            return null;
        }

        var low = ctx.createBiquadFilter();
        low.type = 'lowshelf';
        low.frequency.value = 180;

        var mid = ctx.createBiquadFilter();
        mid.type = 'peaking';
        mid.frequency.value = 1000;
        mid.Q.value = 0.9;

        var high = ctx.createBiquadFilter();
        high.type = 'highshelf';
        high.frequency.value = 4500;

        var lowpass = ctx.createBiquadFilter();
        lowpass.type = 'lowpass';
        lowpass.frequency.value = 22050;
        lowpass.Q.value = 0.0001;

        var dry = ctx.createGain();
        dry.gain.value = 1;

        var wet = ctx.createGain();
        wet.gain.value = 0;

        var convolver = ctx.createConvolver();
        convolver.buffer = makeImpulse(ctx, 2.4, 2.0);

        var output = ctx.createGain();
        output.gain.value = 1;

        low.connect(mid);
        mid.connect(high);
        high.connect(lowpass);

        lowpass.connect(dry);
        dry.connect(output);

        lowpass.connect(convolver);
        convolver.connect(wet);
        wet.connect(output);

        output.connect(ctx.destination);

        nodes = {
            low: low,
            mid: mid,
            high: high,
            lowpass: lowpass,
            dry: dry,
            wet: wet,
            convolver: convolver,
            output: output,
            sources: []
        };

        return nodes;
    }

    function resumeContext() {
        if (!ctx) return;
        if (ctx.state !== 'running') {
            try { ctx.resume(); } catch (e) { /* ждём жеста пользователя */ }
        }
    }

    /* ---------- привязка к плееру ---------- */

    function findMediaElement() {
        var videos = document.querySelectorAll('video');
        if (!videos.length) return null;

        var fallback = null;
        for (var i = 0; i < videos.length; i++) {
            var v = videos[i];
            if (!fallback && (v.currentSrc || v.readyState >= 2)) fallback = v;
            if (!v.__nyaAttached && (v.currentSrc || v.readyState >= 1)) return v;
        }
        return fallback || videos[0];
    }

    function attach(el) {
        if (!el || el.__nyaAttached) return false;

        var graph = ensureGraph();
        if (!graph) return false;

        var source;
        try {
            source = ctx.createMediaElementSource(el);
        } catch (e) {
            // элемент уже был привязан к другому контексту
            el.__nyaAttached = true;
            return false;
        }

        source.connect(graph.low);
        graph.sources.push(source);
        el.__nyaAttached = true;
        currentEl = el;

        resumeContext();
        apply();
        return true;
    }

    /* ---------- применение настроек ---------- */

    function apply() {
        var s = settings;

        if (currentEl) {
            try {
                var rate = clamp(Number(s.speed) || 1, 0.5, 2.5);
                if (Math.abs((currentEl.playbackRate || 1) - rate) > 0.001)
                    currentEl.playbackRate = rate;

                var pitch = !!s.preservePitch;
                currentEl.preservesPitch = pitch;
                currentEl.mozPreservesPitch = pitch;
                currentEl.webkitPreservesPitch = pitch;
            } catch (e) { /* плеер может быть ещё не готов */ }
        }

        var n = nodes;
        if (!n) return;

        n.low.gain.value = Number(s.bass) || 0;
        n.mid.gain.value = Number(s.mid) || 0;
        n.high.gain.value = Number(s.treble) || 0;

        var depth = clamp(Number(s.underwaterDepth) || 0, 0, 1);
        var reverb = clamp(Number(s.reverb) || 0, 0, 1);

        if (s.underwater) {
            // ровный «приглушённый» звук: постоянная частота среза, без качаний
            rampParam(n.lowpass.frequency, 1200 - 950 * depth, ctx); // 1200 Гц .. 250 Гц
            rampParam(n.lowpass.Q, 0.9, ctx);
            reverb = Math.max(reverb, 0.18);
        } else {
            rampParam(n.lowpass.frequency, 22050, ctx);
            rampParam(n.lowpass.Q, 0.0001, ctx);
        }

        rampParam(n.wet.gain, reverb, ctx);
        rampParam(n.dry.gain, 1 - 0.4 * reverb, ctx);
    }

    /* ---------- периодическая проверка ---------- */

    function tick() {
        var el = findMediaElement();
        if (el && !el.__nyaAttached) {
            attach(el);
        } else if (el && !currentEl) {
            currentEl = el;
        }

        if (currentEl) {
            try {
                var rate = clamp(Number(settings.speed) || 1, 0.5, 2.5);
                if (Math.abs((currentEl.playbackRate || 1) - rate) > 0.001)
                    currentEl.playbackRate = rate;
                if (currentEl.preservesPitch !== !!settings.preservePitch)
                    currentEl.preservesPitch = !!settings.preservePitch;
            } catch (e) { /* ignore */ }
        }
    }

    function armUserGesture() {
        var once = function () {
            resumeContext();
            document.removeEventListener('pointerdown', once, true);
            document.removeEventListener('keydown', once, true);
        };
        document.addEventListener('pointerdown', once, true);
        document.addEventListener('keydown', once, true);
    }

    /* ---------- публичное API ---------- */

    window.__nothingAudio = {
        set: function (partial) {
            if (partial && typeof partial === 'object') {
                settings = Object.assign({}, settings, partial);
            }
            tick();
            apply();
            resumeContext();
            return true;
        },
        get: function () {
            return Object.assign({}, settings);
        },
        reset: function () {
            settings = Object.assign({}, defaults);
            apply();
            return true;
        },
        state: function () {
            return {
                attached: !!currentEl,
                context: ctx ? ctx.state : 'none',
                speed: settings.speed
            };
        }
    };

    armUserGesture();
    setInterval(tick, 1000);
    document.addEventListener('DOMContentLoaded', tick);
    tick();
})();
