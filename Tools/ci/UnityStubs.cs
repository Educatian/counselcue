// Minimal UnityEngine/UnityEditor stand-ins so the pure-logic scripts and their editor checks
// can be compiled and run with Mono in CI, without a Unity license. Not used by Unity itself.
using System;
namespace UnityEngine
{
    public class Object { public string name; }
    public class TextAsset : Object { public string text; }
    public static class Resources { public static T Load<T>(string path) where T : class => null; }
    public static class Mathf { public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v); }
    public static class JsonUtility { public static T FromJson<T>(string json) => default(T); }
    public static class Debug
    {
        public static void Log(object o) => Console.WriteLine(o);
        public static void LogWarning(object o) => Console.WriteLine("WARNING " + o);
        public static void LogError(object o) => Console.WriteLine("ERROR " + o);
    }
}
namespace UnityEditor
{
    public sealed class MenuItem : Attribute { public MenuItem(string path) { } }
    public static class EditorApplication { public static int ExitCode = -1; public static void Exit(int code) { ExitCode = code; } }
}
