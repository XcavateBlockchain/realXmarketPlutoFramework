using System.Collections.ObjectModel;

namespace PlutoFramework.Components.XcavateProperty;

public partial class PropertyImagesView : ContentView
{
    private readonly ObservableCollection<string> imageSources = [];

    public static readonly BindableProperty ImageSourcesProperty = BindableProperty.Create(
        nameof(ImageSources), typeof(IReadOnlyList<string>), typeof(PropertyImagesView),
        default(IReadOnlyList<string>),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var control = (PropertyImagesView)bindable;
            control.UpdateImages(newValue as IReadOnlyList<string>);
        });

    public PropertyImagesView()
    {
        InitializeComponent();

        carouselView.ItemsSource = imageSources;
    }

    public IReadOnlyList<string> ImageSources
    {
        get => (IReadOnlyList<string>)GetValue(ImageSourcesProperty);
        set => SetValue(ImageSourcesProperty, value);
    }

    private void UpdateImages(IReadOnlyList<string>? sources)
    {
        imageSources.Clear();

        if (sources is not null)
        {
            foreach (string imageSource in sources)
            {
                if (string.IsNullOrWhiteSpace(imageSource))
                {
                    continue;
                }

                imageSources.Add(imageSource);
            }
        }

        if (imageSources.Count == 0)
        {
            imageSources.Add("noimage.png");
        }

        carouselView.Position = 0;
    }

    private void OnCarouselPositionChanged(object sender, PositionChangedEventArgs e)
    {

    }

    private void OnThumbnailSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is string selectedSource)
        {
            int selectedIndex = imageSources.IndexOf(selectedSource);

            if (selectedIndex >= 0)
            {
                carouselView.Position = selectedIndex;
            }
        }

        ((CollectionView)sender).SelectedItem = null;
    }
}