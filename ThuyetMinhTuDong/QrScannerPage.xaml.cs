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
            CameraLocation = CameraLocation.Rear,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormat.QrCode,
                AutoRotate = true,
                Multiple = false,
                TryHarder = true,
                TryInverted = true
            }
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
                    Text = "Đang tìm QR Code... Đưa mã vào giữa hình",
                    TextColor = Colors.LightGreen,
                    FontAttributes = FontAttributes.Bold,
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

        _isHandlingResult = true;

        if (sender is CameraBarcodeReaderView qrReader)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                qrReader.IsDetecting = false;
            });
        }

        // Tùy ch?n: rung thi?t b? nh? d? báo hi?u dã quét thành công
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }

        // Log t?t c? k?t qu? quét du?c
        if (e.Results != null)
        {
            foreach (var result in e.Results)
            {
                System.Diagnostics.Debug.WriteLine($"[QR Scanner] Format: {result.Format}, Value: {result.Value}");
            }
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var qrString = scannedValue.Trim();

                // Lấy ID: do mã QR value bây giờ đã đổi thành id POI
                if (int.TryParse(qrString, out int poiId))
                {
                    // Truyền params qrPoiId về trang gốc (MainPage) qua Shell Navigation
                    await Shell.Current.GoToAsync($"..?qrPoiId={poiId}");
                }
                else
                {
                    if (Uri.TryCreate(qrString, UriKind.Absolute, out var uri))
                    {
                        await Launcher.Default.OpenAsync(uri);
                        await Shell.Current.GoToAsync("..");
                    }
                    else
                    {
                        await DisplayAlertAsync("QR", scannedValue, "OK");
                        await Shell.Current.GoToAsync("..");
                    }
                }
            }
            finally
            {
                _isHandlingResult = false;
            }
        });
    }
}
