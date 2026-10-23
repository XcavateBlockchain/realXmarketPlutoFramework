using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlutoFramework.Components.Buttons;
using PlutoFramework.Model;
using PlutoFramework.Model.Xcavate;
using PlutoFramework.Model.Xcavate.Profile;
using PlutoFrameworkCore.Xcavate;
using System.Globalization;

namespace PlutoFramework.Components.XcavateProperty;

/// <summary>
/// The "Select lawyer type" sheet a lawyer opens from a sold-out property page to claim
/// the SPV-lawyer seat: it shows the developer-lawyer seat's status, the lawyer's own
/// published terms document (with an Upload new escape) and the legal-costs quote, and
/// "Claim &amp; Submit Terms" hands off to the page's executor through
/// <see cref="ContinueRequested"/>, which submits claim_spv_case. The candidacy itself
/// carries no document - the terms live on the lawyer's profile; requiring one here is
/// what the sheet's flow calls "submitting terms".
/// </summary>
public partial class ClaimSpvCasePopupViewModel : ObservableObject, IPopup, ISetToDefault
{
    /// <summary>The card's own cap; the API accepts 25 MB of PDF plus multipart overhead.</summary>
    private const long MaxTermsPdfBytes = 25L * 1024 * 1024;

    private static readonly FilePickerFileType PdfFileTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.iOS, new[] { "com.adobe.pdf" } }, // UTType values
            { DevicePlatform.Android, new[] { "application/pdf" } }, // MIME type
            { DevicePlatform.WinUI, new[] { ".pdf" } }, // file extension
            { DevicePlatform.macOS, new[] { "com.adobe.pdf" } }, // UTType values
            { DevicePlatform.Tizen, new[] { "*/*" } },
        });

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
    private bool isVisible = false;

    /// <summary>"Assigned" when the developer-lawyer seat is taken, "Available" otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeveloperLawyerStatusColor))]
    private string developerLawyerStatusText = "Available";

    public Color DeveloperLawyerStatusColor => DeveloperLawyerStatusText == "Assigned"
        ? Color.FromArgb("#357461")
        : Color.FromArgb("#6E6E6E");

    /// <summary>The lawyer's published terms document; null when they have uploaded none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TermsDocumentName))]
    [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
    private TermsDocumentInfo? termsDocument = null;

    public string TermsDocumentName => TermsDocument?.FileName ?? "No terms document uploaded";

    /// <summary>The lawyer's fee quote in tGBP; converted to the 9-decimal quote on Continue.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
    private string legalCosts = "";

    /// <summary>
    /// The validated costs quote, set by Continue before the sheet closes; null while the
    /// entry is empty, unparseable or out of range. The page's executor submits nothing
    /// without it.
    /// </summary>
    public ulong? CostsQuote { get; private set; }

    [ObservableProperty]
    private string errorMessage = "";

    /// <summary>True while a terms-document upload round trip runs - blocks a double submit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContinueButtonState))]
    private bool termsBusy = false;

    public ButtonStateEnum ContinueButtonState =>
        IsVisible && TermsDocument is not null && !TermsBusy && TryParseCosts(LegalCosts, out _)
            ? ButtonStateEnum.Enabled
            : ButtonStateEnum.Disabled;

    /// <summary>
    /// The page view model's submission callback - runs after the sheet closes, with
    /// <see cref="LegalCosts"/> still holding the validated quote.
    /// </summary>
    public Func<Task> ContinueRequested { get; set; } = () => Task.FromResult(0);

    /// <summary>
    /// Re-reads the wallet's published terms document; the profile API's 404 is the normal
    /// "no document" answer. A failed read keeps whatever the sheet already shows.
    /// </summary>
    public async Task LoadTermsAsync(string address, CancellationToken token)
    {
        try
        {
            var document = await TermsDocumentClient.GetAsync(address, token).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() => TermsDocument = document);
        }
        catch (OperationCanceledException)
        {
            // The page went away mid-query.
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to load the terms document: ");
            Console.WriteLine(ex);

            // A failed read must not look like "no document" - that would disable the
            // claim behind a false statement with no hint of what went wrong.
            MainThread.BeginInvokeOnMainThread(() =>
                ErrorMessage = "Your terms document could not be loaded. Check your connection and reopen this sheet.");
        }
    }

    /// <summary>
    /// Picks a PDF (25 MB cap), uploads it signed and swaps the document row to the new
    /// file. A declined signing prompt, an unreadable file and a server rejection all land
    /// in <see cref="ErrorMessage"/>; a cancelled picker changes nothing.
    /// </summary>
    [RelayCommand]
    public async Task UploadNewAsync()
    {
        if (TermsBusy)
        {
            return;
        }

        var address = KeysModel.GetSolanaAddress();

        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        FileResult? picked;

        try
        {
            picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select your terms PDF",
                FileTypes = PdfFileTypes,
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Terms PDF pick error:");
            Console.WriteLine(ex);
            ErrorMessage = "The file could not be opened. Try again.";
            return;
        }

        if (picked is null)
        {
            return;
        }

        if (!string.Equals(Path.GetExtension(picked.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Only PDF files can be uploaded.";
            return;
        }

        MemoryStream pdf;

        try
        {
            await using var pickedStream = await picked.OpenReadAsync();
            pdf = await ReadCappedAsync(pickedStream, MaxTermsPdfBytes, CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            ErrorMessage = "The PDF is too large - the limit is 25 MB.";
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Terms PDF read error:");
            Console.WriteLine(ex);
            ErrorMessage = "The file could not be read. Try again.";
            return;
        }

        TermsBusy = true;

        try
        {
            pdf.Position = 0;

            var info = await TermsDocumentClient.UploadAsync(
                address, picked.FileName, pdf, CancellationToken.None).ConfigureAwait(false);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TermsDocument = info;
                ErrorMessage = "";
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Terms document upload error:");
            Console.WriteLine(ex);

            MainThread.BeginInvokeOnMainThread(() =>
                ErrorMessage = "The terms document could not be uploaded. Try again.");
        }
        finally
        {
            TermsBusy = false;
        }
    }

    [RelayCommand]
    public void Cancel() => SetToDefault();

    [RelayCommand]
    public Task ContinueAsync()
    {
        if (TermsDocument is null || !TryParseCosts(LegalCosts, out var costs))
        {
            return Task.FromResult(0);
        }

        // Zero is a legitimate quote on chain; anything else goes through the share-price
        // conversion's nine-decimal and u64 guards. A rejection keeps the sheet open with
        // the reason, rather than closing it over a submission that never happens.
        ulong quote;

        try
        {
            quote = XcavateMarketplaceCallsModel.QuoteCostsFromDecimal(costs);
        }
        catch (ArgumentOutOfRangeException)
        {
            ErrorMessage = "The legal costs must have at most nine decimal places and fit the program's range.";

            return Task.FromResult(0);
        }

        CostsQuote = quote;
        ErrorMessage = "";
        IsVisible = false;

        return MainThread.InvokeOnMainThreadAsync(ContinueRequested);
    }

    public void SetToDefault()
    {
        IsVisible = false;
        LegalCosts = "";
        CostsQuote = null;
        ErrorMessage = "";
        TermsBusy = false;
        TermsDocument = null;
        DeveloperLawyerStatusText = "Available";
        ContinueRequested = () => Task.FromResult(0);
    }

    /// <summary>
    /// The costs quote as a decimal, accepting both the device's own number format and the
    /// invariant one - a UK price is typed with a point whatever the phone's locale says.
    /// Zero is a legitimate quote (the program only caps it at the collected fees).
    /// </summary>
    public static bool TryParseCosts(string text, out decimal costs) =>
        (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out costs)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out costs))
        && costs >= 0;

    private static async Task<MemoryStream> ReadCappedAsync(
        Stream source, long maxBytes, CancellationToken token)
    {
        var memory = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0L;

        int read;
        while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            total += read;

            if (total > maxBytes)
            {
                throw new InvalidDataException("The file exceeds the size cap.");
            }

            memory.Write(buffer, 0, read);
        }

        memory.Position = 0;
        return memory;
    }
}
