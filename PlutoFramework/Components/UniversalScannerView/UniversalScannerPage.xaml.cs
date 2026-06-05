using PlutoFramework.Model;
using SkiaSharp;
using ZXing;
using ZXing.Net.Maui;
using MauiBarcodeFormat = ZXing.Net.Maui.BarcodeFormat;
using ZXingBarcodeFormat = ZXing.BarcodeFormat;

namespace PlutoFramework.Components.UniversalScannerView;

public partial class UniversalScannerPage : ContentPage
{
    private EventHandler<BarcodeDetectionEventArgs>? _onScanned;

    public UniversalScannerPage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        Shell.SetNavBarIsVisible(this, false);

        InitializeComponent();

        scanner.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
        };
    }

    public EventHandler<BarcodeDetectionEventArgs> OnScannedMethod
    {
        set
        {
            _onScanned = value;
            scanner.BarcodesDetected += value;
        }
    }

    private void OnDetected(System.Object sender, ZXing.Net.Maui.BarcodeDetectionEventArgs e)
    {
        scanner.IsDetecting = false;
    }

    private async void OnUploadQrCodeTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Select a QR code image"
            });

            if (result == null)
            {
                return;
            }

            await using var stream = await result.OpenReadAsync();
            var decodedText = DecodeQrFromStream(stream);

            if (string.IsNullOrWhiteSpace(decodedText))
            {
                return;
            }

            scanner.IsDetecting = false;
            _onScanned?.Invoke(this, new BarcodeDetectionEventArgs([
                new BarcodeResult
                {
                    Format = MauiBarcodeFormat.QrCode,
                    Value = decodedText
                }
            ]));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private string? DecodeQrFromStream(Stream stream)
    {
        using var bitmap = SKBitmap.Decode(stream);
        if (bitmap == null)
        {
            return null;
        }

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new List<ZXingBarcodeFormat> { ZXingBarcodeFormat.QR_CODE }
            }
        };

        var rgba = new byte[bitmap.Width * bitmap.Height * 4];
        var pixels = bitmap.Pixels;

        for (var i = 0; i < pixels.Length; i++)
        {
            var color = pixels[i];
            var offset = i * 4;
            rgba[offset] = color.Red;
            rgba[offset + 1] = color.Green;
            rgba[offset + 2] = color.Blue;
            rgba[offset + 3] = color.Alpha;
        }

        var luminanceSource = new RGBLuminanceSource(rgba, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
        var result = reader.Decode(luminanceSource);
        return result?.Text;
    }

    private async void OnMyQrCodeTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();

        ReceiveAndTransferModel.Receive();
    }
}
