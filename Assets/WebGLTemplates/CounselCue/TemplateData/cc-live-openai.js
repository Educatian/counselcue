// cc-live-openai v4 (2026-10-02)
// CounselCue live mode on OpenAI Realtime (speech to speech).
//
// The Unity bridge (CounselCueWebBridge.jslib) ships a Gemini Live client. When the API worker
// reports liveProvider "openai" (no Gemini key, OPENAI_API_KEY set), this adapter takes over
// window.CounselCueWeb.liveStart/Stop/Mute/Text/Hint and speaks the same SendMessage protocol
// to Unity (OnLiveState, OnLivePartial, OnLiveTurn, OnLiveLevel), so the session controller,
// the face and the Jev analysis of every turn work unchanged. Gemini stays the default
// whenever the worker has a Gemini key.
//
// With no working realtime key the adapter runs a hands-free relay instead: the browser's speech
// recognition ends each counselor turn on a short pause, /turn (Gemini 3.8) writes the reply and
// its AffectPlan, and /voice speaks it. Same Unity protocol, so Jev still analyses every turn.
//
// Mic -> 24 kHz PCM16 -> WebSocket (ephemeral client secret from /live-token; persona, voice
// and turn detection are locked server-side) -> 24 kHz PCM16 playback.
(function () {
  "use strict";
  var RATE = 24000;
  var WORKLET =
    "class P extends AudioWorkletProcessor{constructor(){super();this.r=sampleRate/" + RATE + ";this.a=0;this.s=0;this.c=0;this.o=new Int16Array(1200);this.n=0;this.m=false;this.port.onmessage=e=>{this.m=!!e.data.mute}}" +
    "process(i){const x=i[0]&&i[0][0];if(!x)return true;let e=0;for(let k=0;k<x.length;k++){const v=this.m?0:x[k];e+=v*v;this.s+=v;this.c++;this.a+=1;if(this.a>=this.r){this.a-=this.r;const y=Math.max(-1,Math.min(1,this.s/this.c));this.s=0;this.c=0;this.o[this.n++]=y<0?y*32768:y*32767;" +
    "if(this.n===this.o.length){this.port.postMessage({pcm:this.o.buffer},[this.o.buffer]);this.o=new Int16Array(1200);this.n=0}}}this.port.postMessage({level:Math.sqrt(e/x.length)});return true}}registerProcessor('cc-pcm24',P);";

  function b64(buffer) {
    var bytes = new Uint8Array(buffer), out = "";
    for (var k = 0; k < bytes.length; k += 0x8000) out += String.fromCharCode.apply(null, bytes.subarray(k, k + 0x8000));
    return btoa(out);
  }

  function install(S) {
    if (S.ccOpenAi) return;
    S.ccOpenAi = true;
    var L = S.lv;
    var orig = { start: S.liveStart, stop: S.liveStop, mute: S.liveMute, text: S.liveText, hint: S.liveHint };
    var O = null;
    var providerPromise = null;

    var toUnity = function (method, value) {
      try { window.CounselCueUnity.SendMessage(S.o, method, value); } catch (e) {}
    };
    var setState = function (value) { L.state = value; toUnity("OnLiveState", value); if (S.liveBadge) S.liveBadge(); };
    var send = function (payload) { if (O && O.ws && O.ws.readyState === 1) O.ws.send(JSON.stringify(payload)); };
    var partialAt = 0;
    var partial = function (role, text) {
      var now = Date.now();
      if (now - partialAt < 150) return;
      partialAt = now;
      toUnity("OnLivePartial", JSON.stringify({ role: role, text: String(text).slice(-240) }));
    };

    var provider = function () {
      if (!providerPromise) {
        providerPromise = fetch(S.a + "/health", { cache: "no-store" })
          .then(function (r) { return r.ok ? r.json() : {}; })
          .then(function (h) { return (h.services && h.services.liveProvider) || (h.services && h.services.live ? "gemini" : ""); })
          .catch(function () { providerPromise = null; return "gemini"; });
      }
      return providerPromise;
    };

    // ---- turns: one counselor utterance and the client's reply ------------------------------
    var newTurn = function (inText, inDone) {
      flushTurn();
      O.turn = { inText: inText || "", inDone: !!inDone, outText: "", outDone: false, interrupted: false, sent: false, timer: 0 };
      return O.turn;
    };
    var emitTurn = function (t) {
      if (!t || t.sent) return;
      t.sent = true;
      clearTimeout(t.timer);
      var counselor = t.inText.trim(), client = t.outText.trim();
      if (counselor || client) toUnity("OnLiveTurn", JSON.stringify({ counselor: counselor, client: client, interrupted: !!t.interrupted }));
    };
    var flushTurn = function () { if (O && O.turn && !O.turn.sent && (O.turn.outDone || O.turn.outText)) emitTurn(O.turn); };
    // The reply usually finishes before the counselor's transcript; wait briefly for it so
    // Jev analyses the counselor's words against what the client said.
    var maybeEmit = function (t) {
      if (!t || t.sent || !t.outDone) return;
      if (t.inDone) emitTurn(t);
      else if (!t.timer) t.timer = setTimeout(function () { emitTurn(t); }, 1500);
    };

    // ---- playback ---------------------------------------------------------------------------
    var stopPlayback = function () {
      if (!O) return;
      for (var k = 0; k < O.queue.length; k++) { try { O.queue[k].stop(); } catch (e) {} }
      O.queue = []; O.nextTime = 0;
    };
    var maybeListening = function () {
      if (O && O.speaking && O.queue.length === 0 && O.replyDone) { O.speaking = false; L.speaking = false; setState("listening"); }
    };
    var play = function (base64, itemId) {
      if (!O || !O.out) return;
      var raw = atob(base64), n = raw.length >> 1;
      if (!n) return;
      var f = new Float32Array(n);
      for (var k = 0; k < n; k++) { var v = raw.charCodeAt(2 * k) | (raw.charCodeAt(2 * k + 1) << 8); f[k] = (v >= 32768 ? v - 65536 : v) / 32768; }
      var buffer = O.out.createBuffer(1, n, RATE);
      buffer.copyToChannel(f, 0);
      var source = O.out.createBufferSource();
      source.buffer = buffer;
      source.connect(O.gain);
      var start = Math.max(O.out.currentTime + 0.03, O.nextTime);
      if (itemId && O.itemId !== itemId) { O.itemId = itemId; O.itemStart = start; }
      source.start(start);
      O.nextTime = start + buffer.duration;
      O.queue.push(source);
      source.onended = function () { if (!O) return; var i = O.queue.indexOf(source); if (i >= 0) O.queue.splice(i, 1); maybeListening(); };
      if (!O.speaking) { O.speaking = true; L.speaking = true; setState("speaking"); }
    };
    // Barge-in: the counselor started talking over the client.
    var interrupt = function () {
      if (!O || !O.speaking) return;
      if (O.itemId && O.out) {
        var played = Math.max(0, Math.round((O.out.currentTime - O.itemStart) * 1000));
        send({ type: "conversation.item.truncate", item_id: O.itemId, content_index: 0, audio_end_ms: played });
      }
      stopPlayback();
      if (O.turn) { O.turn.interrupted = true; O.turn.outDone = true; maybeEmit(O.turn); }
      O.speaking = false; L.speaking = false; O.replyDone = true;
      setState("listening");
    };

    var onEvent = function (m) {
      if (!O) return;
      switch (m.type) {
        case "session.created":
        case "session.updated":
          if (!O.ready) { O.ready = true; L.ready = true; setState("listening"); }
          break;
        case "input_audio_buffer.speech_started":
          interrupt();
          partial("counselor", "…");
          break;
        case "input_audio_buffer.committed":
          newTurn("", false);
          O.turn.userItem = m.item_id;
          break;
        case "conversation.item.input_audio_transcription.delta":
          if (O.turn && !O.turn.inDone && (!O.turn.userItem || O.turn.userItem === m.item_id)) { O.turn.inText += m.delta || ""; partial("counselor", O.turn.inText); }
          break;
        case "conversation.item.input_audio_transcription.completed":
          var t = O.turn && (!O.turn.userItem || O.turn.userItem === m.item_id) ? O.turn : null;
          if (t) { t.inText = m.transcript || t.inText; t.inDone = true; partial("counselor", t.inText); maybeEmit(t); }
          break;
        case "conversation.item.input_audio_transcription.failed":
          if (O.turn) { O.turn.inDone = true; maybeEmit(O.turn); }
          break;
        case "response.created":
          if (!O.turn || O.turn.outDone) newTurn("", true);
          O.replyDone = false;
          break;
        case "response.output_audio.delta":
        case "response.audio.delta":
          play(m.delta, m.item_id);
          break;
        case "response.output_audio_transcript.delta":
        case "response.audio_transcript.delta":
          if (O.turn) { O.turn.outText += m.delta || ""; partial("client", O.turn.outText); }
          break;
        case "response.output_audio_transcript.done":
        case "response.audio_transcript.done":
          if (O.turn && m.transcript) O.turn.outText = m.transcript;
          break;
        case "response.done":
          if (O.turn) {
            if (m.response && m.response.status === "cancelled") O.turn.interrupted = true;
            O.turn.outDone = true;
            maybeEmit(O.turn);
          }
          O.replyDone = true;
          maybeListening();
          break;
        case "error":
          console.warn("[CounselCue live] OpenAI Realtime error", m.error && (m.error.code || m.error.message));
          break;
      }
    };

    // ---- lifecycle --------------------------------------------------------------------------
    var stopOpenAi = function (silent) {
      var s = O;
      O = null;
      L.active = false; L.ready = false; L.speaking = false;
      if (!s) return;
      s.closing = true;
      clearTimeout(s.turn && s.turn.timer);
      try { if (s.ws) s.ws.close(1000); } catch (e) {}
      for (var k = 0; k < s.queue.length; k++) { try { s.queue[k].stop(); } catch (e) {} }
      if (s.meter) clearInterval(s.meter);
      if (s.stream) s.stream.getTracks().forEach(function (tr) { tr.stop(); });
      try { if (s.inCtx) s.inCtx.close(); if (s.out) s.out.close(); } catch (e) {}
      if (!silent && L.state !== "off") setState("off");
    };

    var startOpenAi = function (cfg) {
      stopOpenAi(true);
      if (!navigator.mediaDevices || !window.AudioWorkletNode || !window.WebSocket) { toUnity("OnLiveState", "error:unsupported"); return; }
      var s = O = { queue: [], nextTime: 0, speaking: false, replyDone: true, ready: false, turn: null, itemId: "", itemStart: 0 };
      L.active = true; L.muted = false; L.hints = !!cfg.hints; L.inText = ""; L.outText = "";
      setState("connecting");
      var media = navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true } });
      var token = fetch(S.a + "/live-token", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ sessionId: cfg.sessionId, caseId: cfg.caseId, phase: cfg.phase, openingLine: cfg.openingLine,
          safety: cfg.safety, guardedness: cfg.guardedness, disclosure: cfg.disclosure, expression: cfg.expression }),
      }).then(function (r) {
        if (r.ok) return r.json();
        return r.json().catch(function () { return {}; }).then(function (j) { var err = Error("token " + r.status); err.relay = r.status === 502 || r.status === 503; err.detail = j; throw err; });
      });
      Promise.all([media, token]).then(function (res) {
        if (O !== s) { res[0].getTracks().forEach(function (tr) { tr.stop(); }); return; }
        var tk = res[1];
        if (tk.provider !== "openai") throw Error("provider " + tk.provider);
        s.stream = res[0];
        s.inCtx = new AudioContext();
        s.out = new AudioContext({ sampleRate: RATE });
        s.gain = s.out.createGain(); s.analyser = s.out.createAnalyser(); s.analyser.fftSize = 512;
        s.gain.connect(s.analyser); s.analyser.connect(s.out.destination);
        try { s.inCtx.resume(); s.out.resume(); } catch (e) {}
        var url = URL.createObjectURL(new Blob([WORKLET], { type: "application/javascript" }));
        return s.inCtx.audioWorklet.addModule(url).then(function () {
          URL.revokeObjectURL(url);
          if (O !== s) return;
          var src = s.inCtx.createMediaStreamSource(s.stream);
          s.node = new AudioWorkletNode(s.inCtx, "cc-pcm24");
          s.node.port.postMessage({ mute: L.muted });
          s.node.port.onmessage = function (e) {
            if (e.data.pcm) { if (s.ready && !L.muted) send({ type: "input_audio_buffer.append", audio: b64(e.data.pcm) }); }
          };
          src.connect(s.node);
          var ws = new WebSocket(tk.wsUrl + "?model=" + encodeURIComponent(tk.model), ["realtime", "openai-insecure-api-key." + tk.token]);
          s.ws = ws;
          ws.onmessage = function (e) { var m; try { m = JSON.parse(e.data); } catch (x) { return; } if (O === s) onEvent(m); };
          ws.onclose = function (e) {
            if (O !== s || s.closing) return;
            console.warn("[CounselCue live] OpenAI Realtime closed", e.code, e.reason);
            flushTurn();
            stopOpenAi(true);
            toUnity("OnLiveState", "error:closed:" + e.code);
          };
          var data = new Float32Array(512), last = -1;
          s.meter = setInterval(function () {
            if (!s.speaking) { if (last !== 0) { last = 0; toUnity("OnLiveLevel", "0"); } return; }
            s.analyser.getFloatTimeDomainData(data);
            var en = 0; for (var k = 0; k < data.length; k++) en += data[k] * data[k];
            var level = Math.min(1, Math.sqrt(en / data.length) * 5.5);
            if (Math.abs(level - last) > 0.04) { last = level; toUnity("OnLiveLevel", level.toFixed(2)); }
          }, 70);
        });
      }).catch(function (e) {
        if (O !== s) return;
        var denied = e && (e.name === "NotAllowedError" || e.name === "SecurityError");
        stopOpenAi(true);
        if (!denied && e && e.relay && Recognition) {
          // The realtime key was rejected upstream: stay hands-free on the relay for this page.
          console.warn("[CounselCue live] realtime unavailable, using relay", e.detail && (e.detail.error + " " + (e.detail.upstream || "")));
          providerPromise = Promise.resolve("relay");
          media.then(function (m) { m.getTracks().forEach(function (tr) { tr.stop(); }); }).catch(function () {});
          startRelay(cfg);
          return;
        }
        toUnity("OnLiveState", denied ? "error:mic" : "error:" + String((e && e.message) || e).slice(0, 60));
      });
    };

    // ---- relay: browser speech recognition -> /turn -> /voice -----------------------------
    var R = null;
    var Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    var stopRelay = function (silent) {
      var r = R;
      R = null;
      L.active = false; L.ready = false; L.speaking = false;
      if (!r) return;
      r.closing = true;
      clearTimeout(r.endTimer);
      try { r.rec.abort(); } catch (e) {}
      if (r.ac) try { r.ac.abort(); } catch (e) {}
      if (r.source) try { r.source.stop(); } catch (e) {}
      if (r.meter) clearInterval(r.meter);
      try { if (r.out) r.out.close(); } catch (e) {}
      if (!silent && L.state !== "off") setState("off");
    };
    var relayListen = function (r) {
      if (R !== r || r.busy || r.speaking || L.muted) return;
      r.heard = ""; r.interim = "";
      try { r.rec.start(); } catch (e) {}
    };
    var relaySubmit = function (r, text) {
      text = String(text || "").replace(/\s+/g, " ").trim();
      if (R !== r || !text || r.busy) return;
      r.busy = true;
      try { r.rec.abort(); } catch (e) {}
      partial("counselor", text);
      partialAt = 0;
      partial("client", "…");
      var cfg = r.cfg;
      r.turn++;
      fetch(S.a + "/turn", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ sessionId: cfg.sessionId, caseId: cfg.caseId, turn: r.turn, stage: "", phase: cfg.phase,
          counselorUtterance: text, safety: r.state.safety, guardedness: r.state.guardedness, disclosure: r.state.disclosure,
          openingLine: cfg.openingLine, history: r.history.slice(-8), expression: cfg.expression }),
      }).then(function (res) { if (!res.ok) throw Error("turn " + res.status); return res.json(); }).then(function (t) {
        if (R !== r) return;
        var reply = String(t.reply || "").trim();
        var plan = t.plan || {};
        r.history.push({ counselor: text, client: reply });
        if (plan.affect) toUnity("OnLiveAffect", JSON.stringify({ affect: plan.affect, intensity: Number(plan.intensity) || 0.5 }));
        partialAt = 0;
        partial("client", reply);
        // The turn goes to Unity now, so Jev analyses the counselor's words against the
        // client's previous line while the reply is still being voiced.
        toUnity("OnLiveTurn", JSON.stringify({ counselor: text, client: reply, interrupted: false }));
        return relaySpeak(r, reply, plan);
      }).catch(function (e) {
        console.warn("[CounselCue live] relay turn failed", e && e.message);
      }).then(function () {
        if (R !== r) return;
        r.busy = false;
        if (!r.speaking) relayListen(r);
      });
    };
    // The voice streams (ElevenLabs MP3 through MediaSource) so the client starts speaking on
    // the first audio bytes; other providers return a whole WAV that is decoded and played.
    var relayPlaying = function (r) { r.speaking = true; L.speaking = true; setState("speaking"); };
    var relayDone = function (r, done) {
      r.speaking = false; L.speaking = false; r.source = null;
      if (R === r) { setState("listening"); relayListen(r); }
      done();
    };
    var playStream = function (r, res) {
      return new Promise(function (done) {
        var ms = new MediaSource(), audio = new Audio(), url = URL.createObjectURL(ms), finished = false;
        var finish = function () { if (finished) return; finished = true; try { URL.revokeObjectURL(url); } catch (e) {} relayDone(r, done); };
        audio.src = url;
        try { r.out.createMediaElementSource(audio).connect(r.gain); } catch (e) {}
        r.source = { stop: function () { try { audio.pause(); } catch (e) {} finish(); } };
        audio.onplaying = function () { if (!r.speaking) relayPlaying(r); };
        audio.onended = finish;
        audio.onerror = finish;
        ms.addEventListener("sourceopen", function () {
          var sb = ms.addSourceBuffer("audio/mpeg"), queue = [], ended = false, reader = res.body.getReader();
          var pump = function () {
            if (sb.updating) return;
            if (queue.length) { try { sb.appendBuffer(queue.shift()); } catch (e) { finish(); } }
            else if (ended && ms.readyState === "open") { try { ms.endOfStream(); } catch (e) {} }
          };
          sb.addEventListener("updateend", pump);
          var read = function () {
            reader.read().then(function (x) {
              if (x.done) { ended = true; pump(); return; }
              queue.push(x.value); pump();
              if (audio.paused && !finished) audio.play().catch(function () {});
              read();
            }).catch(function () { ended = true; pump(); });
          };
          read();
        }, { once: true });
      });
    };
    var playBuffer = function (r, audioBuffer) {
      return new Promise(function (done) {
        var source = r.out.createBufferSource();
        source.buffer = audioBuffer;
        source.connect(r.gain);
        r.source = source;
        relayPlaying(r);
        source.onended = function () { relayDone(r, done); };
        source.start();
      });
    };
    var relaySpeak = function (r, reply, plan) {
      if (!reply) return null;
      r.ac = window.AbortController ? new AbortController() : null;
      return fetch(S.a + "/voice", {
        method: "POST", headers: { "Content-Type": "application/json" }, signal: r.ac ? r.ac.signal : undefined,
        body: JSON.stringify({ provider: "elevenlabs", text: reply, spoken: plan.spoken, emotion: plan.affect, intensity: plan.intensity, delivery: plan.delivery,
          expression: r.cfg.expression, caseId: r.cfg.caseId, clientId: S.cid }),
      }).then(function (res) {
        if (!res.ok) throw Error("voice " + res.status);
        if (R !== r) return;
        var type = (res.headers.get("Content-Type") || "").split(";")[0];
        if (type === "audio/mpeg" && res.body && window.MediaSource && MediaSource.isTypeSupported("audio/mpeg")) return playStream(r, res);
        return res.arrayBuffer().then(function (buf) { return r.out.decodeAudioData(buf); }).then(function (a) { if (R === r) return playBuffer(r, a); });
      });
    };
    var startRelay = function (cfg) {
      stopRelay(true);
      if (!Recognition || !window.AudioContext) { toUnity("OnLiveState", "error:unsupported"); return; }
      var r = R = { cfg: cfg, turn: 0, history: [], busy: false, speaking: false, heard: "", interim: "",
        state: { safety: cfg.safety, guardedness: cfg.guardedness, disclosure: cfg.disclosure } };
      L.active = true; L.muted = false; L.hints = true;
      setState("connecting");
      r.out = new AudioContext();
      try { r.out.resume(); } catch (e) {}
      r.gain = r.out.createGain(); r.analyser = r.out.createAnalyser(); r.analyser.fftSize = 512;
      r.gain.connect(r.analyser); r.analyser.connect(r.out.destination);
      var data = new Float32Array(512), last = -1;
      r.meter = setInterval(function () {
        if (!r.speaking) { if (last !== 0) { last = 0; toUnity("OnLiveLevel", "0"); } return; }
        r.analyser.getFloatTimeDomainData(data);
        var en = 0; for (var k = 0; k < data.length; k++) en += data[k] * data[k];
        var level = Math.min(1, Math.sqrt(en / data.length) * 5.5);
        if (Math.abs(level - last) > 0.04) { last = level; toUnity("OnLiveLevel", level.toFixed(2)); }
      }, 70);
      var rec = r.rec = new Recognition();
      rec.lang = "ko-KR"; rec.continuous = true; rec.interimResults = true;
      // End of turn: a final phrase followed by a short silence (no new interim results).
      var arm = function () {
        clearTimeout(r.endTimer);
        r.endTimer = setTimeout(function () { var text = (r.heard + " " + r.interim).trim(); if (text) relaySubmit(r, text); }, r.interim ? 1100 : 650);
      };
      rec.onresult = function (event) {
        if (R !== r || r.busy || r.speaking) return;
        var fin = "", inter = "";
        for (var j = 0; j < event.results.length; j++) {
          if (event.results[j].isFinal) fin += event.results[j][0].transcript; else inter += event.results[j][0].transcript;
        }
        r.heard = fin.trim(); r.interim = inter.trim();
        partial("counselor", (r.heard + " " + r.interim).trim());
        arm();
      };
      rec.onstart = function () { if (R === r && !r.ready) { r.ready = true; L.ready = true; setState("listening"); } };
      rec.onerror = function (e) {
        if (R !== r) return;
        if (e.error === "not-allowed" || e.error === "service-not-allowed") { stopRelay(true); toUnity("OnLiveState", "error:mic"); }
      };
      // Chrome ends continuous recognition after a while; keep listening until the session stops.
      rec.onend = function () { if (R === r && !r.busy && !r.speaking && !L.muted) setTimeout(function () { relayListen(r); }, 120); };
      relayListen(r);
    };

    S.liveStart = function (cfg) {
      if (O) stopOpenAi(true);
      if (R) stopRelay(true);
      provider().then(function (p) {
        if (p === "openai") startOpenAi(cfg);
        else if (p === "relay") startRelay(cfg);
        else orig.start(cfg);
      });
    };
    S.liveStop = function () { if (O) stopOpenAi(false); else if (R) stopRelay(false); else orig.stop(); };
    S.liveMute = function (value) {
      if (R) {
        L.muted = !!value;
        if (L.muted) { clearTimeout(R.endTimer); try { R.rec.abort(); } catch (e) {} } else relayListen(R);
        if (S.liveBadge) S.liveBadge();
        return;
      }
      if (!O) return orig.mute(value);
      L.muted = !!value;
      if (O.node) O.node.port.postMessage({ mute: L.muted });
      if (S.liveBadge) S.liveBadge();
    };
    // Typed text during a live session: a counselor turn without audio.
    S.liveText = function (text) {
      if (R) { if (R.source) try { R.source.stop(); } catch (e) {} relaySubmit(R, text); return; }
      if (!O) return orig.text(text);
      if (!O.ready || !text) return;
      interrupt();
      newTurn(text, true);
      send({ type: "conversation.item.create", item: { type: "message", role: "user", content: [{ type: "input_text", text: text }] } });
      send({ type: "response.create" });
    };
    // Private relationship-state context ("[상담 시스템] ..."): informs the next reply, never answered.
    S.liveHint = function (text) {
      if (R) {
        // Keep the relay's relationship state in step with Unity's coded turns.
        var m = /안전\s*([0-9.]+).*경계\s*([0-9.]+).*개방 의지\s*([0-9.]+)/.exec(String(text || ""));
        if (m) R.state = { safety: +m[1], guardedness: +m[2], disclosure: +m[3] };
        return;
      }
      if (!O) return orig.hint(text);
      if (!O.ready || !L.hints || !text) return;
      send({ type: "conversation.item.create", item: { type: "message", role: "system", content: [{ type: "input_text", text: text }] } });
    };
    provider();
  }

  // The bridge re-creates window.CounselCueWeb whenever Unity re-initialises it, so keep
  // watching and wrap each new instance.
  setInterval(function () {
    var S = window.CounselCueWeb;
    if (S && S.liveStart && S.lv && !S.ccOpenAi) install(S);
  }, 300);
})();
