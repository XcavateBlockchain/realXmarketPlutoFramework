using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

using Tab = PlutoFramework.Components.Tabs.Tab;

namespace PlutoFramework.Components.Notifications
{
    /// <summary>
    /// Shows the notifications recorded in <see cref="NotificationsModel"/>. The list
    /// refreshes while the page is visible - a push can land mid-view - but only
    /// between appearances, so the model's change event must be attached and detached
    /// by the page's lifecycle.
    /// </summary>
    public partial class NotificationsPageViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedFilter))]
        [NotifyPropertyChangedFor(nameof(Notifications))]
        private ObservableCollection<Tab> tabs = new ObservableCollection<Tab>(
            new List<NotificationType>([NotificationType.All, NotificationType.Announcement, NotificationType.System])
                .Select(filter => new Tab
                {
                    Title = filter switch
                    {
                        NotificationType.All => "All",
                        NotificationType.Announcement => "Announcements",
                        NotificationType.System => "System",
                        _ => "Unknown"
                    },
                    IsSelected = filter == NotificationType.All,
                    Value = filter
                })
            );

        public NotificationType SelectedFilter => (NotificationType)Tabs.FirstOrDefault(
            tab => tab.IsSelected,
            new Tab { IsSelected = true, Title = "All", Value = NotificationType.All }
        ).Value;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Notifications))]
        [NotifyPropertyChangedFor(nameof(HasNoNotifications))]
        private List<Notification> allNotifications = [.. NotificationsModel.GetAll()];

        /// <summary>Drives the empty state, so a fresh install is not a blank screen.</summary>
        public bool HasNoNotifications => AllNotifications.Count == 0;

        public ObservableCollection<Notification> Notifications => new ObservableCollection<Notification>(
            AllNotifications.Where(n => n.Type == SelectedFilter || SelectedFilter == NotificationType.All)
        );

        public void OnAppearing()
        {
            NotificationsModel.Changed += OnStoreChanged;
            Refresh();
        }

        public void OnDisappearing()
        {
            NotificationsModel.Changed -= OnStoreChanged;
        }

        /// <summary>
        /// The store raises its event from Firebase callback threads, so the reload is
        /// marshalled before it touches bound state.
        /// </summary>
        private void OnStoreChanged() => MainThread.BeginInvokeOnMainThread(Refresh);

        private void Refresh() => AllNotifications = [.. NotificationsModel.GetAll()];

        [RelayCommand]
        public void SelectTab(object parameter)
        {
            var filter = parameter as NotificationType?;

            if (filter == SelectedFilter)
                return;


            Tabs = new ObservableCollection<Tab>(Tabs.Select(tab =>
            {
                return new Tab
                {
                    Title = tab.Title,
                    Value = tab.Value,
                    IsSelected = (NotificationType)tab.Value == filter
                };
            }));
        }
    }
}
