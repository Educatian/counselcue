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
      var dictationBase = "";
      recognition.onstart = function () {
        mic.classList.add("on");
        dictationBase = S.x.value.replace(/\s+$/, "");
      };
      recognition.onresult = function (event) {
        // Rebuild from every result so typed text before dictation is kept and
        // finalized phrases are not dropped when interim results arrive.
        var text = "";
        for (var j = 0; j < event.results.length; j++) text += event.results[j][0].transcript;
        S.x.value = dictationBase ? dictationBase + " " + text.trim() : text.trim();
        changed();
      };
      recognition.onend = function () { mic.classList.remove("on"); S.x.focus(); };
      recognition.onerror = function () { mic.classList.remove("on"); };
      mic.onclick = function () { try { recognition.start(); } catch (error) { recognition.stop(); } };
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
    var skip = tour.querySelector(".skip");
    S.applyLang = function () {
      var t = S.t();
      document.documentElement.lang = S.lang;
      if (!S.feedbackSet) S.f.textContent = t.feedback;
      root.querySelector("#ccn").textContent = t.notice;
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

  CounselCueWeb_SetLanguage: function (isEnglish) {
    var S = window.CounselCueWeb;
    if (!S) return;
    S.lang = isEnglish ? "en" : "ko";
    S.applyLang();
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
