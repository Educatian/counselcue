mergeInto(LibraryManager.library, {
  CounselCueWeb_Initialize: function (objectPointer, apiPointer) {
    var S = window.CounselCueWeb = {
      o: UTF8ToString(objectPointer),
      a: UTF8ToString(apiPointer).replace(/\/$/, ""),
      on: false,
      i: 0,
      c: "",
      v: 0,
      lang: "ko",
      // Per-page id so the voice rate limit paces each learner, not a whole classroom NAT.
      cid: Math.random().toString(36).slice(2) + Date.now().toString(36)
    };
    S.T = {
      ko: {
        feedback: "상담자의 언어·비언어 전달을 함께 관찰합니다.",
        notice: "AI 생성 내담자 음성 · 원음 미저장",
        input: "상담자 응답",
        placeholder: "응답을 입력하거나 마이크를 누르세요…",
        micLabel: "말하기", micAria: "음성 입력", micUnsupported: "Chrome 또는 Edge에서 음성 입력을 사용할 수 있습니다.",
        send: "응답하기", help: "? 사용 안내", skip: "건너뛰기", next: "다음", start: "시작하기",
        liveConnecting: "● 실시간 음성 연결 중…", liveListening: "● 듣고 있어요 — 말씀하세요", liveSpeaking: "● 내담자가 말하는 중", liveMuted: "● 마이크 꺼짐 (눌러서 켜기)",
        steps: [
          ["내담자의 표정과 자세를 관찰하세요", "얼굴 근육, 시선, 움직임과 말의 내용을 함께 보세요."],
          ["관찰 줌을 활용하세요", "오른쪽 줌 컨트롤로 표정과 제스처를 가까이 확인하세요."],
          ["한글 입력을 지원합니다", "한글 조합, 붙여넣기, Shift+Enter 줄바꿈이 가능합니다."],
          ["마이크로 응답하세요", "최초 1회 브라우저 마이크 권한 승인이 필요합니다. 받아쓰기는 한국어로 인식합니다."],
          ["감정 음성으로 답합니다", "AI 내담자 답변이 사례별 ElevenLabs 음성으로 재생됩니다."]
        ]
      },
      en: {
        feedback: "Observing the counselor's verbal and embodied delivery together.",
        notice: "AI-generated client voice · no audio saved",
        input: "Counselor response",
        placeholder: "Type your response in Korean or press the mic…",
        micLabel: "Speak", micAria: "Voice input", micUnsupported: "Voice input is available in Chrome or Edge.",
        send: "Respond", help: "? Guide", skip: "Skip", next: "Next", start: "Start",
        liveConnecting: "● Connecting live voice…", liveListening: "● Listening — go ahead", liveSpeaking: "● Client is speaking", liveMuted: "● Mic off (tap to unmute)",
        steps: [
          ["Observe the client's face and posture", "Watch facial muscles, gaze and movement together with what is said."],
          ["Use the observation zoom", "Use the zoom controls on the right to look closely at expressions and gestures."],
          ["Korean input is supported", "Korean IME composition, paste, and Shift+Enter line breaks all work."],
          ["Respond with the microphone", "Allow microphone access once. Dictation recognizes Korean speech."],
          ["The client answers with an emotional voice", "The AI client's reply plays in a case-specific ElevenLabs voice."]
        ]
      }
    };
    S.t = function () { return S.T[S.lang] || S.T.ko; };
    // Browser storage can throw (blocked site data, some private modes); the tour
    // flag is a convenience and must never stop the bridge from initializing.
    S.get = function (key) { try { return window.localStorage.getItem(key); } catch (error) { return null; } };
    S.set = function (key, value) { try { window.localStorage.setItem(key, value); } catch (error) {} };
    var canvas = document.querySelector("#unity-canvas");
    var css = document.createElement("style");
    // Tokens match UiTheme in the Unity build: ink glass, hanji paper, celadon, lamp amber.
    css.textContent =
      "#cci{position:fixed;z-index:60;left:50%;bottom:max(14px,env(safe-area-inset-bottom));transform:translateX(-50%);width:min(calc(100vw - 32px),1040px);display:none;flex-direction:column;gap:8px;padding:12px;border-radius:22px;background:#121513f0;box-shadow:0 18px 50px #0008,inset 0 0 0 1px #ffffff12;font-family:'Noto Sans KR','Malgun Gothic','Apple SD Gothic Neo',sans-serif;-webkit-font-smoothing:antialiased}" +
      "#ccf{box-sizing:border-box;width:100%;min-height:24px;max-height:46px;overflow:hidden;padding:2px 8px 0 22px;position:relative;color:#f3efe6;font-size:15px;line-height:21px}" +
      "#ccf:before{content:'';position:absolute;left:6px;top:8px;width:8px;height:8px;border-radius:50%;background:#7fb8a0;box-shadow:0 0 0 4px #7fb8a026}" +
      "#cccontrols{display:flex;gap:10px;width:100%;align-items:stretch}" +
      "#cci textarea{flex:1;min-width:0;height:60px;resize:none;box-sizing:border-box;border:0;border-radius:16px;background:#f6f1e7;color:#1e211f;padding:17px 18px;font:17px/1.45 'Noto Sans KR','Malgun Gothic','Apple SD Gothic Neo',sans-serif;outline:none;box-shadow:inset 0 0 0 1px #0000000f}" +
      "#cci textarea::placeholder{color:#6f736d}" +
      "#cci textarea:focus{box-shadow:inset 0 0 0 2px #2c6352,0 0 0 4px #7fb8a040}" +
      ".ccb{min-width:104px;height:60px;border:0;border-radius:16px;padding:0 20px;background:#2c6352;color:#f6f1e7;font:700 16px/1 'Noto Sans KR','Malgun Gothic','Apple SD Gothic Neo',sans-serif;white-space:nowrap;cursor:pointer;transition:filter .15s,transform .15s}" +
      ".ccb:hover:not(:disabled){filter:brightness(1.12)}.ccb:active:not(:disabled){transform:translateY(1px)}" +
      ".ccb:focus-visible,.ctb:focus-visible,#cch:focus-visible{outline:3px solid #efbe74;outline-offset:3px}" +
      ".ccb:disabled{opacity:.5;cursor:not-allowed}.mic{background:#ffffff14;color:#f3efe6;box-shadow:inset 0 0 0 1px #ffffff26}.mic.on{background:#b84a38;box-shadow:0 0 0 6px #b84a3840}" +
      "#ccn{flex:0 0 150px;align-self:center;box-sizing:border-box;color:#c8cac2;font-size:12px;line-height:1.45;text-align:left;padding:0 4px}" +
      "#ccn.live{color:#9fd0ba;font-weight:700}#ccn.live.speaking{color:#efbe74}#ccn.live.connecting{color:#c8cac2}#ccn.live.muted{color:#e79a86}.mic.live{background:#2c6352;color:#f6f1e7}" +
      "#cch{position:fixed;z-index:61;right:max(16px,env(safe-area-inset-right));bottom:calc(150px + env(safe-area-inset-bottom));border:0;border-radius:999px;background:#121513e6;color:#f3efe6;padding:9px 15px;font:700 13px/1 'Noto Sans KR','Malgun Gothic','Apple SD Gothic Neo',sans-serif;box-shadow:0 8px 24px #0006,inset 0 0 0 1px #ffffff1f;cursor:pointer}" +
      "#cct{position:fixed;inset:0;z-index:100;display:none;font-family:'Noto Sans KR','Malgun Gothic','Apple SD Gothic Neo',sans-serif;pointer-events:none}" +
      "#ccs{position:fixed;border:2px solid #efbe74;border-radius:18px;box-shadow:0 0 0 9999vmax #0000008c,0 0 30px #efbe7466;transition:left .25s,top .25s,width .25s,height .25s}" +
      "#ccp{position:fixed;width:380px;max-width:calc(100vw - 24px);box-sizing:border-box;background:#f6f1e7;border-radius:20px;padding:22px 24px 18px;box-shadow:0 24px 60px #0009;pointer-events:auto;color:#1e211f}" +
      "#ccp h3{margin:0 0 8px;color:#1e211f;font-size:19px;line-height:1.35;word-break:keep-all}#ccp p{margin:0 0 18px;color:#474b46;line-height:1.65;font-size:15px;word-break:keep-all}" +
      ".ctb{border:0;border-radius:12px;padding:11px 16px;font:700 14px/1 'Noto Sans KR','Malgun Gothic','Apple SD Gothic Neo',sans-serif;cursor:pointer}.skip{background:transparent;color:#585c57}.next{float:right;background:#2c6352;color:#f6f1e7}" +
      ".ccart{display:block;width:100%;aspect-ratio:16/9;object-fit:cover;border-radius:14px;margin:0 0 14px;background:#e9e2d4}.ccart[hidden]{display:none}" +
      "@media(max-width:1100px){#ccn{display:none}#cci{width:min(calc(100vw - 24px),900px)}}" +
      "@media(max-width:700px) and (orientation:landscape){#cci{bottom:6px;gap:5px;padding:8px}#ccf{max-height:40px;font-size:13px;line-height:18px}#cci textarea,.ccb{height:48px}#cci textarea{padding:13px 14px;font-size:15px}.ccb{min-width:72px;padding:0 12px;font-size:14px}#cch{bottom:104px;font-size:12px}.lbl{display:none}#ccp{width:330px;padding:16px 18px}#ccp h3{font-size:17px}#ccp p{font-size:14px;margin-bottom:12px}}" +
      "@media(max-height:520px) and (orientation:landscape){#cci{bottom:5px;gap:4px;padding:7px;width:calc(100vw - 12px)!important}#ccf{max-height:36px;font-size:13px;line-height:17px}#cci textarea,.ccb{height:44px}#cci textarea{padding:11px 12px;font-size:15px}.ccb{min-width:72px;padding:0 10px;font-size:14px}#ccn{display:none}#cch{bottom:94px;font-size:12px;padding:7px 11px}.lbl{display:none}#ccp{width:320px;padding:14px 16px}#ccp h3{font-size:17px}#ccp p{font-size:14px;margin-bottom:10px}}" +
      "@media(prefers-reduced-motion:reduce){#ccs,.ccb{transition:none}}";
    document.head.appendChild(css);

    var root = document.createElement("div");
    root.id = "cci";
    root.innerHTML = '<div id="ccf"></div><div id="cccontrols"><span id="ccn"></span><textarea></textarea><button class="ccb mic">● <span class="lbl"></span></button><button class="ccb send"></button></div>';
    document.body.appendChild(root);
    S.r = root;
    S.x = root.querySelector("textarea");
    S.f = root.querySelector("#ccf");
    var mic = root.querySelector(".mic");
    var send = root.querySelector(".send");
    var changed = function () { SendMessage(S.o, "OnWebTextChanged", S.x.value); };
    var submit = function () {
      var value = S.x.value.trim();
      if (S.on && value) SendMessage(S.o, "OnWebTextSubmitted", value);
    };
    S.x.oninput = changed;
    S.x.onkeydown = function (event) {
      if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
        event.preventDefault();
        submit();
      }
    };
    send.onclick = submit;

    S.place = function () {
      var rect = canvas.getBoundingClientRect();
      var viewport = window.visualViewport;
      var visibleHeight = viewport ? viewport.height + viewport.offsetTop : innerHeight;
      var bottom = Math.max(8, visibleHeight - rect.bottom + 12);
      var width = Math.max(300, Math.min(rect.width - 24, 1040));
      Object.assign(root.style, {
        left: Math.max(width / 2 + 8, Math.min(rect.left + rect.width / 2, innerWidth - width / 2 - 8)) + "px",
        bottom: bottom + "px",
        width: width + "px"
      });
    };
    addEventListener("resize", S.place);
    if (window.visualViewport) {
      visualViewport.addEventListener("resize", S.place);
      visualViewport.addEventListener("scroll", S.place);
    }
    S.place();

    var Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (Recognition) {
      var recognition = new Recognition();
      recognition.lang = "ko-KR";
      recognition.interimResults = true;
      // Continuous dictation: counselors pause mid-response, so recognition keeps listening
      // until the mic is pressed again (or 90 s pass), instead of stopping at the first pause.
      recognition.continuous = true;
      var dictationBase = "";
      var listening = false;
      var dictationTimer = 0;
      recognition.onstart = function () {
        listening = true;
        mic.classList.add("on");
        dictationBase = S.x.value.replace(/\s+$/, "");
        clearTimeout(dictationTimer);
        dictationTimer = setTimeout(function () { try { recognition.stop(); } catch (e) {} }, 90000);
      };
      recognition.onresult = function (event) {
        // Rebuild from every result so typed text before dictation is kept and
        // finalized phrases are not dropped when interim results arrive.
        var text = "";
        for (var j = 0; j < event.results.length; j++) text += event.results[j][0].transcript;
        S.x.value = dictationBase ? dictationBase + " " + text.trim() : text.trim();
        changed();
      };
      recognition.onend = function () { listening = false; clearTimeout(dictationTimer); mic.classList.remove("on"); S.x.focus(); };
      recognition.onerror = function () { listening = false; clearTimeout(dictationTimer); mic.classList.remove("on"); };
      mic.onclick = function () {
        if (listening) { try { recognition.stop(); } catch (e) {} return; }
        try { recognition.start(); } catch (error) { try { recognition.stop(); } catch (e) {} }
      };
    } else {
      mic.disabled = true;
      S.micUnsupported = true;
    }

    var tour = document.createElement("div");
    tour.id = "cct";
    tour.innerHTML = '<div id="ccs"></div><div id="ccp"><img class="ccart" alt="" hidden><h3></h3><p></p><button class="ctb skip">건너뛰기</button><button class="ctb next">다음</button></div>';
    document.body.appendChild(tour);
    var help = document.createElement("button");
    help.id = "cch";
    help.textContent = "? 사용 안내";
    document.body.appendChild(help);
    var spotlight = tour.querySelector("#ccs");
    var card = tour.querySelector("#ccp");
    var next = tour.querySelector(".next");
    // Optional first-step illustration (TemplateData/tour-welcome.jpg). It stays hidden if the
    // file is absent, so the tour never shows a broken image.
    var art = tour.querySelector(".ccart");
    art.onload = function () { S.artOk = true; if (tour.style.display === "block") draw(); };
    art.onerror = function () { S.artOk = false; art.hidden = true; };
    art.src = "TemplateData/tour-welcome.jpg";
    var canvasRect = function () { return canvas.getBoundingClientRect(); };
    var canvasArea = function (left, top, width, height) {
      var rect = canvasRect();
      return [rect.left + rect.width * left, rect.top + rect.height * top, rect.width * width, rect.height * height];
    };
    var elementRect = function (element) {
      var rect = element.getBoundingClientRect();
      return [rect.left, rect.top, rect.width, rect.height];
    };
    var targets = [
      function () { return canvasArea(.31, .12, .38, .56); },
      function () { return canvasArea(.757, .305, .225, .125); },
      function () { return elementRect(S.x); },
      function () { return elementRect(mic); },
      function () { return elementRect(send); }
    ];
    var clamp = function (value, min, max) { return Math.max(min, Math.min(max, value)); };
    var draw = function () {
      var step = S.t().steps[S.i];
      var target = targets[S.i]();
      var pad = 7;
      var left = clamp(target[0] - pad, 6, innerWidth - 12);
      var top = clamp(target[1] - pad, 6, innerHeight - 12);
      var width = clamp(target[2] + pad * 2, 24, innerWidth - left - 6);
      var height = clamp(target[3] + pad * 2, 24, innerHeight - top - 6);
      Object.assign(spotlight.style, { left: left + "px", top: top + "px", width: width + "px", height: height + "px" });
      art.hidden = !(S.artOk && S.i === 0);
      card.querySelector("h3").textContent = step[0];
      card.querySelector("p").textContent = step[1];
      card.style.visibility = "hidden";
      tour.style.display = "block";
      var cardWidth = Math.min(370, innerWidth - 24);
      var cardHeight = card.offsetHeight || 190;
      var gap = 16;
      var candidates = [
        [target[0] + target[2] / 2 - cardWidth / 2, target[1] + target[3] + gap],
        [target[0] + target[2] / 2 - cardWidth / 2, target[1] - cardHeight - gap],
        [target[0] + target[2] + gap, target[1] + target[3] / 2 - cardHeight / 2],
        [target[0] - cardWidth - gap, target[1] + target[3] / 2 - cardHeight / 2]
      ];
      var chosen = candidates[0];
      for (var k = 0; k < candidates.length; k++) {
        var point = candidates[k];
        if (point[0] >= 12 && point[1] >= 12 && point[0] + cardWidth <= innerWidth - 12 && point[1] + cardHeight <= innerHeight - 12) {
          chosen = point;
          break;
        }
      }
      card.style.left = clamp(chosen[0], 12, innerWidth - cardWidth - 12) + "px";
      card.style.top = clamp(chosen[1], 12, innerHeight - cardHeight - 12) + "px";
      card.style.visibility = "visible";
      next.textContent = S.i === targets.length - 1 ? S.t().start : S.t().next;
    };
    var closeTour = function () {
      tour.style.display = "none";
      S.set("counselcue-tour-v3", "done");
    };
    next.onclick = function () { if (++S.i >= targets.length) closeTour(); else draw(); };
    tour.querySelector(".skip").onclick = closeTour;
    help.onclick = function () { S.i = 0; draw(); };
    addEventListener("resize", function () { S.place(); if (tour.style.display === "block") draw(); });
    S.show = function () { if (!S.get("counselcue-tour-v3")) { S.i = 0; draw(); } };
    // ---- Gemini Live real-time voice ----------------------------------------------------
    // Mic → 16 kHz PCM16 → WebSocket (ephemeral token from the worker's /live-token, persona
    // locked server-side) → 24 kHz PCM16 playback. Transcripts and states go to Unity through
    // SendMessage: OnLiveState, OnLivePartial, OnLiveTurn, OnLiveLevel.
    var WORKLET = "class P extends AudioWorkletProcessor{constructor(){super();this.r=sampleRate/16000;this.a=0;this.s=0;this.c=0;this.o=new Int16Array(800);this.n=0;this.m=false;this.port.onmessage=e=>{this.m=!!e.data.mute}}" +
      "process(i){const x=i[0]&&i[0][0];if(!x)return true;let e=0;for(let k=0;k<x.length;k++){const v=this.m?0:x[k];e+=v*v;this.s+=v;this.c++;this.a+=1;if(this.a>=this.r){this.a-=this.r;const y=Math.max(-1,Math.min(1,this.s/this.c));this.s=0;this.c=0;this.o[this.n++]=y<0?y*32768:y*32767;" +
      "if(this.n===this.o.length){this.port.postMessage({pcm:this.o.buffer},[this.o.buffer]);this.o=new Int16Array(800);this.n=0}}}this.port.postMessage({level:Math.sqrt(e/x.length)});return true}}registerProcessor('cc-pcm16',P);";
    var L = S.lv = { active: false, muted: false, handle: "", inText: "", outText: "", interrupted: false, queue: [], nextTime: 0, speaking: false, turnDone: false, hints: false };
    var toUnity = function (method, value) { try { SendMessage(S.o, method, value); } catch (e) {} };
    var b64 = function (buffer) {
      var bytes = new Uint8Array(buffer), out = "";
      for (var k = 0; k < bytes.length; k += 0x8000) out += String.fromCharCode.apply(null, bytes.subarray(k, k + 0x8000));
      return btoa(out);
    };
    var setState = function (value) { L.state = value; toUnity("OnLiveState", value); S.liveBadge(); };
    var send = function (payload) { if (L.ws && L.ws.readyState === 1) L.ws.send(JSON.stringify(payload)); };
    var stopPlayback = function () {
      for (var k = 0; k < L.queue.length; k++) { try { L.queue[k].stop(); } catch (e) {} }
      L.queue = []; L.nextTime = 0;
    };
    var finishTurn = function (interrupted) {
      var counselor = L.inText.trim(), client = L.outText.trim();
      L.inText = ""; L.outText = "";
      if (counselor || client) toUnity("OnLiveTurn", JSON.stringify({ counselor: counselor, client: client, interrupted: !!interrupted }));
    };
    var maybeListening = function () {
      if (L.speaking && L.queue.length === 0 && L.turnDone) { L.speaking = false; setState("listening"); }
    };
    var partialAt = 0;
    var partial = function (role, text) {
      var now = Date.now();
      if (now - partialAt < 180) return;
      partialAt = now;
      toUnity("OnLivePartial", JSON.stringify({ role: role, text: text.slice(-240) }));
    };
    var play = function (base64) {
      if (!L.out) return;
      var raw = atob(base64), n = raw.length >> 1, f = new Float32Array(n);
      for (var k = 0; k < n; k++) { var v = raw.charCodeAt(2 * k) | (raw.charCodeAt(2 * k + 1) << 8); f[k] = (v >= 32768 ? v - 65536 : v) / 32768; }
      var buffer = L.out.createBuffer(1, n, 24000);
      buffer.copyToChannel(f, 0);
      var source = L.out.createBufferSource();
      source.buffer = buffer;
      source.connect(L.gain);
      var start = Math.max(L.out.currentTime + 0.04, L.nextTime);
      source.start(start);
      L.nextTime = start + buffer.duration;
      L.queue.push(source);
      source.onended = function () { var i = L.queue.indexOf(source); if (i >= 0) L.queue.splice(i, 1); maybeListening(); };
      if (!L.speaking) { L.speaking = true; L.turnDone = false; setState("speaking"); }
    };
    var onMessage = function (event) {
      var handle = function (text) {
        var m; try { m = JSON.parse(text); } catch (e) { return; }
        if (m.setupComplete) { L.ready = true; setState("listening"); return; }
        if (m.sessionResumptionUpdate && m.sessionResumptionUpdate.resumable && m.sessionResumptionUpdate.newHandle) L.handle = m.sessionResumptionUpdate.newHandle;
        if (m.goAway) { L.reconnect = true; }
        var c = m.serverContent;
        if (!c) return;
        if (c.inputTranscription && c.inputTranscription.text) { L.inText += c.inputTranscription.text; partial("counselor", L.inText); }
        if (c.outputTranscription && c.outputTranscription.text) { L.outText += c.outputTranscription.text; partial("client", L.outText); }
        if (c.modelTurn && c.modelTurn.parts) {
          for (var k = 0; k < c.modelTurn.parts.length; k++) {
            var part = c.modelTurn.parts[k];
            if (part.inlineData && part.inlineData.data && /audio/.test(part.inlineData.mimeType || "audio")) play(part.inlineData.data);
          }
        }
        if (c.interrupted) { stopPlayback(); L.turnDone = true; maybeListening(); finishTurn(true); if (L.speaking) { L.speaking = false; setState("listening"); } }
        if (c.turnComplete) { L.turnDone = true; finishTurn(false); maybeListening(); }
      };
      if (typeof event.data === "string") handle(event.data);
      else if (event.data && event.data.text) event.data.text().then(handle);
    };
    var connect = function () {
      var ws = new WebSocket(L.wsUrl + "?access_token=" + encodeURIComponent(L.token));
      L.ws = ws; L.ready = false;
      ws.onopen = function () {
        var setup = { model: "models/" + L.model, generationConfig: { responseModalities: ["AUDIO"] },
          inputAudioTranscription: {}, outputAudioTranscription: {}, sessionResumption: L.handle ? { handle: L.handle } : {} };
        ws.send(JSON.stringify({ setup: setup }));
      };
      ws.onmessage = onMessage;
      ws.onerror = function () {};
      ws.onclose = function (e) {
        if (!L.active) return;
        // Live sessions end at their time limit or on network drops; resume with the
        // handle while the token still has uses, otherwise hand control back to text.
        if ((L.reconnect || e.code === 1000 || e.code === 1001 || e.code === 1006) && L.retries < 3) {
          L.retries++; L.reconnect = false; setState("connecting"); setTimeout(connect, 400 * L.retries);
        } else { S.liveStop(); toUnity("OnLiveState", "error:closed:" + e.code); }
      };
    };
    S.liveStart = function (cfg) {
      if (L.active) S.liveStop();
      if (!navigator.mediaDevices || !window.AudioWorkletNode || !window.WebSocket) { toUnity("OnLiveState", "error:unsupported"); return; }
      L.active = true; L.retries = 0; L.handle = ""; L.inText = ""; L.outText = ""; L.muted = false; L.hints = !!cfg.hints;
      setState("connecting");
      var mediaPromise = navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true } });
      var tokenPromise = fetch(S.a + "/live-token", { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ sessionId: cfg.sessionId, caseId: cfg.caseId, phase: cfg.phase, openingLine: cfg.openingLine, safety: cfg.safety, guardedness: cfg.guardedness, disclosure: cfg.disclosure }) })
        .then(function (r) { if (!r.ok) throw Error("token " + r.status); return r.json(); });
      Promise.all([mediaPromise, tokenPromise]).then(function (res) {
        if (!L.active) { res[0].getTracks().forEach(function (t) { t.stop(); }); return; }
        L.stream = res[0]; L.token = res[1].token; L.model = res[1].model; L.wsUrl = res[1].wsUrl;
        L.inCtx = new AudioContext();
        L.out = new AudioContext({ sampleRate: 24000 });
        L.gain = L.out.createGain(); L.analyser = L.out.createAnalyser(); L.analyser.fftSize = 512;
        L.gain.connect(L.analyser); L.analyser.connect(L.out.destination);
        var url = URL.createObjectURL(new Blob([WORKLET], { type: "application/javascript" }));
        return L.inCtx.audioWorklet.addModule(url).then(function () {
          URL.revokeObjectURL(url);
          var src = L.inCtx.createMediaStreamSource(L.stream);
          L.node = new AudioWorkletNode(L.inCtx, "cc-pcm16");
          L.node.port.onmessage = function (e) {
            if (e.data.pcm) { if (L.ready && !L.muted) send({ realtimeInput: { audio: { data: b64(e.data.pcm), mimeType: "audio/pcm;rate=16000" } } }); }
            else if (typeof e.data.level === "number") L.micLevel = e.data.level;
          };
          src.connect(L.node);
          connect();
          var data = new Float32Array(512), last = -1;
          L.meter = setInterval(function () {
            if (!L.speaking) { if (last !== 0) { last = 0; toUnity("OnLiveLevel", "0"); } return; }
            L.analyser.getFloatTimeDomainData(data);
            var e = 0; for (var k = 0; k < data.length; k++) e += data[k] * data[k];
            var level = Math.min(1, Math.sqrt(e / data.length) * 5.5);
            if (Math.abs(level - last) > 0.04) { last = level; toUnity("OnLiveLevel", level.toFixed(2)); }
          }, 70);
        });
      }).catch(function (e) {
        var denied = e && (e.name === "NotAllowedError" || e.name === "SecurityError");
        S.liveStop();
        toUnity("OnLiveState", denied ? "error:mic" : "error:" + String((e && e.message) || e).slice(0, 60));
      });
    };
    S.liveStop = function () {
      L.active = false; L.ready = false; L.speaking = false;
      try { if (L.ws) L.ws.close(1000); } catch (e) {}
      L.ws = null;
      stopPlayback();
      if (L.meter) clearInterval(L.meter);
      if (L.stream) L.stream.getTracks().forEach(function (t) { t.stop(); });
      L.stream = null;
      try { if (L.inCtx) L.inCtx.close(); if (L.out) L.out.close(); } catch (e) {}
      L.inCtx = null; L.out = null;
      if (L.state !== "off") setState("off");
    };
    S.liveMute = function (value) {
      L.muted = !!value;
      if (L.node) L.node.port.postMessage({ mute: L.muted });
      S.liveBadge();
    };
    S.liveText = function (text) {
      if (!L.active || !L.ready || !text) return;
      L.inText += (L.inText ? " " : "") + text;
      send({ realtimeInput: { text: text } });
    };
    S.liveHint = function (text) {
      if (!L.active || !L.ready || !L.hints || !text) return;
      send({ clientContent: { turns: [{ role: "user", parts: [{ text: text }] }], turnComplete: false } });
    };
    S.liveBadge = function () {
      var t = S.t(), note = root.querySelector("#ccn");
      if (!L.active) { note.textContent = t.notice; note.className = ""; mic.classList.remove("live"); return; }
      note.className = "live " + (L.muted ? "muted" : L.state || "");
      note.textContent = L.muted ? t.liveMuted : L.state === "speaking" ? t.liveSpeaking : L.state === "connecting" ? t.liveConnecting : t.liveListening;
      mic.classList.add("live");
      mic.disabled = false;
    };
    // In live mode the mic button mutes/unmutes the stream instead of starting dictation.
    var dictate = mic.onclick;
    mic.onclick = function () { if (L.active) S.liveMute(!L.muted); else if (dictate) dictate(); };
    var skip = tour.querySelector(".skip");
    S.applyLang = function () {
      var t = S.t();
      document.documentElement.lang = S.lang;
      if (!S.feedbackSet) S.f.textContent = t.feedback;
      root.querySelector("#ccn").textContent = t.notice;
      if (S.liveBadge) S.liveBadge();
      S.x.setAttribute("aria-label", t.input);
      S.x.placeholder = t.placeholder;
      mic.setAttribute("aria-label", t.micAria);
      mic.querySelector(".lbl").textContent = t.micLabel;
      mic.title = S.micUnsupported ? t.micUnsupported : "";
      send.textContent = t.send;
      help.textContent = t.help;
      skip.textContent = t.skip;
      if (tour.style.display === "block") draw();
    };
    S.applyLang();
  },

  CounselCueWeb_SetEnabled: function (value) {
    var S = window.CounselCueWeb;
    if (!S) return;
    S.on = !!value;
    S.r.style.display = S.on ? "flex" : "none";
    if (S.on) { S.place(); S.x.focus(); setTimeout(S.show, 400); }
  },

  CounselCueWeb_SetText: function (pointer) {
    var S = window.CounselCueWeb;
    if (S) S.x.value = UTF8ToString(pointer);
  },

  CounselCueWeb_SetFeedback: function (pointer) {
    var S = window.CounselCueWeb;
    if (!S) return;
    // Strip Unity rich-text tags without executing markup: DOMParser documents
    // are inert, unlike innerHTML on an element owned by the live page.
    var parsed = new DOMParser().parseFromString(UTF8ToString(pointer), "text/html");
    var value = (parsed.body && parsed.body.textContent) || "";
    S.f.textContent = value;
    S.f.title = value;
    S.feedbackSet = true;
  },

  CounselCueWeb_LiveStart: function (configPointer) {
    var S = window.CounselCueWeb;
    if (!S || !S.liveStart) return;
    var cfg; try { cfg = JSON.parse(UTF8ToString(configPointer)); } catch (e) { return; }
    S.liveStart(cfg);
  },

  CounselCueWeb_LiveStop: function () {
    var S = window.CounselCueWeb;
    if (S && S.liveStop) S.liveStop();
  },

  CounselCueWeb_LiveMute: function (value) {
    var S = window.CounselCueWeb;
    if (S && S.liveMute) S.liveMute(!!value);
  },

  CounselCueWeb_LiveText: function (textPointer) {
    var S = window.CounselCueWeb;
    if (S && S.liveText) S.liveText(UTF8ToString(textPointer));
  },

  CounselCueWeb_LiveHint: function (textPointer) {
    var S = window.CounselCueWeb;
    if (S && S.liveHint) S.liveHint(UTF8ToString(textPointer));
  },

  CounselCueWeb_SetLanguage: function (isEnglish) {
    var S = window.CounselCueWeb;
    if (!S) return;
    S.lang = isEnglish ? "en" : "ko";
    S.applyLang();
  },

  // Saves the learner's export bundle for the instructor dashboard (Blob download; the
  // text is JSON produced by ResearchExportBundle, never interpreted as markup).
  CounselCueWeb_Download: function (namePointer, textPointer) {
    var name = UTF8ToString(namePointer).replace(/[^A-Za-z0-9._-]/g, "_") || "counselcue-export.json";
    var blob = new Blob([UTF8ToString(textPointer)], { type: "application/json" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 4000);
  },

  CounselCueWeb_SetCase: function (casePointer) {
    var S = window.CounselCueWeb;
    if (S) S.c = UTF8ToString(casePointer);
  },

  CounselCueWeb_Speak: function (textPointer, emotionPointer) {
    var S = window.CounselCueWeb;
    if (!S || !S.a) return;
    // Each request gets a token; a slower earlier request must not start playing
    // over (or report on behalf of) the reply that superseded it.
    var token = ++S.v;
    var current = function () { return token === S.v; };
    // Cancel a superseded request so it is neither billed nor counted against the voice limit.
    if (S.ac) S.ac.abort();
    var controller = window.AbortController ? new AbortController() : null;
    S.ac = controller;
    var notify = function (name) { if (current()) SendMessage(S.o, name, ""); };
    var clean = function () {
      if (S.auUrl) URL.revokeObjectURL(S.auUrl);
      S.auUrl = "";
      S.au = null;
    };
    if (S.au) {
      S.au.onended = null;
      S.au.onerror = null;
      S.au.pause();
      clean();
      notify("OnWebVoiceEnded");
    }
    var failed = false;
    var fail = function (error) {
      if (failed || !current()) return;
      failed = true;
      if (error) console.warn(error);
      clean();
      notify("OnWebVoiceFailed");
    };
    fetch(S.a + "/voice", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      signal: controller ? controller.signal : undefined,
      body: JSON.stringify({ text: UTF8ToString(textPointer), emotion: UTF8ToString(emotionPointer), caseId: S.c, clientId: S.cid })
    }).then(function (response) {
      if (!response.ok) throw Error("voice " + response.status);
      return response.blob();
    }).then(function (blob) {
      if (!current()) return;
      var url = URL.createObjectURL(blob);
      var audio = new Audio(url);
      S.au = audio;
      S.auUrl = url;
      audio.onplay = function () { notify("OnWebVoiceStarted"); };
      audio.onended = function () { clean(); notify("OnWebVoiceEnded"); };
      audio.onerror = function () { fail(Error("audio playback failed")); };
      audio.play().catch(fail);
    }).catch(fail);
  }
});
