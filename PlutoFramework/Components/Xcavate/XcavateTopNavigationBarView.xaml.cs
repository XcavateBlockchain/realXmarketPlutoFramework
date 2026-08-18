using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Model;

namespace PlutoFramework.Components.Xcavate;

public partial class XcavateTopNavigationBarView : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), 
        typeof(XcavateTopNavigationBarView),
        defaultValue: "",
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) => {
            var control = (XcavateTopNavigationBarView)bindable;

            if (newValue == null)
            {
                return;
            }

            control.titleText.Text = (string)newValue;
        });

    public static readonly BindableProperty Extra1CommandProperty = BindableProperty.Create(
        nameof(Extra1Command), typeof(IAsyncRelayCommand), typeof(XcavateTopNavigationBarView),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) => {
            var control = (XcavateTopNavigationBarView)bindable;

            control.extra1GestureRecognizer.Command = (IAsyncRelayCommand)newValue;
        });

    public static readonly BindableProperty Extra1ImageProperty = BindableProperty.Create(
       nameof(Extra1Image), typeof(ImageSource), typeof(XcavateTopNavigationBarView),
       defaultBindingMode: BindingMode.TwoWay,
       propertyChanging: (bindable, oldValue, newValue) =>
       {
           var control = (XcavateTopNavigationBarView)bindable;
           control.extra1Image.Source = (ImageSource)newValue;
           control.extra1Image.IsVisible = newValue != null;
       });

    public static readonly BindableProperty Extra1IsVisibleProperty = BindableProperty.Create(
        nameof(Extra1IsVisible), typeof(bool), typeof(XcavateTopNavigationBarView),
        defaultValue: true,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra1Border.IsVisible = (bool)newValue;
        }
    );

    public static readonly BindableProperty Extra1ImageMarginProperty = BindableProperty.Create(
        nameof(Extra1ImageMargin), typeof(Thickness), typeof(XcavateTopNavigationBarView),
        defaultValue: new Thickness(0, 0, 0, 0),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra1Image.Margin = (Thickness)newValue;
        }
    );

    public static readonly BindableProperty Extra1ImageHeightProperty = BindableProperty.Create(
        nameof(Extra1ImageHeight), typeof(double), typeof(XcavateTopNavigationBarView),
        defaultValue: 25.0,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra1Image.HeightRequest = (double)newValue;
        }
    );

    public static readonly BindableProperty Extra1ImageWidthProperty = BindableProperty.Create(
        nameof(Extra1ImageWidth), typeof(double), typeof(XcavateTopNavigationBarView),
        defaultValue: 25.0,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra1Image.WidthRequest = (double)newValue;
        }
    );

    public static readonly BindableProperty Extra2CommandProperty = BindableProperty.Create(
       nameof(Extra2Command), typeof(IAsyncRelayCommand), typeof(XcavateTopNavigationBarView),
       defaultBindingMode: BindingMode.TwoWay,
       propertyChanging: (bindable, oldValue, newValue) => {
           var control = (XcavateTopNavigationBarView)bindable;

           control.extra2GestureRecognizer.Command = (IAsyncRelayCommand)newValue;
       });

    public static readonly BindableProperty Extra2ImageHeightProperty = BindableProperty.Create(
        nameof(Extra2ImageHeight), typeof(double), typeof(XcavateTopNavigationBarView),
        defaultValue: 25.0,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra2Image.HeightRequest = (double)newValue;
        }
    );

    public static readonly BindableProperty Extra2ImageWidthProperty = BindableProperty.Create(
        nameof(Extra2ImageWidth), typeof(double), typeof(XcavateTopNavigationBarView),
        defaultValue: 25.0,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra2Image.WidthRequest = (double)newValue;
        }
    );

    public static readonly BindableProperty Extra2ImageProperty = BindableProperty.Create(
       nameof(Extra2Image), typeof(ImageSource), typeof(XcavateTopNavigationBarView),
       defaultBindingMode: BindingMode.TwoWay,
       propertyChanging: (bindable, oldValue, newValue) =>
       {
           var control = (XcavateTopNavigationBarView)bindable;
           control.extra2Image.Source = (ImageSource)newValue;
           control.extra2Image.IsVisible = newValue != null;
       });

    public static readonly BindableProperty Extra2ImageMarginProperty = BindableProperty.Create(
        nameof(Extra2ImageMargin), typeof(Thickness), typeof(XcavateTopNavigationBarView),
        defaultValue: new Thickness(0, 0, 0, 0),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra2Image.Margin = (Thickness)newValue;
        }
    );

    public static readonly BindableProperty Extra2IsVisibleProperty = BindableProperty.Create(
        nameof(Extra2IsVisible), typeof(bool), typeof(XcavateTopNavigationBarView),
        defaultValue: true,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra2Border.IsVisible = (bool)newValue;
        }
    );

    public static readonly BindableProperty Extra3CommandProperty = BindableProperty.Create(
       nameof(Extra3Command), typeof(IAsyncRelayCommand), typeof(XcavateTopNavigationBarView),
       defaultBindingMode: BindingMode.TwoWay,
       propertyChanging: (bindable, oldValue, newValue) => {
           var control = (XcavateTopNavigationBarView)bindable;

           control.extra3GestureRecognizer.Command = (IAsyncRelayCommand)newValue;
       });

    public static readonly BindableProperty Extra3ImageProperty = BindableProperty.Create(
       nameof(Extra3Image), typeof(ImageSource), typeof(XcavateTopNavigationBarView),
       defaultBindingMode: BindingMode.TwoWay,
       propertyChanging: (bindable, oldValue, newValue) =>
       {
           var control = (XcavateTopNavigationBarView)bindable;
           control.extra3Image.Source = (ImageSource)newValue;
           control.extra3Image.IsVisible = newValue != null;
       });

    public static readonly BindableProperty Extra3ImageMarginProperty = BindableProperty.Create(
        nameof(Extra3ImageMargin), typeof(Thickness), typeof(XcavateTopNavigationBarView),
        defaultValue: new Thickness(0, 0, 0, 0),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra3Image.Margin = (Thickness)newValue;
        }
    );

    public static readonly BindableProperty Extra3ImageHeightProperty = BindableProperty.Create(
        nameof(Extra3ImageHeight), typeof(double), typeof(XcavateTopNavigationBarView),
        defaultValue: 25.0,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra3Image.HeightRequest = (double)newValue;
        }
    );

    public static readonly BindableProperty Extra3ImageWidthProperty = BindableProperty.Create(
        nameof(Extra3ImageWidth), typeof(double), typeof(XcavateTopNavigationBarView),
        defaultValue: 25.0,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra3Image.WidthRequest = (double)newValue;
        }
    );

    public static readonly BindableProperty Extra3IsVisibleProperty = BindableProperty.Create(
        nameof(Extra3IsVisible), typeof(bool), typeof(XcavateTopNavigationBarView),
        defaultValue: true,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanging: (bindable, oldValue, newValue) =>
        {
            var control = (XcavateTopNavigationBarView)bindable;
            control.extra3Border.IsVisible = (bool)newValue;
        }
    );
    public XcavateTopNavigationBarView()
	{
		InitializeComponent();
	}

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    public IAsyncRelayCommand Extra1Command
    {
        get => (IAsyncRelayCommand)GetValue(Extra1CommandProperty);
        set => SetValue(Extra1CommandProperty, value);
    }

    public ImageSource Extra1Image
    {
        get => (ImageSource)GetValue(Extra1ImageProperty);
        set => SetValue(Extra1ImageProperty, value);
    }
    public Thickness Extra1ImageMargin
    {
        get => (Thickness)GetValue(Extra1ImageMarginProperty);
        set => SetValue(Extra1ImageMarginProperty, value);
    }

    public double Extra1ImageHeight
    {
        get => (double)GetValue(Extra1ImageHeightProperty);
        set => SetValue(Extra1ImageHeightProperty, value);
    }
    public double Extra1ImageWidth
    {
        get => (double)GetValue(Extra1ImageWidthProperty);
        set => SetValue(Extra1ImageWidthProperty, value);
    }
    public bool Extra1IsVisible
    {
        get => (bool)GetValue(Extra1IsVisibleProperty);
        set => SetValue(Extra1IsVisibleProperty, value);
    }
    public IAsyncRelayCommand Extra2Command
    {
        get => (IAsyncRelayCommand)GetValue(Extra2CommandProperty);
        set => SetValue(Extra2CommandProperty, value);
    }

    public ImageSource Extra2Image
    {
        get => (ImageSource)GetValue(Extra2ImageProperty);
        set => SetValue(Extra2ImageProperty, value);
    }

    public Thickness Extra2ImageMargin
    {
        get => (Thickness)GetValue(Extra2ImageMarginProperty);
        set => SetValue(Extra2ImageMarginProperty, value);
    }
    public double Extra2ImageHeight
    {
        get => (double)GetValue(Extra2ImageHeightProperty);
        set => SetValue(Extra2ImageHeightProperty, value);
    }
    public double Extra2ImageWidth
    {
        get => (double)GetValue(Extra2ImageWidthProperty);
        set => SetValue(Extra2ImageWidthProperty, value);
    }
    public bool Extra2IsVisible
    {
        get => (bool)GetValue(Extra2IsVisibleProperty);
        set => SetValue(Extra2IsVisibleProperty, value);
    }
    public IAsyncRelayCommand Extra3Command
    {
        get => (IAsyncRelayCommand)GetValue(Extra3CommandProperty);
        set => SetValue(Extra3CommandProperty, value);
    }

    public ImageSource Extra3Image
    {
        get => (ImageSource)GetValue(Extra3ImageProperty);
        set => SetValue(Extra3ImageProperty, value);
    }

    public Thickness Extra3ImageMargin
    {
        get => (Thickness)GetValue(Extra3ImageMarginProperty);
        set => SetValue(Extra3ImageMarginProperty, value);
    }
    public double Extra3ImageHeight
    {
        get => (double)GetValue(Extra3ImageHeightProperty);
        set => SetValue(Extra3ImageHeightProperty, value);
    }
    public double Extra3ImageWidth
    {
        get => (double)GetValue(Extra3ImageWidthProperty);
        set => SetValue(Extra3ImageWidthProperty, value);
    }
    public bool Extra3IsVisible
    {
        get => (bool)GetValue(Extra3IsVisibleProperty);
        set => SetValue(Extra3IsVisibleProperty, value);
    }
    private async void OnBackClicked(object sender, TappedEventArgs e)
    {
        await NavigationModel.PopAsync();
    }
}