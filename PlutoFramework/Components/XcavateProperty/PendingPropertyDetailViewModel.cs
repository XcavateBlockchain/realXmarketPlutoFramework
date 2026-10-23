using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Alerts;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model.Xcavate;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The admin's look at one pending property: the record as the developer declared it, with a
/// Reject / Approve pair at the bottom while the record still awaits a decision. A decision
/// writes the new status to the developer property store (Submitted → Verified or Rejected)
/// and swaps the buttons for the outcome, which is also what reopening the record later
/// shows - the buttons belong to the Submitted state alone.
/// </summary>
public partial class PendingPropertyDetailViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(PropertyImageSource))]
    [NotifyPropertyChangedFor(nameof(HasCompany))]
    [NotifyPropertyChangedFor(nameof(SubmittedText))]
    [NotifyPropertyChangedFor(nameof(HasSubmitted))]
    [NotifyPropertyChangedFor(nameof(ShowDecisionButtons))]
    [NotifyPropertyChangedFor(nameof(ShowDecidedText))]
    [NotifyPropertyChangedFor(nameof(DecidedText))]
    [NotifyPropertyChangedFor(nameof(DecidedColor))]
    private DeveloperProperty property;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DecisionButtonsState))]
    private bool saving;

    public PendingPropertyDetailViewModel(DeveloperProperty property)
    {
        this.property = property;
    }

    public string Title => Property.NameText;

    /// <summary>The record's photo, or the app's placeholder - the card's own rule.</summary>
    public ImageSource PropertyImageSource
    {
        get
        {
            var imageUrl = Property.ImageUrl;

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return "noimage.png";
            }

            if (!imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return imageUrl;
            }

            return new UriImageSource
            {
                Uri = new Uri(imageUrl),
                CacheValidity = new TimeSpan(1, 0, 0),
            };
        }
    }

    public bool HasCompany => !string.IsNullOrWhiteSpace(Property.CompanyName);

    public string SubmittedText => Property.CreatedAt?.ToLocalTime().ToString("d MMM yyyy") ?? string.Empty;

    public bool HasSubmitted => Property.CreatedAt is not null;

    /// <summary>The buttons belong to the awaiting-decision state alone.</summary>
    public bool ShowDecisionButtons => Property.Status == DeveloperPropertyStatus.Submitted;

    public bool ShowDecidedText =>
        Property.Status is DeveloperPropertyStatus.Verified or DeveloperPropertyStatus.Rejected;

    public string DecidedText =>
        Property.Status == DeveloperPropertyStatus.Verified ? "Approved" : "Rejected";

    public Color DecidedColor => Property.Status == DeveloperPropertyStatus.Verified
        ? (Color)Application.Current!.Resources["Primary"]
        : (Color)Application.Current!.Resources["DangerousRed"];

    public ButtonStateEnum DecisionButtonsState =>
        Saving ? ButtonStateEnum.Disabled : ButtonStateEnum.Enabled;

    [RelayCommand]
    private Task ApproveAsync() => DecideAsync(DeveloperPropertyStatus.Verified);

    [RelayCommand]
    private Task RejectAsync() => DecideAsync(DeveloperPropertyStatus.Rejected);

    /// <summary>
    /// The decision, written through. The store answers with the record as it stands after
    /// the write - or as it stands, when a second admin's decision beat this one - so the
    /// page always settles on the truth rather than on what the tap hoped for. Null means
    /// the decision did not go through: the buttons stay, and a retry is a tap away.
    /// </summary>
    private async Task DecideAsync(DeveloperPropertyStatus status)
    {
        if (Saving)
        {
            return;
        }

        Saving = true;

        try
        {
            var updated = await DeveloperPropertyClient.UpdatePropertyStatusAsync(
                Property.PropertyId, status, CancellationToken.None);

            if (updated is null)
            {
                await Toast.Make("The property could not be updated. Please try again.").Show();
                return;
            }

            Property = updated;
        }
        finally
        {
            Saving = false;
        }
    }
}
