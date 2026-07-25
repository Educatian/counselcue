mergeInto(LibraryManager.library, {
  CounselCueWeb_Initialize: function (objectPointer, apiPointer) {
    var S = window.CounselCueWeb = {
      o: UTF8ToString(objectPointer),
      a: UTF8ToString(apiPointer).replace(/\/$/, ""),
      on: false,
      i: 0
    };
    var canvas = document.querySelector("#unity-canvas");
    var css = document.createElement("style");
    css.textContent =
      "#cci{position:fixed;z-index:60;left:50%;bottom:max(12px,env(safe-area-inset-bottom));transform:translateX(-50%);width:min(calc(100vw - 32px),1040px);display:none;flex-direction:column;gap:7px;font-family:'Malgun Gothic','Noto Sans KR',sans-serif}" +
      "#ccf{box-sizing:border-box;width:100%;min-height:42px;max-height:54px;overflow:hidden;border-left:4px solid #a7d4bb;border-radius:11px;background:#0d1b18f2;color:#eef8f1;padding:9px 14px;font-size:16px;line-height:21px;box-shadow:0 6px 18px #0004}" +
      "#cccontrols{display:flex;gap:8px;width:100%;align-items:stretch}" +
      "#cci textarea{flex:1;min-width:0;height:62px;resize:none;box-sizing:border-box;border:2px solid #a7d4bb;border-radius:14px;background:#faf8f2;color:#23352e;padding:15px 16px;font:17px/1.45 'Malgun Gothic','Noto Sans KR',sans-serif;outline:none;box-shadow:0 8px 28px #0005}" +
      "#cci textarea:focus{border-color:#4d9a78;box-shadow:0 0 0 3px #9ed7bc66,0 8px 28px #0005}" +
      ".ccb{min-width:96px;height:62px;border:0;border-radius:14px;padding:0 18px;background:#347a5d;color:#fff;font-size:16px;font-weight:700;white-space:nowrap;cursor:pointer}" +
      ".ccb:focus-visible,.ctb:focus-visible,#cch:focus-visible{outline:3px solid #f3c778;outline-offset:3px}" +
      ".ccb:disabled{opacity:.55;cursor:not-allowed}.mic{background:#835b50}.mic.on{background:#b84d4d;box-shadow:0 0 0 7px #b84d4d44}" +
      "#ccn{flex:0 0 154px;align-self:center;box-sizing:border-box;color:#fff;background:#173b30ee;border-radius:14px;padding:8px 10px;font-size:11px;line-height:1.35;text-align:center}" +
      "#cch{position:fixed;z-index:61;right:max(14px,env(safe-area-inset-right));bottom:calc(132px + env(safe-area-inset-bottom));border:1px solid #fff8;border-radius:20px;background:#173b30ee;color:#fff;padding:8px 13px;font-weight:700;cursor:pointer}" +
      "#cct{position:fixed;inset:0;z-index:100;display:none;font-family:'Malgun Gothic','Noto Sans KR',sans-serif;pointer-events:none}" +
      "#ccs{position:fixed;border:3px solid #f3c778;border-radius:16px;box-shadow:0 0 0 2px #173b30aa,0 0 26px #f3c778;transition:left .2s,top .2s,width .2s,height .2s}" +
      "#ccp{position:fixed;width:370px;max-width:calc(100vw - 24px);box-sizing:border-box;background:#faf6ec;border-radius:18px;padding:20px 22px;box-shadow:0 16px 46px #0007;pointer-events:auto;color:#24352f}" +
      "#ccp h3{margin:0 0 8px;color:#245e47;font-size:20px}#ccp p{margin:0 0 16px;line-height:1.55;font-size:15px}" +
      ".ctb{border:0;border-radius:9px;padding:10px 14px;font-weight:700;cursor:pointer}.next{float:right;background:#347a5d;color:white}" +
      "@media(max-width:1100px){#ccn{display:none}#cci{width:min(calc(100vw - 24px),900px)}}" +
      "@media(max-width:700px) and (orientation:landscape){#cci{bottom:6px;gap:4px}#ccf{min-height:34px;max-height:40px;padding:6px 10px;font-size:13px;line-height:17px}#cci textarea,.ccb{height:48px}.ccb{min-width:72px;padding:0 10px;font-size:14px}#cch{bottom:96px;font-size:12px}.lbl{display:none}#ccp{width:330px;padding:16px 18px}#ccp h3{font-size:17px}#ccp p{font-size:13px;margin-bottom:10px}}" +
      "@media(max-height:520px) and (orientation:landscape){#cci{bottom:5px;gap:3px;width:calc(100vw - 12px)!important}#ccf{min-height:30px;max-height:34px;padding:5px 9px;font-size:12px;line-height:16px}#cci textarea,.ccb{height:44px}.ccb{min-width:72px;padding:0 10px;font-size:14px}#ccn{display:none}#cch{bottom:87px;font-size:12px;padding:6px 10px}.lbl{display:none}#ccp{width:320px;padding:14px 16px}#ccp h3{font-size:17px}#ccp p{font-size:13px;margin-bottom:10px}}" +
      "@media(prefers-reduced-motion:reduce){#ccs{transition:none}}";
    document.head.appendChild(css);

    var root = document.createElement("div");
    root.id = "cci";
    root.innerHTML = '<div id="ccf">상담자의 언어·비언어 전달을 함께 관찰합니다.</div><div id="cccontrols"><span id="ccn">AI 생성 내담자 음성 · 원음 미저장</span><textarea aria-label="상담자 응답" placeholder="응답을 입력하거나 마이크를 누르세요…"></textarea><button class="ccb mic" aria-label="음성 입력">● <span class="lbl">말하기</span></button><button class="ccb send">응답하기</button></div>';
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
      recognition.onstart = function () { mic.classList.add("on"); };
      recognition.onresult = function (event) {
        var text = "";
        for (var j = event.resultIndex; j < event.results.length; j++) text += event.results[j][0].transcript;
        S.x.value = text;
        changed();
      };
      recognition.onend = function () { mic.classList.remove("on"); S.x.focus(); };
      recognition.onerror = function () { mic.classList.remove("on"); };
      mic.onclick = function () { try { recognition.start(); } catch (error) { recognition.stop(); } };
    } else {
      mic.disabled = true;
      mic.title = "Chrome 또는 Edge에서 음성 입력을 사용할 수 있습니다.";
    }

    var tour = document.createElement("div");
    tour.id = "cct";
    tour.innerHTML = '<div id="ccs"></div><div id="ccp"><h3></h3><p></p><button class="ctb skip">건너뛰기</button><button class="ctb next">다음</button></div>';
    document.body.appendChild(tour);
    var help = document.createElement("button");
    help.id = "cch";
    help.textContent = "? 사용 안내";
    document.body.appendChild(help);
    var spotlight = tour.querySelector("#ccs");
    var card = tour.querySelector("#ccp");
    var next = tour.querySelector(".next");
    var canvasRect = function () { return canvas.getBoundingClientRect(); };
    var canvasArea = function (left, top, width, height) {
      var rect = canvasRect();
      return [rect.left + rect.width * left, rect.top + rect.height * top, rect.width * width, rect.height * height];
    };
    var elementRect = function (element) {
      var rect = element.getBoundingClientRect();
      return [rect.left, rect.top, rect.width, rect.height];
    };
    var steps = [
      ["내담자의 표정과 자세를 관찰하세요", "얼굴 근육, 시선, 움직임과 말의 내용을 함께 보세요.", function () { return canvasArea(.31, .12, .38, .56); }],
      ["관찰 줌을 활용하세요", "오른쪽 줌 컨트롤로 표정과 제스처를 가까이 확인하세요.", function () { return canvasArea(.78, .24, .205, .10); }],
      ["한글 입력을 지원합니다", "한글 조합, 붙여넣기, Shift+Enter 줄바꿈이 가능합니다.", function () { return elementRect(S.x); }],
      ["마이크로 응답하세요", "최초 1회 브라우저 마이크 권한 승인이 필요합니다.", function () { return elementRect(mic); }],
      ["감정 음성으로 답합니다", "LLM 페르소나 답변이 ElevenLabs 음성으로 재생됩니다.", function () { return elementRect(send); }]
    ];
    var clamp = function (value, min, max) { return Math.max(min, Math.min(max, value)); };
    var draw = function () {
      var step = steps[S.i];
      var target = step[2]();
      var pad = 7;
      var left = clamp(target[0] - pad, 6, innerWidth - 12);
      var top = clamp(target[1] - pad, 6, innerHeight - 12);
      var width = clamp(target[2] + pad * 2, 24, innerWidth - left - 6);
      var height = clamp(target[3] + pad * 2, 24, innerHeight - top - 6);
      Object.assign(spotlight.style, { left: left + "px", top: top + "px", width: width + "px", height: height + "px" });
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
      next.textContent = S.i === steps.length - 1 ? "시작하기" : "다음";
    };
    var closeTour = function () {
      tour.style.display = "none";
      localStorage.setItem("counselcue-tour-v3", "done");
    };
    next.onclick = function () { if (++S.i >= steps.length) closeTour(); else draw(); };
    tour.querySelector(".skip").onclick = closeTour;
    help.onclick = function () { S.i = 0; draw(); };
    addEventListener("resize", function () { S.place(); if (tour.style.display === "block") draw(); });
    S.show = function () { if (!localStorage.getItem("counselcue-tour-v3")) { S.i = 0; draw(); } };
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
    var decoder = document.createElement("div");
    decoder.innerHTML = UTF8ToString(pointer);
    var value = decoder.textContent || "";
    S.f.textContent = value;
    S.f.title = value;
  },

  CounselCueWeb_Speak: function (textPointer, emotionPointer) {
    var S = window.CounselCueWeb;
    if (!S || !S.a) return;
    var notify = function (name) { SendMessage(S.o, name, ""); };
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
      if (failed) return;
      failed = true;
      if (error) console.warn(error);
      clean();
      notify("OnWebVoiceFailed");
    };
    fetch(S.a + "/voice", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text: UTF8ToString(textPointer), emotion: UTF8ToString(emotionPointer) })
    }).then(function (response) {
      if (!response.ok) throw Error(response.status);
      return response.blob();
    }).then(function (blob) {
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
