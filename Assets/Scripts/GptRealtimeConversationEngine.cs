using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AdieLab.AffectCounsel
{
    public readonly struct RealtimeReply
    {
        private RealtimeReply(bool succeeded, string text, string error)
        {
            Succeeded = succeeded;
            Text = text;
            Error = error;
        }

        public bool Succeeded { get; }
        public string Text { get; }
        public string Error { get; }

        public static RealtimeReply Success(string text) => new RealtimeReply(true, text, string.Empty);
        public static RealtimeReply Failure(string error) => new RealtimeReply(false, string.Empty, error);
    }

    [DisallowMultipleComponent]
    public sealed class GptRealtimeConversationEngine : MonoBehaviour
    {
        [SerializeField] private string tokenBrokerUrl = "http://127.0.0.1:8787/token";
        [SerializeField] private string model = "gpt-realtime-2.1";
        [SerializeField, Range(5f, 45f)] private float requestTimeoutSeconds = 20f;
        [SerializeField] private bool enableInEditor;

        private ClientWebSocket socket;

        public bool IsRequested
        {
            get
            {
                if (enableInEditor && Application.isEditor) return true;
                return Array.Exists(Environment.GetCommandLineArgs(), argument => argument == "--realtime");
            }
        }

        public async Task<RealtimeReply> RequestReplyAsync(string counselorUtterance)
        {
            if (!IsRequested) return RealtimeReply.Failure("Realtime 비활성화");

            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(requestTimeoutSeconds));
            try
            {
                await EnsureConnectedAsync(timeout.Token);
                await SendAsync(RealtimeProtocol.CreateUserMessage(counselorUtterance), timeout.Token);
                await SendAsync(RealtimeProtocol.CreateTextResponse(), timeout.Token);
                return await ReceiveReplyAsync(timeout.Token);
            }
            catch (Exception exception) when (exception is WebSocketException || exception is OperationCanceledException || exception is InvalidOperationException)
            {
                ResetSocket();
                return RealtimeReply.Failure(exception.Message);
            }
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (socket != null && socket.State == WebSocketState.Open) return;

            string token = await RequestClientSecretAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("임시 Realtime 토큰이 없습니다.");

            ResetSocket();
            socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
            Uri endpoint = new Uri($"wss://api.openai.com/v1/realtime?model={Uri.EscapeDataString(model)}");
            await socket.ConnectAsync(endpoint, cancellationToken);
        }

        private async Task<string> RequestClientSecretAsync(CancellationToken cancellationToken)
        {
            using UnityWebRequest request = new UnityWebRequest(tokenBrokerUrl, UnityWebRequest.kHttpVerbPOST)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                uploadHandler = new UploadHandlerRaw(Array.Empty<byte>())
            };
            request.SetRequestHeader("Content-Type", "application/json");
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException($"토큰 브로커 연결 실패: {request.responseCode}");
            }

            return RealtimeProtocol.ReadClientSecret(request.downloadHandler.text);
        }

        private async Task SendAsync(string json, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task<RealtimeReply> ReceiveReplyAsync(CancellationToken cancellationToken)
        {
            StringBuilder reply = new StringBuilder();
            while (socket.State == WebSocketState.Open)
            {
                string json = await ReceiveMessageAsync(cancellationToken);
                RealtimeServerEvent serverEvent = RealtimeProtocol.ParseServerEvent(json);
                if (!string.IsNullOrEmpty(serverEvent.Error)) return RealtimeReply.Failure(serverEvent.Error);
                if (serverEvent.Type == "response.output_text.delta") reply.Append(serverEvent.Text);
                if (serverEvent.Type == "response.output_text.done")
                {
                    string completed = string.IsNullOrWhiteSpace(serverEvent.Text) ? reply.ToString() : serverEvent.Text;
                    return string.IsNullOrWhiteSpace(completed)
                        ? RealtimeReply.Failure("GPT 응답이 비어 있습니다.")
                        : RealtimeReply.Success(completed.Trim());
                }
            }

            return RealtimeReply.Failure("Realtime 연결이 종료되었습니다.");
        }

        private async Task<string> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[8192];
            using MemoryStream message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("Realtime 연결 종료");
                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(message.ToArray());
        }

        private void OnDestroy() => ResetSocket();

        private void ResetSocket()
        {
            socket?.Abort();
            socket?.Dispose();
            socket = null;
        }
    }
}
