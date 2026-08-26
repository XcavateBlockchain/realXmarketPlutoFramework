using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PlutoFramework.Model;

namespace PlutoFramework.Components.MessagePopup
{
    public partial class MessagePopupViewModel : ObservableObject, IPopup
    {
        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string text;

        [ObservableProperty]
        private bool isVisible;

        public MessagePopupViewModel()
        {
            isVisible = false;
        }
    }
}

