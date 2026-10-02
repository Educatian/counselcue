using System;
public static class CheckRunner
{
    public static int Main()
    {
        int failures = 0;
        try { AdieLab.AffectCounsel.Editor.RelationalDeliveryModelChecks.RunFromCommandLine(); }
        catch (Exception e) { Console.WriteLine("RELATIONAL_DELIVERY_CHECKS_FAILED " + e.Message); failures++; }
        AdieLab.AffectCounsel.Editor.CounselingResponseEvaluatorChecks.RunFromCommandLine();
        if (UnityEditor.EditorApplication.ExitCode != 0) { Console.WriteLine("RESPONSE_EVALUATOR_CHECKS_FAILED"); failures++; }
        else Console.WriteLine("RESPONSE_EVALUATOR_CHECKS_PASS");
        // Response pattern profile: counts, underused core skills and repeated runs.
        var profile = AdieLab.AffectCounsel.ResponsePatternProfile.Build(
            new[] { "closed_question", "closed_question", "closed_question", "advice", "reflection" }, new[] { 1, 1, 0, 0, 2 });
        string pattern = profile.ToKoreanSummary();
        if (profile.LongestStreak != 3 || profile.LongestStreakCode != "closed_question" || profile.Underused.Count != 3 ||
            !pattern.Contains("닫힌 질문 3") || !pattern.Contains("덜 쓴 기술") || !pattern.Contains("3회 연속"))
        { Console.WriteLine("RESPONSE_PATTERN_CHECK_FAIL " + pattern.Replace("\n", " | ")); failures++; }
        else Console.WriteLine("RESPONSE_PATTERN_CHECK_PASS");
        // Export bundle: tricky learner codes must still yield valid JSON (validated by node in run-csharp-checks.sh).
        string bundle = AdieLab.AffectCounsel.ResearchExportBundle.Compose(
            "{\"turn\":1,\"counselorUtterance\":\"\uc548\ub155\"}", "", "{\"sourceTurn\":1}",
            "2026-09-30T00:00:00Z", "0.9", "L-ABC123", "학번 \"12\"\\반\n\t2");
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "counselcue-bundle-check.json"), bundle);
        Console.WriteLine(failures == 0 ? "CI_CHECKS_PASS" : $"CI_CHECKS_FAIL {failures}");
        return failures == 0 ? 0 : 1;
    }
}
