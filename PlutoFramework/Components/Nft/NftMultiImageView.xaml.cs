using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Nft;

public partial class NftMultiImageView : ContentView
{
    private readonly ObservableCollection<string> thumbnailSources = [];

    private int currentMainIndex;

    public static readonly BindableProperty ImageSourcesProperty = BindableProperty.Create(
        nameof(ImageSources), typeof(List<string>), typeof(NftMultiImageView),
        default(List<string>),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) => {
            var control = (NftMultiImageView)bindable;
            control.UpdateImages(newValue as List<string>);
        });

    /// <summary>
    /// The full-resolution counterparts of <see cref="ImageSources"/>, in the same order.
    /// Nothing here displays them; they only reach the full-screen image page through the
    /// main image's expand button.
    /// </summary>
    public static readonly BindableProperty FullImageSourcesProperty = BindableProperty.Create(
        nameof(FullImageSources), typeof(List<string>), typeof(NftMultiImageView),
        default(List<string>),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) => {
            var control = (NftMultiImageView)bindable;
            control.SyncMainFullSource();
        });

    public NftMultiImageView()
	{
		InitializeComponent();

        thumbnailsCollectionView.ItemsSource = thumbnailSources;
    }

    public List<string> ImageSources
    {
        get => (List<string>)GetValue(ImageSourcesProperty);
        set => SetValue(ImageSourcesProperty, value);
    }

    public List<string> FullImageSources
    {
        get => (List<string>)GetValue(FullImageSourcesProperty);
        set => SetValue(FullImageSourcesProperty, value);
    }

    private void UpdateImages(List<string>? imageSources)
    {
        thumbnailSources.Clear();
        currentMainIndex = 0;

        if (imageSources is null || imageSources.Count == 0)
        {
            mainImage.ImageSource = "noimage.png";
            mainImage.FullImageSource = null;
            return;
        }

        foreach (string imageSource in imageSources)
        {
            if (string.IsNullOrWhiteSpace(imageSource))
            {
                continue;
            }

            thumbnailSources.Add(imageSource);
        }

        mainImage.ImageSource = thumbnailSources.Count > 0 ? thumbnailSources[0] : "noimage.png";

        SyncMainFullSource();
    }

    private void OnThumbnailSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is string selectedSource)
        {
            mainImage.ImageSource = selectedSource;
            currentMainIndex = thumbnailSources.IndexOf(selectedSource);

            SyncMainFullSource();
        }

        ((CollectionView)sender).SelectedItem = null;
    }

    /// <summary>
    /// Points the main image's expand button at the full-resolution original of the
    /// currently shown (possibly compressed) image. The mirror keeps thumbnails in the
    /// originals' order, so the mapping is positional - and only trustworthy while both
    /// lists have the same length (a partially mirrored asset would pair the wrong
    /// images, so there the expand just re-shows the displayed image).
    /// </summary>
    private void SyncMainFullSource()
    {
        var fullSources = FullImageSources;

        mainImage.FullImageSource = fullSources is not null
            && currentMainIndex >= 0
            && fullSources.Count == thumbnailSources.Count
            && currentMainIndex < fullSources.Count
            ? fullSources[currentMainIndex]
            : null;
    }
}
