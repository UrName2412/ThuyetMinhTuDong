using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ThuyetMinhTuDong.Services
{
    public class StatusService : INotifyPropertyChanged
    {
        private string _statusMessage = "Đang khởi tạo...";
        private string _statusColor = "Gray";

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public void UpdateStatus(string message, string color)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusMessage = message;
                StatusColor = color;
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
