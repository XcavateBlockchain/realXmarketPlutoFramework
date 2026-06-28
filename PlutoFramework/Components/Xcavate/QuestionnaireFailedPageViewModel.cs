using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
namespace PlutoFramework.Components.Xcavate
{
    public record QuestionnaireFailedSection
    {
        public required string Title { get; init; }
        public required string Reason { get; init; }
    }

    public partial class QuestionnaireFailedPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string title = "You do not currently qualify.";

        [ObservableProperty]
        private string summary = "Based on your answers, none of the available investor categories passed.";

        public ObservableCollection<QuestionnaireFailedSection> FailedSections { get; } = [];

        public bool HasFailedSections => FailedSections.Count > 0;

        [RelayCommand]
        public Task CancelAsync()
        {
            return Shell.Current.Navigation.PopToRootAsync();
        }

        public void SetFailedSections(IEnumerable<QuestionnaireFailedSection> sections)
        {
            FailedSections.Clear();

            foreach (var section in sections)
            {
                FailedSections.Add(section);
            }

            OnPropertyChanged(nameof(HasFailedSections));
        }
    }
}
