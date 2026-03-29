using ThuyetMinhTuDong.Data;
using ThuyetMinhTuDong.Models;

namespace ThuyetMinhTuDong
{
    public partial class QRScannerPage : ContentPage
    {
        private readonly LocalDatabase _database;

        public QRScannerPage(LocalDatabase database)
        {
            InitializeComponent();
            _database = database;
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            string qrValue = QRInputEntry.Text?.Trim();

            if (string.IsNullOrEmpty(qrValue))
            {
                ResultLabel.Text = "Vui lòng nhập hoặc quét mã QR";
                return;
            }

            try
            {
                // Search for QR code in database
                var qrCode = await _database.GetQRCodeByValueAsync(qrValue);

                if (qrCode == null)
                {
                    ResultLabel.Text = $"Không tìm thấy mã QR '{qrValue}'";
                    return;
                }

                // Pass the QR code data back to MainPage
                await Shell.Current.GoToAsync($"///main?qrId={qrCode.Id}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Đã xảy ra lỗi: {ex.Message}", "OK");
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("///main");
        }
    }
}

