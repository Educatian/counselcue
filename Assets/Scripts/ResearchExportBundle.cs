using System.Text;

namespace AdieLab.AffectCounsel
{
    /// <summary>Version of the local JSONL research records. See Docs/DATA_SCHEMA.md.</summary>
    public static class ResearchRecord
    {
        public const int SchemaVersion = 3;
    }

    /// <summary>
    /// Builds the learner export file read by the instructor dashboard. Pure string work so it
    /// runs in CI; the record arrays are the JSONL lines exactly as written, joined by commas.
    /// </summary>
    public static class ResearchExportBundle
    {
        public const string Format = "counselcue-export";
        public const int Version = 1;

        public static string Compose(string sessionsJoined, string summariesJoined, string assessmentsJoined,
            string exportedUtc, string appVersion, string learnerId, string learnerCode)
        {
            return "{" +
                   $"\"format\":\"{Format}\",\"version\":{Version}," +
                   $"\"exportedUtc\":{Quote(exportedUtc)}," +
                   $"\"appVersion\":{Quote(appVersion)}," +
                   $"\"schemaVersion\":{ResearchRecord.SchemaVersion}," +
                   $"\"codebookVersion\":{Quote(CounselingCodebook.Version)}," +
                   $"\"learnerId\":{Quote(learnerId)}," +
                   $"\"learnerCode\":{Quote(learnerCode)}," +
                   $"\"sessions\":[{sessionsJoined}],\"summaries\":[{summariesJoined}],\"assessments\":[{assessmentsJoined}]" +
                   "}";
        }

        public static string Quote(string value)
        {
            StringBuilder builder = new StringBuilder("\"");
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ') builder.Append("\\u").Append(((int)c).ToString("x4"));
                        else builder.Append(c);
                        break;
                }
            }
            return builder.Append('"').ToString();
        }
    }
}
