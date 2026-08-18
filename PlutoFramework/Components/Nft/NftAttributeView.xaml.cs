namespace PlutoFramework.Components.Nft;

public partial class NftAttributeView : ContentView
{
    public static readonly BindableProperty AttributeNameProperty = BindableProperty.Create(
        nameof(AttributeName), typeof(string), typeof(NftAttributeView),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (NftAttributeView)bindable;

            control.attributeNameLabel.Text = (string)newValue;
        });

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(string), typeof(NftAttributeView),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (NftAttributeView)bindable;

            control.attributeValueLabel.Text = (string)newValue;
        });

    public static readonly BindableProperty CardBackgroundColorProperty = BindableProperty.Create(
        nameof(CardBackgroundColor), typeof(Color), typeof(NftAttributeView),
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (NftAttributeView)bindable;

            control.border.BackgroundColor = (Color)newValue;
        });

    public static readonly BindableProperty HasShadowProperty = BindableProperty.Create(
        nameof(HasShadow), typeof(bool), typeof(NftAttributeView), true,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (NftAttributeView)bindable;

            if (newValue is null)
            {
                return;
            }
            control.border.Shadow = (bool)newValue ? (Shadow)Application.Current.Resources["CardShadow"] : new()
            {
                Brush = Brush.Black,
                Offset = new Point(0, 0),
                Radius = 0,
                Opacity = 0,
            };
        });

    public NftAttributeView()
    {
        InitializeComponent();
    }

    public string AttributeName
    {
        get => (string)GetValue(AttributeNameProperty);
        set => SetValue(AttributeNameProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Color CardBackgroundColor
    {
        get => (Color)GetValue(CardBackgroundColorProperty);
        set => SetValue(CardBackgroundColorProperty, value);
    }

    public bool HasShadow
    {
        get => (bool)GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }
}