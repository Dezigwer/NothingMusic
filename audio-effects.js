/*
 * Audio Effects — Web Audio граф для музыкальных сайтов.
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
    var ctx = null;              // наш контекст, если сайт не использует Web Audio
    var ownGraph = null;         // граф в нашем контексте
    var siteGraphs = [];         // графы, встроенные в контексты сайта
    var currentEl = null;
    var lastDeepScan = 0;
    var tracked = [];
    var impulses = [];
    var contextHooked = false;
    var captureCalls = 0;

    /* ---------- перехват медиа-элементов ----------
     * Многие плееры создают <audio>/<video> в памяти (new Audio()) и не вставляют
     * их в DOM. querySelectorAll такие элементы не видит, поэтому запоминаем всё,
     * на чём вызывают play(), и все объекты new Audio().
     */

    function remember(el) {
        if (!el) return;

        var tag = el.tagName;
        if (tag !== 'AUDIO' && tag !== 'VIDEO') return;
        if (tracked.indexOf(el) >= 0) return;

        tracked.push(el);

        // момент, когда элемент впервые появился — нужен, чтобы не перехватить
        // его раньше, чем это сделает собственный граф сайта
        if (!el.__nyaSeen) {
            try { el.__nyaSeen = Date.now(); } catch (e) { }
        }

        // не копим ссылки на старые треки
        if (tracked.length > 24) {
            for (var i = 0; i < tracked.length && tracked.length > 16; i++) {
                var old = tracked[i];
                if (old !== currentEl && old !== el && (old.paused || old.ended)) {
                    tracked.splice(i, 1);
                    i--;
                }
            }
            if (tracked.length > 24)
                tracked.splice(0, tracked.length - 24);
        }
    }

    function installMediaHooks() {
        try {
            var proto = window.HTMLMediaElement && window.HTMLMediaElement.prototype;
            if (proto && typeof proto.play === 'function') {
                var origPlay = proto.play;
                proto.play = function () {
                    remember(this);
                    return origPlay.apply(this, arguments);
                };
            }

            var OrigAudio = window.Audio;
            if (typeof OrigAudio === 'function') {
                var TrackedAudio = function Audio(src) {
                    var el = new OrigAudio(src);
                    remember(el);
                    return el;
                };
                TrackedAudio.prototype = OrigAudio.prototype;
                window.Audio = TrackedAudio;
            }

            // Закрытый Shadow DOM не обойти querySelectorAll, поэтому ловим
            // элементы в момент создания — так они попадут в tracked.
            var Doc = window.Document;
            if (Doc && Doc.prototype) {
                if (typeof Doc.prototype.createElement === 'function') {
                    var origCreate = Doc.prototype.createElement;
                    Doc.prototype.createElement = function () {
                        var el = origCreate.apply(this, arguments);
                        try { remember(el); } catch (e) { }
                        return el;
                    };
                }

                if (typeof Doc.prototype.createElementNS === 'function') {
                    var origCreateNS = Doc.prototype.createElementNS;
                    Doc.prototype.createElementNS = function () {
                        var el = origCreateNS.apply(this, arguments);
                        try { remember(el); } catch (e) { }
                        return el;
                    };
                }
            }
        } catch (e) { /* не критично — останется обычный поиск по DOM */ }
    }

    /* ---------- перехват источника в графе сайта ----------
     *
     * Яндекс Музыка и подобные плееры сами создают
     * ctx.createMediaElementSource(el) и соединяют его со своим графом.
     * Если этот вызов не перехватить, наш собственный createMediaElementSource
     * падает (элемент уже занят), и слышна только скорость через playbackRate.
     *
     * Поэтому встраиваемся в граф сайта: возвращаем ему выход нашего
     * фильтр-графа, созданного в том же AudioContext.
     */

    function installContextHooks() {
        try {
            // createMediaElementSource живёт на AudioContext, а не на BaseAudioContext
            var protos = [];
            if (window.AudioContext && window.AudioContext.prototype)
                protos.push(window.AudioContext.prototype);
            if (window.webkitAudioContext && window.webkitAudioContext !== window.AudioContext &&
                window.webkitAudioContext.prototype)
                protos.push(window.webkitAudioContext.prototype);

            for (var p = 0; p < protos.length; p++) {
                var proto = protos[p];
                if (proto.__nyaSourceHooked) continue;
                if (typeof proto.createMediaElementSource !== 'function') continue;

                var orig = proto.createMediaElementSource;

                proto.createMediaElementSource = function (el) {
                    // наш собственный контекст — без обёртки
                    if (this.__nyaOurs)
                        return orig.apply(this, arguments);

                    // элемент уже обрабатывается в другом контексте: отдаём сайту
                    // пустой узел, чтобы его граф не упал, звук идёт через наш
                    if (el && el.__nyaSource && el.__nyaSource.context !== this) {
                        try { return this.createGain(); } catch (e) { }
                    }

                    var source = orig.apply(this, arguments);

                    try {
                        if (el) {
                            remember(el);
                            el.__nyaAttached = true;
                            el.__nyaSite = true;
                        }

                        var graph = ensureSiteGraph(this, el);
                        source.connect(graph.low);
                        graph.sources.push(source);

                        if (el) {
                            el.__nyaSource = source;
                            el.__nyaGraph = graph;
                        }

                        applyGraph(graph, settings);

                        // некоторые плееры ожидают у узла свойство mediaElement
                        try { graph.output.mediaElement = el; } catch (e) { }

                        return graph.output;
                    } catch (e) {
                        return source;
                    }
                };

                proto.__nyaSourceHooked = true;
                contextHooked = true;
            }
        } catch (e) { /* сайт останется без встроенных эффектов */ }
    }

    /* ---------- перехват captureStream ----------
     *
     * Яндекс Музыка забирает звук элемента через el.captureStream() и играет
     * его в своём AudioContext. В этом случае наш MediaElementSource тоже
     * подключается, но звук идёт мимо него — слышна только скорость.
     *
     * Возвращаем сайту поток с уже обработанным звуком: наш MediaElementSource
     * забирает элемент, пропускает его через фильтры и отдаёт результат в
     * MediaStreamDestination.
     */

    function installCaptureHooks() {
        try {
            var proto = window.HTMLMediaElement && window.HTMLMediaElement.prototype;
            if (!proto || typeof proto.captureStream !== 'function') return;

            var orig = proto.captureStream;

            proto.captureStream = function () {
                var el = this;

                try {
                    if (el.mediaKeys)
                        return orig.apply(this, arguments);

                    captureCalls++;
                    remember(el);

                    var graph = el.__nyaCaptureGraph;
                    if (!graph || !graph.context || graph.context.state === 'closed') {
                        var AudioCtor = window.AudioContext || window.webkitAudioContext;
                        if (!AudioCtor)
                            return orig.apply(this, arguments);

                        var captureCtx = new AudioCtor();
                        try { captureCtx.__nyaOurs = true; } catch (e) { }

                        var source = captureCtx.createMediaElementSource(el);
                        graph = buildChain(captureCtx, false); // не подключаем к колонкам
                        source.connect(graph.low);
                        graph.sources.push(source);
                        graph.el = el;
                        graph.capture = true;

                        graph.dest = captureCtx.createMediaStreamDestination();
                        graph.output.connect(graph.dest);

                        siteGraphs.push(graph);

                        el.__nyaCaptureGraph = graph;
                        el.__nyaSource = source;
                        el.__nyaAttached = true;
                        currentEl = el;
                    }

                    try { graph.context.resume(); } catch (e) { }
                    applyGraph(graph, settings);

                    return graph.dest.stream;
                } catch (e) {
                    return orig.apply(this, arguments);
                }
            };
        } catch (e) { /* сайт получит исходный поток */ }
    }

    installMediaHooks();
    installContextHooks();
    installCaptureHooks();

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

    // буфер реверберации общий для всех графов одного контекста
    function impulseFor(context) {
        for (var i = 0; i < impulses.length; i++) {
            if (impulses[i].context === context)
                return impulses[i].buffer;
        }

        var buffer = makeImpulse(context, 2.4, 2.0);
        impulses.push({ context: context, buffer: buffer });
        return buffer;
    }

    /// Граф нужен только для эквалайзера и эффектов. Скорость работает и без него
    /// через playbackRate, поэтому на незнакомых сайтах не трогаем звук,
    /// пока пользователь сам не включит эффект.
    function needsGraph() {
        return Math.abs(Number(settings.bass) || 0) > 0.01 ||
            Math.abs(Number(settings.mid) || 0) > 0.01 ||
            Math.abs(Number(settings.treble) || 0) > 0.01 ||
            Number(settings.reverb) > 0.001 ||
            !!settings.underwater;
    }

    function buildChain(context, attachToDestination) {
        // выше частоты Найквиста фильтр не должен ничего срезать
        var maxFreq = context.sampleRate / 2 - 1;

        var low = context.createBiquadFilter();
        low.type = 'lowshelf';
        low.frequency.value = 180;

        var mid = context.createBiquadFilter();
        mid.type = 'peaking';
        mid.frequency.value = 1000;
        mid.Q.value = 0.9;

        var high = context.createBiquadFilter();
        high.type = 'highshelf';
        high.frequency.value = 4500;

        var lowpass = context.createBiquadFilter();
        lowpass.type = 'lowpass';
        lowpass.frequency.value = maxFreq;
        lowpass.Q.value = 0.0001;

        var dry = context.createGain();
        dry.gain.value = 1;

        var wet = context.createGain();
        wet.gain.value = 0;

        var output = context.createGain();
        output.gain.value = 1;

        low.connect(mid);
        mid.connect(high);
        high.connect(lowpass);
        lowpass.connect(dry);
        dry.connect(output);

        var convolver = null;
        try {
            convolver = context.createConvolver();
            convolver.buffer = impulseFor(context);
            lowpass.connect(convolver);
            convolver.connect(wet);
            wet.connect(output);
        } catch (e) {
            // реверберация недоступна — останутся эквалайзер и «под водой»
            convolver = null;
            wet = null;
        }

        if (attachToDestination)
            output.connect(context.destination);

        return {
            context: context,
            maxFreq: maxFreq,
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
    }

    function ensureGraph() {
        if (ownGraph) return ownGraph;

        var AudioCtor = window.AudioContext || window.webkitAudioContext;
        if (!AudioCtor) return null;

        try {
            ctx = new AudioCtor();
        } catch (e) {
            return null;
        }

        try { ctx.__nyaOurs = true; } catch (e) { }

        ownGraph = buildChain(ctx, true);
        return ownGraph;
    }

    // отдельный фильтр-граф на каждый элемент в контексте сайта:
    // сайт соединяет наш выход со своим графом ровно один раз
    function ensureSiteGraph(context, el) {
        if (el && el.__nyaGraph && el.__nyaGraph.context === context)
            return el.__nyaGraph;

        var graph = buildChain(context, false);
        graph.el = el || null;
        siteGraphs.push(graph);

        if (siteGraphs.length > 16) {
            for (var i = 0; i < siteGraphs.length && siteGraphs.length > 12; i++) {
                var old = siteGraphs[i];
                if (old.el && old.el !== currentEl && old.el !== el &&
                    (old.el.paused || old.el.ended)) {
                    siteGraphs.splice(i, 1);
                    i--;
                }
            }
            if (siteGraphs.length > 16)
                siteGraphs.splice(0, siteGraphs.length - 16);
        }

        return graph;
    }

    function resumeContext() {
        var contexts = [];
        if (ctx) contexts.push(ctx);
        for (var i = 0; i < siteGraphs.length; i++)
            contexts.push(siteGraphs[i].context);

        for (var j = 0; j < contexts.length; j++) {
            var current = contexts[j];
            if (!current || current.state === 'running') continue;
            try { current.resume(); } catch (e) { /* ждём жеста пользователя */ }
        }
    }

    /* ---------- привязка к плееру ---------- */

    // Некоторые плееры держат <video>/<audio> в открытом Shadow DOM —
    // обычный querySelectorAll его не видит, поэтому обходим вложенные корни.
    function collectShadowMedia(root, out, depth) {
        if (!root || depth > 6) return;

        var all;
        try { all = root.querySelectorAll('*'); } catch (e) { return; }

        for (var i = 0; i < all.length; i++) {
            var shadow = all[i].shadowRoot;
            if (!shadow) continue;

            var found;
            try { found = shadow.querySelectorAll('video, audio'); } catch (e) { found = []; }
            for (var j = 0; j < found.length; j++)
                out.push(found[j]);

            collectShadowMedia(shadow, out, depth + 1);
        }
    }

    function pickMedia(media) {
        var fallback = null;

        for (var i = 0; i < media.length; i++) {
            var el = media[i];

            // сначала ищем реально играющий элемент, а не служебный <audio>
            if (!el.paused)
                return el;

            if (!fallback && (el.currentSrc || el.readyState >= 2))
                fallback = el;
        }

        return fallback || currentEl || media[0] || null;
    }

    function allMedia() {
        var media = document.querySelectorAll('video, audio');
        for (var i = 0; i < media.length; i++)
            remember(media[i]);

        return tracked;
    }

    function findMediaElement() {
        var media = allMedia();
        if (media.length)
            return pickMedia(media);

        // обход Shadow DOM дорогой, поэтому не чаще раза в 5 секунд
        var now = Date.now();
        if (now - lastDeepScan < 5000)
            return null;
        lastDeepScan = now;

        var deep = [];
        collectShadowMedia(document, deep, 0);
        for (var i = 0; i < deep.length; i++)
            remember(deep[i]);

        return pickMedia(deep);
    }

    function attach(el) {
        if (!el || el.__nyaAttached) return false;

        // Защищённый DRM-поток нельзя заворачивать в Web Audio:
        // получится тишина. Оставляем его родному плееру.
        if (el.mediaKeys) {
            el.__nyaDrm = true;
            el.__nyaAttached = true;
            return false;
        }

        var graph = ensureGraph();
        if (!graph) return false;

        var source;
        try {
            source = ctx.createMediaElementSource(el);
        } catch (e) {
            // элемент уже был привязан к контексту сайта
            el.__nyaAttached = true;
            return false;
        }

        source.connect(graph.low);
        graph.sources.push(source);

        el.__nyaSource = source;
        el.__nyaAttached = true;
        currentEl = el;

        resumeContext();
        apply();
        return true;
    }

    /* ---------- применение настроек ---------- */

    function applyMediaRate() {
        var rate = clamp(Number(settings.speed) || 1, 0.5, 2.5);
        var preserve = !!settings.preservePitch;

        for (var i = 0; i < tracked.length; i++) {
            var el = tracked[i];
            try {
                if (Math.abs((el.playbackRate || 1) - rate) > 0.001)
                    el.playbackRate = rate;

                el.preservesPitch = preserve;
                el.mozPreservesPitch = preserve;
                el.webkitPreservesPitch = preserve;
            } catch (e) { /* элемент мог быть выгружен */ }
        }
    }

    function applyGraph(graph, s) {
        if (!graph) return;

        rampParam(graph.low.gain, Number(s.bass) || 0, graph.context);
        rampParam(graph.mid.gain, Number(s.mid) || 0, graph.context);
        rampParam(graph.high.gain, Number(s.treble) || 0, graph.context);

        var depth = clamp(Number(s.underwaterDepth) || 0, 0, 1);
        var reverb = clamp(Number(s.reverb) || 0, 0, 1);

        if (s.underwater) {
            // ровный «приглушённый» звук: постоянная частота среза, без качаний
            rampParam(graph.lowpass.frequency, 1200 - 950 * depth, graph.context); // 1200 Гц .. 250 Гц
            rampParam(graph.lowpass.Q, 0.9, graph.context);
            reverb = Math.max(reverb, 0.18);
        } else {
            rampParam(graph.lowpass.frequency, graph.maxFreq, graph.context);
            rampParam(graph.lowpass.Q, 0.0001, graph.context);
        }

        if (graph.wet)
            rampParam(graph.wet.gain, reverb, graph.context);

        rampParam(graph.dry.gain, graph.wet ? 1 - 0.4 * reverb : 1, graph.context);
    }

    function apply() {
        var s = settings;

        applyMediaRate();

        if (ownGraph)
            applyGraph(ownGraph, s);

        for (var i = 0; i < siteGraphs.length; i++)
            applyGraph(siteGraphs[i], s);
    }

    /* ---------- периодическая проверка ---------- */

    function tick() {
        var el = findMediaElement();
        if (el) {
            remember(el);
            currentEl = el;
        }

        if (!currentEl) return;

        if (!currentEl.__nyaAttached) {
            if (currentEl.mediaKeys) {
                currentEl.__nyaDrm = true;
                currentEl.__nyaAttached = true;
            } else if (needsGraph()) {
                // Даём сайту около секунды, чтобы он сам создал источник
                // в своём AudioContext: тогда эффекты встроятся в его граф.
                var seen = currentEl.__nyaSeen;
                if (!seen) {
                    currentEl.__nyaSeen = Date.now();
                    seen = currentEl.__nyaSeen;
                }
                if (Date.now() - seen >= 1200)
                    attach(currentEl);
            }
        }

        // Плееры на SPA-сайтах пересоздают <video>/<audio> при смене трека,
        // поэтому всегда держимся за актуальный элемент, а не за первый найденный.

        // если контекст создан, но браузер его усыпил — будим на каждом тике,
        // пока играет музыка (жест пользователя на странице уже был)
        if (!currentEl.paused) resumeContext();

        applyMediaRate();
    }

    function armUserGesture() {
        var once = function () {
            resumeContext();
            document.removeEventListener('pointerdown', once, true);
            document.removeEventListener('keydown', once, true);
        };
        document.addEventListener('pointerdown', once, true);
        document.addEventListener('keydown', once, true);
        document.addEventListener('play', resumeContext, true);
    }

    /* ---------- синхронизация настроек с iframe ----------
     *
     * C# умеет выполнять скрипт только в верхнем документе. Если плеер
     * живёт во вложенном фрейме, верхнее окно передаёт настройки вниз
     * через postMessage, а фрейм при загрузке сам их запрашивает.
     */

    function broadcastToFrames() {
        var payload = Object.assign({}, settings);
        for (var i = 0; i < window.frames.length; i++) {
            try {
                window.frames[i].postMessage({ __nyaSettings: payload }, '*');
            } catch (e) { /* фрейм мог ещё не загрузиться */ }
        }
    }

    function applySettings(partial) {
        if (partial && typeof partial === 'object') {
            settings = Object.assign({}, settings, partial);
        }

        tick();
        apply();
        resumeContext();
        broadcastToFrames();
        return true;
    }

    window.addEventListener('message', function (event) {
        var data = event.data;
        if (!data || typeof data !== 'object') return;

        if (data.__nyaNeedSettings) {
            try {
                event.source.postMessage({ __nyaSettings: Object.assign({}, settings) }, '*');
            } catch (e) { /* источник мог закрыться */ }
            return;
        }

        if (data.__nyaSettings && typeof data.__nyaSettings === 'object') {
            // верхнее окно получает настройки только от приложения (C#),
            // а фреймы — от верхнего окна или родителя
            if (window !== window.top)
                applySettings(data.__nyaSettings);
        }
    });

    function requestSettingsFromTop() {
        if (window === window.top) return;
        try {
            window.top.postMessage({ __nyaNeedSettings: true }, '*');
        } catch (e) { /* кросс-доменный фрейм — postMessage всё равно разрешён */ }
    }

    /* ---------- публичное API ---------- */

    window.__nothingAudio = {
        set: applySettings,
        get: function () {
            return Object.assign({}, settings);
        },
        reset: function () {
            settings = Object.assign({}, defaults);
            apply();
            broadcastToFrames();
            return true;
        },
        state: function () {
            var ownSources = ownGraph ? ownGraph.sources.length : 0;
            var siteSources = 0;
            for (var i = 0; i < siteGraphs.length; i++)
                siteSources += siteGraphs[i].sources.length;

            var g = siteGraphs[0] || ownGraph;
            var eq = null;
            if (g) {
                eq = {
                    bass: Math.round(g.low.gain.value * 10) / 10,
                    mid: Math.round(g.mid.gain.value * 10) / 10,
                    treble: Math.round(g.high.gain.value * 10) / 10,
                    lowpass: Math.round(g.lowpass.frequency.value),
                    wet: g.wet ? Math.round(g.wet.gain.value * 100) / 100 : null
                };
            }

            return {
                attached: !!currentEl,
                context: ctx ? ctx.state : 'none',
                speed: settings.speed,
                drm: !!(currentEl && currentEl.__nyaDrm),
                tracked: tracked.length,
                rate: currentEl ? currentEl.playbackRate : null,
                ownSources: ownSources,
                siteSources: siteSources,
                siteGraphs: siteGraphs.length,
                hooked: contextHooked,
                captureCalls: captureCalls,
                eq: eq
            };
        }
    };

    armUserGesture();
    setInterval(tick, 1000);
    document.addEventListener('DOMContentLoaded', function () {
        tick();
        requestSettingsFromTop();
    });

    if (window !== window.top)
        requestSettingsFromTop();

    // Смена трека/пересоздание плеера: применяем настройки сразу, не ждём таймер.
    ['play', 'playing', 'loadedmetadata', 'canplay', 'emptied'].forEach(function (ev) {
        document.addEventListener(ev, function () { tick(); }, true);
    });

    tick();
})();
