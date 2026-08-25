namespace PlutoFramework.Components.Loading;

/// <summary>
/// A stack of skeleton placeholder cards shown while a list is loading. Renders
/// <see cref="DefaultCount" /> <see cref="PropertyCardSkeletonView"/> cards by default; the
/// count, the per-card margin and the card template itself are all configurable, so the
/// view can stand in for other card designs:
/// <code>
///     &lt;loading:SkeletonCardView Count="3" ItemMargin="20, 5, 20, 10" IsVisible="{Binding Loading}" /&gt;
/// </code>
/// </summary>
public partial class SkeletonCardView : ContentView
{
    /// <summary>Default number of skeleton cards displayed while loading.</summary>
    public const int DefaultCount = 2;

    /// <summary>
    /// Default per-card margin, matching the item padding used by the marketplace list
    /// (20 left/right, 5 top, 10 bottom).
    /// </summary>
    public static readonly Thickness DefaultItemMargin = new Thickness(20, 5, 20, 10);

    public static readonly BindableProperty CountProperty = BindableProperty.Create(
        nameof(Count), typeof(int), typeof(SkeletonCardView), DefaultCount,
        propertyChanged: (bindable, _, _) => ((SkeletonCardView)bindable).RebuildCards());

    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(SkeletonCardView),
        defaultValue: null,
        propertyChanged: (bindable, _, _) => ((SkeletonCardView)bindable).RebuildCards());

    public static readonly BindableProperty ItemMarginProperty = BindableProperty.Create(
        nameof(ItemMargin), typeof(Thickness), typeof(SkeletonCardView), DefaultItemMargin,
        propertyChanged: (bindable, _, _) => ((SkeletonCardView)bindable).RebuildCards());

    /// <summary>How many skeleton cards to show. Defaults to <see cref="DefaultCount"/>.</summary>
    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    /// <summary>
    /// Optional template for the skeleton cards. When set, its content is used in place
    /// of the default <see cref="PropertyCardSkeletonView"/> shape.
    /// </summary>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>Margin applied to each card. Defaults to <see cref="DefaultItemMargin"/>.</summary>
    public Thickness ItemMargin
    {
        get => (Thickness)GetValue(ItemMarginProperty);
        set => SetValue(ItemMarginProperty, value);
    }

    public SkeletonCardView()
    {
        InitializeComponent();

        PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == IsVisibleProperty.PropertyName)
            {
                // The children stop their shimmer while the stack is hidden and resume
                // when it comes back.
                foreach (View child in skeletonStack.Children)
                {
                    child.IsVisible = IsVisible;
                }
            }
        };

        RebuildCards();
    }

    private void RebuildCards()
    {
        skeletonStack.Children.Clear();

        for (var i = 0; i < Math.Max(0, Count); i++)
        {
            var card = ItemTemplate is not null
                ? ItemTemplate.CreateContent() as View ?? new PropertyCardSkeletonView()
                : new PropertyCardSkeletonView();

            card.Margin = ItemMargin;
            card.IsVisible = IsVisible;

            skeletonStack.Children.Add(card);
        }
    }
}
