namespace PlutoFramework.Components.Menu;

public partial class UserProfilePictureView : ContentView
{
    public static readonly BindableProperty ImageSourceProperty =
        BindableProperty.Create(nameof(ImageSource), typeof(ImageSource), typeof(UserProfilePictureView), default(string),
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                var control = (UserProfilePictureView)bindable;

                if (newValue is null)
                {
                    return;
                }

                control.image.Source = (ImageSource)newValue;
            });
    public UserProfilePictureView()
    {
        InitializeComponent();

        image.Source = "xcavateprofilepicture.png";
    }

    public ImageSource ImageSource
    {
        get => (ImageSource)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }
}