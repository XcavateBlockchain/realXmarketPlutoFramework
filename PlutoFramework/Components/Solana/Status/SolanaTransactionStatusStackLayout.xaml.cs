using System.ComponentModel;
using PlutoFramework.Components.Extrinsic;

namespace PlutoFramework.Components.Solana.Status;

/// <summary>
/// The Solana toast stack, mounted in the page template beside the Substrate one.
/// </summary>
/// <remarks>
/// Both stacks are positioned at the top of the page with the same layout bounds, so without
/// an offset a Solana toast would sit on top of a Substrate one whenever both are showing.
/// This reads the Substrate stack's height and drops below it.
///
/// Deliberately one-way: the Substrate stack knows nothing about this one, so its behaviour
/// cannot regress from anything here.
/// </remarks>
public partial class SolanaTransactionStatusStackLayout : ContentView
{
    /// <summary>Matches the spacing between toasts inside a stack.</summary>
    private const int StackSpacing = 15;

    private const int BaseTranslation = 20;

    private readonly ExtrinsicStatusStackViewModel substrateStack;

    private readonly SolanaTransactionStatusStackViewModel viewModel;

    public SolanaTransactionStatusStackLayout()
    {
        InitializeComponent();

        viewModel = DependencyService.Get<SolanaTransactionStatusStackViewModel>();
        substrateStack = DependencyService.Get<ExtrinsicStatusStackViewModel>();

        BindingContext = viewModel;

        substrateStack.PropertyChanged += OnSubstrateStackChanged;

        ApplyOffset();
    }

    /// <summary>
    /// The Substrate stack view model is an app-wide singleton, so a subscription outlives
    /// every page. Dropping it when the view is detached keeps each visited page from leaving
    /// a listener behind.
    /// </summary>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            substrateStack.PropertyChanged -= OnSubstrateStackChanged;
        }
    }

    private void OnSubstrateStackChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExtrinsicStatusStackViewModel.HeightRequest)
            or nameof(ExtrinsicStatusStackViewModel.IsVisible))
        {
            MainThread.BeginInvokeOnMainThread(ApplyOffset);
        }
    }

    private void ApplyOffset()
    {
        var occupied = substrateStack.IsVisible ? substrateStack.HeightRequest + StackSpacing : 0;

        viewModel.TopOffset = occupied;

        TranslationY = BaseTranslation + occupied;
    }
}
