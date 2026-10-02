using System;
using AdieLab.AffectCounsel;

// Codes utterances with the pilot lexicon for eval/coding-agreement.mjs.
// stdin: "id<TAB>utterance" per line (tabs/newlines in utterances replaced by spaces).
// stdout: "id<TAB>code<TAB>quality".
public static class LexiconCoder
{
    public static int Main()
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        string line;
        while ((line = Console.ReadLine()) != null)
        {
            int tab = line.IndexOf('\t');
            if (tab < 0) continue;
            string id = line.Substring(0, tab);
            ResponseAssessment assessment = CounselingResponseEvaluator.Evaluate(line.Substring(tab + 1));
            Console.WriteLine(id + "\t" + CounselingCodebook.CodeOf(assessment) + "\t" + assessment.Quality);
        }
        return 0;
    }
}
