using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace AdieLab.AffectCounsel.Automation
{
    /// <summary>
    /// Lets tools drive the editor in the background through files, without window focus.
    /// Drop a JSON command into Automation/inbox/ at the project root, e.g.
    ///   {"id":"42","command":"run-all-checks"}
    /// and read Automation/outbox/42.json. Automation/status.json is a heartbeat with the
    /// editor state and the latest compiler errors.
    ///
    /// Commands: ping, refresh, run-all-checks, build-room, capture-review, invoke (args = "Type.Method").
    ///
    /// This assembly has no references to project scripts (it uses reflection), so it keeps
    /// working and reports compiler errors even when the project fails to compile.
    /// </summary>
    [InitializeOnLoad]
    public static class CounselCueAutomationBridge
    {
        private const string Root = "Automation";
        private static readonly string Inbox = Path.Combine(Root, "inbox");
        private static readonly string Outbox = Path.Combine(Root, "outbox");
        private static readonly string StatusPath = Path.Combine(Root, "status.json");
        private const string PendingCaptureKey = "CounselCueAutomation.PendingCapture";

        private static double nextPoll;
        private static string lastCommand = "";
        private static readonly List<string> compilerErrors = new List<string>();

        static CounselCueAutomationBridge()
        {
            Directory.CreateDirectory(Inbox);
            Directory.CreateDirectory(Outbox);
            EditorApplication.update += Poll;
            CompilationPipeline.compilationStarted += _ => compilerErrors.Clear();
            CompilationPipeline.assemblyCompilationFinished += (assembly, messages) =>
            {
                foreach (CompilerMessage message in messages)
                {
                    if (message.type == CompilerMessageType.Error) compilerErrors.Add(message.message);
                }
            };
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            WriteStatus("loaded");
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.5;
            WriteStatus("idle");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;

            string next = Directory.Exists(Inbox)
                ? Directory.GetFiles(Inbox, "*.json").OrderBy(File.GetCreationTimeUtc).FirstOrDefault()
                : null;
            if (next == null) return;

            string text;
            try { text = File.ReadAllText(next); File.Delete(next); }
            catch (Exception) { return; }

            Command command = JsonUtility.FromJson<Command>(text) ?? new Command();
            if (string.IsNullOrEmpty(command.id)) command.id = Path.GetFileNameWithoutExtension(next);
            Execute(command);
        }

        private static void Execute(Command command)
        {
            lastCommand = command.command;
            Result result = new Result { id = command.id, command = command.command, startedUtc = DateTime.UtcNow.ToString("O") };
            StringBuilder log = new StringBuilder();
            Application.LogCallback capture = (message, stack, type) =>
            {
                log.Append('[').Append(type).Append("] ").AppendLine(message);
                if (type == LogType.Exception || type == LogType.Error) log.AppendLine(Truncate(stack, 1200));
            };
            Application.logMessageReceived += capture;
            try
            {
                switch (command.command)
                {
                    case "ping":
                        break;
                    case "refresh":
                        AssetDatabase.Refresh();
                        break;
                    case "run-all-checks":
                        InvokeStatic("AdieLab.AffectCounsel.Editor.CounselCueReviewTools", "RunAllChecks");
                        break;
                    case "build-room":
                        InvokeStatic("AdieLab.AffectCounsel.Editor.CounselingRoomBuilder", "Build");
                        break;
                    case "capture-review":
                        PlayerSettings.runInBackground = true;
                        // Avoid the "save modified scenes?" modal, which would block a background run.
                        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                        SessionState.SetString(PendingCaptureKey, command.id);
                        InvokeStatic("AdieLab.AffectCounsel.Editor.CounselCueReviewTools", "CaptureReviewScreenshots");
                        result.note = "Play mode started; the result is written when play mode ends.";
                        break;
                    case "invoke":
                        int split = command.args?.LastIndexOf('.') ?? -1;
                        if (split <= 0) throw new ArgumentException("invoke needs args \"Namespace.Type.Method\"");
                        InvokeStatic(command.args.Substring(0, split), command.args.Substring(split + 1));
                        break;
                    default:
                        throw new ArgumentException($"Unknown command '{command.command}'.");
                }
                result.success = true;
            }
            catch (Exception exception)
            {
                Exception inner = exception is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : exception;
                result.success = false;
                log.AppendLine("[Exception] " + inner.GetType().Name + ": " + inner.Message);
                log.AppendLine(Truncate(inner.StackTrace, 2000));
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            result.finishedUtc = DateTime.UtcNow.ToString("O");
            result.log = log.ToString();
            if (command.command != "capture-review" || !result.success) WriteResult(result);
            else WriteResult(result, command.id + ".started");
            WriteStatus("ran " + command.command);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            string id = SessionState.GetString(PendingCaptureKey, "");
            if (string.IsNullOrEmpty(id)) return;
            SessionState.EraseString(PendingCaptureKey);
            string folder = Path.GetFullPath("Screenshots/review");
            string[] files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png").Select(Path.GetFileName).OrderBy(f => f).ToArray() : Array.Empty<string>();
            WriteResult(new Result
            {
                id = id,
                command = "capture-review",
                success = files.Length > 0,
                finishedUtc = DateTime.UtcNow.ToString("O"),
                note = "Captured " + files.Length + " file(s) in " + folder,
                log = string.Join("\n", files)
            });
        }

        private static void InvokeStatic(string typeName, string methodName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(found => found != null);
            if (type == null) throw new InvalidOperationException($"Type {typeName} not found (does the project compile?).");
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method == null) throw new InvalidOperationException($"Static method {typeName}.{methodName}() not found.");
            method.Invoke(null, null);
        }

        private static void WriteStatus(string state)
        {
            try
            {
                Status status = new Status
                {
                    state = state,
                    timeUtc = DateTime.UtcNow.ToString("O"),
                    unityVersion = Application.unityVersion,
                    projectPath = Path.GetFullPath("."),
                    isCompiling = EditorApplication.isCompiling,
                    isPlaying = EditorApplication.isPlaying,
                    lastCommand = lastCommand,
                    compilerErrors = compilerErrors.Take(40).ToArray()
                };
                File.WriteAllText(StatusPath, JsonUtility.ToJson(status, true));
            }
            catch (Exception) { }
        }

        private static void WriteResult(Result result, string fileName = null)
        {
            try { File.WriteAllText(Path.Combine(Outbox, (fileName ?? result.id) + ".json"), JsonUtility.ToJson(result, true)); }
            catch (Exception exception) { Debug.LogWarning("Automation result not written: " + exception.Message); }
        }

        private static string Truncate(string value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? value ?? "" : value.Substring(0, max) + "…";

        [Serializable] private sealed class Command { public string id; public string command; public string args; }
        [Serializable] private sealed class Result { public string id; public string command; public bool success; public string startedUtc; public string finishedUtc; public string note; public string log; }
        [Serializable] private sealed class Status { public string state; public string timeUtc; public string unityVersion; public string projectPath; public bool isCompiling; public bool isPlaying; public string lastCommand; public string[] compilerErrors; }
    }
}
