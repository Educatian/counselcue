using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class FacialActionUnitMonitor : MonoBehaviour
    {
        [SerializeField] private Text statusLabel;
        [SerializeField, Range(1024, 65535)] private int port = 18765;
        [SerializeField, Min(0.25f)] private float staleAfterSeconds = 1.5f;

        private readonly object receiveGate = new object();
        private UdpClient receiver;
        private Thread receiveThread;
        private string pendingJson;
        private bool receiveLoopActive;
        private float lastFrameTime;

        public bool IsRequested { get; private set; }
        public bool IsTracking { get; private set; }
        public bool IsCalibrated { get; private set; }
        public float CalibrationProgress { get; private set; }
        public string Source { get; private set; } = string.Empty;
        public float Au01 { get; private set; }
        public float Au02 { get; private set; }
        public float Au04 { get; private set; }
        public float Au06 { get; private set; }
        public float Au07 { get; private set; }
        public float Au12 { get; private set; }
        public float Au14 { get; private set; }
        public float Au15 { get; private set; }
        public float Au17 { get; private set; }
        public float Au23 { get; private set; }
        public float Au25 { get; private set; }
        public float Au26 { get; private set; }
        public float Au45 { get; private set; }

        public static bool IsRequestedFromCommandLine()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            return Array.Exists(arguments, argument => argument == "--au");
        }

        private void Awake()
        {
            IsRequested = IsRequestedFromCommandLine();
        }

        private void Start()
        {
            if (!IsRequested)
            {
                SetStatus("AU 분석 대기 · 선택 기능");
                return;
            }

            SetStatus("AU 브리지 연결 중 · 로컬 처리");
            receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            receiver.Client.ReceiveTimeout = 500;
            receiveLoopActive = true;
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "AffectCounsel-AU-Receiver"
            };
            receiveThread.Start();
        }

        private void Update()
        {
            string json = null;
            lock (receiveGate)
            {
                if (pendingJson != null)
                {
                    json = pendingJson;
                    pendingJson = null;
                }
            }

            if (json != null)
            {
                Apply(JsonUtility.FromJson<AuFrame>(json));
            }

            if (IsTracking && Time.unscaledTime - lastFrameTime > staleAfterSeconds)
            {
                IsTracking = false;
                SetStatus("AU 신호 대기 · 브리지를 확인하세요");
            }
        }

        private void OnDestroy()
        {
            receiveLoopActive = false;
            receiver?.Close();
            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(500);
            }
        }

        private void ReceiveLoop()
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, 0);
            while (receiveLoopActive)
            {
                try
                {
                    byte[] bytes = receiver.Receive(ref endpoint);
                    string json = Encoding.UTF8.GetString(bytes);
                    lock (receiveGate)
                    {
                        pendingJson = json;
                    }
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut)
                {
                }
                catch (SocketException) when (!receiveLoopActive)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void Apply(AuFrame frame)
        {
            if (frame == null) return;
            Source = frame.source ?? string.Empty;
            IsTracking = frame.tracking;
            IsCalibrated = frame.calibrated;
            CalibrationProgress = frame.calibration_progress;
            Au01 = frame.au01;
            Au02 = frame.au02;
            Au04 = frame.au04;
            Au06 = frame.au06;
            Au07 = frame.au07;
            Au12 = frame.au12;
            Au14 = frame.au14;
            Au15 = frame.au15;
            Au17 = frame.au17;
            Au23 = frame.au23;
            Au25 = frame.au25;
            Au26 = frame.au26;
            Au45 = frame.au45;
            lastFrameTime = Time.unscaledTime;
            string sourceLabel = Source == "mediapipe-blendshape-proxy" ? "MediaPipe AU proxy" : "AU proxy";
            if (!IsTracking)
            {
                SetStatus("얼굴을 찾는 중 · 정면을 봐주세요");
            }
            else if (frame.calibrating)
            {
                SetStatus($"중립 보정 {Mathf.RoundToInt(CalibrationProgress * 100f)}% · 표정을 편안하게");
            }
            else
            {
                SetStatus($"{sourceLabel} · 04 {Mathf.RoundToInt(Au04 * 100f)}  12 {Mathf.RoundToInt(Au12 * 100f)}  45 {Mathf.RoundToInt(Au45 * 100f)}");
            }
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null)
            {
                statusLabel.text = value;
            }
        }

        [Serializable]
        private sealed class AuFrame
        {
            public string source;
            public bool tracking;
            public bool calibrating;
            public bool calibrated;
            public float calibration_progress;
            public float au01;
            public float au02;
            public float au04;
            public float au06;
            public float au07;
            public float au12;
            public float au14;
            public float au15;
            public float au17;
            public float au23;
            public float au25;
            public float au26;
            public float au45;
        }
    }
}
