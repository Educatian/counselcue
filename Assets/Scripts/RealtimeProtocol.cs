using System;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    public readonly struct RealtimeServerEvent
    {
        public RealtimeServerEvent(string type, string text, string error)
        {
            Type = type;
            Text = text;
            Error = error;
        }

        public string Type { get; }
        public string Text { get; }
        public string Error { get; }
    }

    public static class RealtimeProtocol
    {
        public static string CreateUserMessage(string text)
        {
            ConversationContent content = new ConversationContent
            {
                type = "input_text",
                text = text
            };
            ConversationItem item = new ConversationItem
            {
                type = "message",
                role = "user",
                content = new[] { content }
            };
            return JsonUtility.ToJson(new ConversationItemEvent
            {
                type = "conversation.item.create",
                item = item
            });
        }

        public static string CreateTextResponse()
        {
            return JsonUtility.ToJson(new ResponseCreateEvent
            {
                type = "response.create",
                response = new ResponseOptions { output_modalities = new[] { "text" } }
            });
        }

        public static string ReadClientSecret(string json)
        {
            ClientSecretResponse response = JsonUtility.FromJson<ClientSecretResponse>(json);
            return response == null ? string.Empty : response.value;
        }

        public static RealtimeServerEvent ParseServerEvent(string json)
        {
            EventEnvelope envelope = JsonUtility.FromJson<EventEnvelope>(json);
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.type))
            {
                return new RealtimeServerEvent(string.Empty, string.Empty, "잘못된 Realtime 이벤트");
            }

            if (envelope.type == "response.output_text.delta")
            {
                TextDeltaEvent delta = JsonUtility.FromJson<TextDeltaEvent>(json);
                return new RealtimeServerEvent(envelope.type, delta.delta, string.Empty);
            }

            if (envelope.type == "response.output_text.done")
            {
                TextDoneEvent done = JsonUtility.FromJson<TextDoneEvent>(json);
                return new RealtimeServerEvent(envelope.type, done.text, string.Empty);
            }

            if (envelope.type == "error")
            {
                ErrorEvent error = JsonUtility.FromJson<ErrorEvent>(json);
                string message = error.error == null ? "Realtime 요청 실패" : error.error.message;
                return new RealtimeServerEvent(envelope.type, string.Empty, message);
            }

            return new RealtimeServerEvent(envelope.type, string.Empty, string.Empty);
        }

        [Serializable]
        private sealed class ClientSecretResponse
        {
            public string value;
        }

        [Serializable]
        private sealed class EventEnvelope
        {
            public string type;
        }

        [Serializable]
        private sealed class TextDeltaEvent
        {
            public string delta;
        }

        [Serializable]
        private sealed class TextDoneEvent
        {
            public string text;
        }

        [Serializable]
        private sealed class ErrorEvent
        {
            public RealtimeError error;
        }

        [Serializable]
        private sealed class RealtimeError
        {
            public string message;
        }

        [Serializable]
        private sealed class ConversationItemEvent
        {
            public string type;
            public ConversationItem item;
        }

        [Serializable]
        private sealed class ConversationItem
        {
            public string type;
            public string role;
            public ConversationContent[] content;
        }

        [Serializable]
        private sealed class ConversationContent
        {
            public string type;
            public string text;
        }

        [Serializable]
        private sealed class ResponseCreateEvent
        {
            public string type;
            public ResponseOptions response;
        }

        [Serializable]
        private sealed class ResponseOptions
        {
            public string[] output_modalities;
        }
    }
}
