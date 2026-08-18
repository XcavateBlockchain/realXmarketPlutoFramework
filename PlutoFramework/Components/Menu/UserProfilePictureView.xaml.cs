namespace PlutoFramework.Components.Menu;

public partial class UserProfilePictureView : ContentView
{
    private static ImageSource DefaultImageSource => ImageSource.FromFile("xcavateprofilepicture.png");

    public static readonly BindableProperty ImageSourceProperty =
        BindableProperty.Create(nameof(ImageSource), typeof(ImageSource), typeof(UserProfilePictureView), default(ImageSource),
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                var control = (UserProfilePictureView)bindable;

                // Back to the placeholder rather than leaving what is on screen. Null is not
                // "nothing to do": it is a user with no picture, or a profile that has just
                // stopped being this user's - a main key change swaps chains - and holding the
                // last picture there shows them a face that is no longer theirs.
                control.image.Source = (ImageSource?)newValue ?? DefaultImageSource;
            });
    public UserProfilePictureView()
    {
        InitializeComponent();

        image.Source = DefaultImageSource;
    }

    public ImageSource ImageSource
    {
        get => (ImageSource)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }
}