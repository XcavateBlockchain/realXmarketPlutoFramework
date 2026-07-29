using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;
using PlutoFrameworkCore.Solana;

namespace PlutoFramework.Components.Solana.Transfer
{
    /// <summary>
    /// Visibility and selection for the token picker stacked over the transfer popup.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="SolanaTransferViewModel"/> because <c>BottomPopupCard</c>
    /// clears a dismissed popup by casting its parent's BindingContext to <see cref="IPopup"/>
    /// and setting <c>IsVisible</c>. That contract is one popup per view model: with both
    /// cards sharing one, swiping the picker away would set the transfer popup's flag and
    /// close the wrong thing.
    ///
    /// It owns only visibility. The balances and the selection stay on the transfer view
    /// model, so there is still one list and one selection rather than two to keep in step.
    /// </remarks>
    public partial class SolanaTokenSelectViewModel : ObservableObject, IPopup
    {
        [ObservableProperty]
        private bool isVisible;

        public SolanaTokenSelectViewModel()
        {
            // Both are app-wide singletons, so this subscription lives as long as both objects
            // and needs no counterpart.
            Transfer.PropertyChanged += OnTransferPropertyChanged;
        }

        private static SolanaTransferViewModel Transfer =>
            DependencyService.Get<SolanaTransferViewModel>();

        /// <summary>
        /// The transfer view model's own collection, not a copy — the picker and the popup
        /// must never disagree about what the wallet can send.
        /// </summary>
        public ObservableCollection<SolanaTransferBalance> Balances => Transfer.Balances;

        public string LoadError => Transfer.LoadError;

        public bool LoadErrorIsVisible => Transfer.LoadErrorIsVisible;

        [RelayCommand]
        private void SelectToken(SolanaTransferBalance? token)
        {
            Transfer.SelectTokenCommand.Execute(token);

            IsVisible = false;
        }

        /// <summary>
        /// A pass-through property raises no change of its own, so the poll's result would
        /// never reach the picker's error label without this.
        /// </summary>
        private void OnTransferPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SolanaTransferViewModel.LoadError)
                or nameof(SolanaTransferViewModel.LoadErrorIsVisible))
            {
                OnPropertyChanged(nameof(LoadError));
                OnPropertyChanged(nameof(LoadErrorIsVisible));
            }
        }
    }
}
