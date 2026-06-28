using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.Xcavate
{
    public class QuestionnaireV2FlowState
    {
        public QuestionnaireInfo Info { get; }

        public Dictionary<string, Dictionary<string, object?>> Responses { get; } = [];

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
    }
}
