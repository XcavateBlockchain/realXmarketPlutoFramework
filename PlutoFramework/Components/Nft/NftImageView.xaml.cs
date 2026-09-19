namespace PlutoFramework.Components.Nft;

public partial class NftImageView : ContentView
{
    public static readonly BindableProperty ImageSourceProperty = BindableProperty.Create(
        nameof(ImageSource), typeof(string), typeof(NftImageView),
        default(string),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) => {
            var control = (NftImageView)bindable;
            var imageSource = newValue as string;

            control.image.Source = string.IsNullOrWhiteSpace(imageSource) ? "noimage.png" : imageSource;
            control.downloadButton.Opacity = !string.IsNullOrWhiteSpace(imageSource) && imageSource.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0.5;
        });

    public static readonly BindableProperty FullImageSourceProperty = BindableProperty.Create(
        nameof(FullImageSource), typeof(string), typeof(NftImageView),
        default(string),
        defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty ExtraButtonsVisibleProperty = BindableProperty.Create(
        nameof(ExtraButtonsVisible), typeof(bool), typeof(NftImageView),
        true,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) => {
            var control = (NftImageView)bindable;

            control.extraButtonsBorder.IsVisible = (bool)newValue;
        });
    public NftImageView()
	{
		InitializeComponent();
	}

    public NftImageView(bool extraButtonsVisible)
    {
        InitializeComponent();

        extraButtonsBorder.IsVisible = extraButtonsVisible;
    }
    public string ImageSource
    {
        get => (string)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    /// <summary>
    /// The full-resolution counterpart of <see cref="ImageSource"/> (which may be a
    /// compressed mirror thumbnail). Only the full-screen image page loads it.
    /// </summary>
    public string FullImageSource
    {
        get => (string)GetValue(FullImageSourceProperty);
        set => SetValue(FullImageSourceProperty, value);
    }

    public bool ExtraButtonsVisible
    {
        get => (bool)GetValue(ExtraButtonsVisibleProperty);
        set => SetValue(ExtraButtonsVisibleProperty, value);
    }

    // Backwards-compatible alias property for naming preference.
    public bool ExtraButtonsIsVisible
    {
        get => ExtraButtonsVisible;
        set => ExtraButtonsVisible = value;
    }

    private void OnSquareSizeChanged(object sender, EventArgs e)
    {
        if (sender is not Border squareBorder || squareBorder.Width <= 0)
        {
            return;
        }

        // Guard against layout loops by only updating when the value actually changed.
        if (Math.Abs(squareBorder.HeightRequest - squareBorder.Width) > 0.5)
        {
            squareBorder.HeightRequest = squareBorder.Width;
        }
    }

    private async void OnDownloadClicked(object sender, TappedEventArgs e)
    {
        if (downloadButton.Opacity == 0.5)
        {
            return;
        }
        
        await Browser.Default.OpenAsync(new Uri(ImageSource), BrowserLaunchMode.SystemPreferred);
    }
    private async void OnExpandClicked(object sender, TappedEventArgs e)
    {
        // The full-screen page is the only surface that loads the full-resolution
        // original; everything else shows the compressed mirror thumbnail.
        var fullImageSource = string.IsNullOrWhiteSpace(FullImageSource) ? ImageSource : FullImageSource;

        await Shell.Current.Navigation.PushAsync(new NftImageFullScreenPage(fullImageSource));
    }
}
