using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Xcavate
{
    public class QuestionnaireV2FlowState
    {
        public const string HighNetWorthSectionId = "high_net_worth_investor";
        public const string SophisticatedInvestorSectionId = "sophisticated-investor";

        public QuestionnaireInfo Info { get; }

        public Dictionary<string, Dictionary<string, object?>> Responses { get; } = [];

        public string? SubmissionId { get; set; }

        public QuestionnaireV2FlowState(QuestionnaireInfo info)
        {
            Info = info;

            foreach (var section in info.Sections)
            {
                Responses[section.Id] = [];
            }
        }

        public QuestionnaireSection GetSection(int sectionIndex)
        {
            return Info.Sections[sectionIndex];
        }

        public QuestionnaireSection? GetSectionById(string sectionId)
        {
            return Info.Sections.FirstOrDefault(section => string.Equals(section.Id, sectionId, StringComparison.Ordinal));
        }
    }
}
