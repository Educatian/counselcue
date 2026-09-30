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
        Console.WriteLine(failures == 0 ? "CI_CHECKS_PASS" : $"CI_CHECKS_FAIL {failures}");
        return failures == 0 ? 0 : 1;
    }
}
