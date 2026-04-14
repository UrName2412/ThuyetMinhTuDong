using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace ThuyetMinhTuDong;

public class QrScannerPage : ContentPage
{
    private bool _isHandlingResult;

    public QrScannerPage()
    {
        Title = "Quét QR";
        BackgroundColor = Color.FromArgb("#0F0F0F");

        var qrReader = new CameraBarcodeReaderView
        {
            IsDetecting = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        qrReader.BarcodesDetected += OnBarcodesDetected;

        Content = new Grid
        {
            Padding = 12,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            },
            Children =
            {
                new Label
                {
                    Text = "Đưa mã QR vào khung camera",
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 8, 0, 12)
                },
                qrReader
            }
        };

        Grid.SetRow(qrReader, 1);
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_isHandlingResult)
            return;

        var scannedValue = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(scannedValue))
            return;

        // Log tất cả kết quả quét được
        if (e.Results != null)
        {
            foreach (var result in e.Results)
            {
                System.Diagnostics.Debug.WriteLine($"[QR Scanner] Format: {result.Format}, Value: {result.Value}");
            }
        }

        _isHandlingResult = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (Uri.TryCreate(scannedValue, UriKind.Absolute, out var uri))
                {
                    await Launcher.Default.OpenAsync(uri);
                }
                else
                {
                    await DisplayAlert("QR", scannedValue, "OK");
                }

                await Shell.Current.GoToAsync("..");
            }
            finally
            {
                _isHandlingResult = false;
            }
        });
    }
}
