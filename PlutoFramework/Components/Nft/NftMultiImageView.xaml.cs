using System.Collections.ObjectModel;

namespace PlutoFramework.Components.Nft;

public partial class NftMultiImageView : ContentView
{
    private readonly ObservableCollection<string> thumbnailSources = [];

    public static readonly BindableProperty ImageSourcesProperty = BindableProperty.Create(
        nameof(ImageSources), typeof(List<string>), typeof(NftMultiImageView),
        default(List<string>),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) => {
            var control = (NftMultiImageView)bindable;
            control.UpdateImages(newValue as List<string>);
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

    private void UpdateImages(List<string> imageSources)
    {
        thumbnailSources.Clear();

        if (imageSources is null || imageSources.Count == 0)
        {
            mainImage.ImageSource = "noimage.png";
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
    }

    private void OnThumbnailSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is string selectedSource)
        {
            mainImage.ImageSource = selectedSource;
        }

        ((CollectionView)sender).SelectedItem = null;
    }
}