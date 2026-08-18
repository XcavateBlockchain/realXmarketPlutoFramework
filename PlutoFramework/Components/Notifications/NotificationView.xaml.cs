namespace PlutoFramework.Components.Notifications;

public partial class NotificationView : ContentView
{
	public NotificationView()
	{
		InitializeComponent();
	}

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        // A push has no detail page behind it - title and message are already on
        // screen - so tapping means "read", clearing the unread dot. (This used to
        // navigate to the messaging overview, a leftover from the design template.)
        if (BindingContext is Notification notification)
        {
            NotificationsModel.MarkRead(notification.Id);
        }
    }
}
