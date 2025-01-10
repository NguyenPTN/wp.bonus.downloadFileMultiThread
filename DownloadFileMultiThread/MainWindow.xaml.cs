using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DownloadFileMultiThread
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async void OnDownloadButtonClick(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Status: Preparing to download...";
            ProgressBarContainer.Children.Clear(); // Xóa các ProgressBar cũ (nếu có)

            string downloadUrl = DownloadLinkTextBox.Text.Trim();
            string savePath = SavePathTextBox.Text.Trim();
            int numberOfThreads = (int)NumberOfThreadsBox.Value;
            string fileName = FileNameTextBox.Text.Trim();
            ComboBoxItem? selectedItem = FileExtensionComboBox.SelectedItem as ComboBoxItem;
            string fileExtension = selectedItem?.Content.ToString() ?? string.Empty;

            if (!ValidateInputs(downloadUrl, savePath, numberOfThreads, fileName, fileExtension))
            {
                return;
            }

            string finalSavePath = Path.Combine(savePath, $"{fileName}{fileExtension}");

            try
            {
                var downloader = new DownloadFileMultiThread(downloadUrl, finalSavePath, numberOfThreads);

                // Tạo danh sách ProgressBar tương ứng với mỗi luồng
                ProgressBar[] progressBars = new ProgressBar[numberOfThreads];
                for (int i = 0; i < numberOfThreads; i++)
                {
                    ProgressBar progressBar = new ProgressBar
                    {
                        Minimum = 0,
                        Maximum = 100,
                        Value = 0,
                        Height = 30,
                        Margin = new Thickness(0, 5, 0, 5)
                    };
                    progressBars[i] = progressBar;
                    ProgressBarContainer.Children.Add(progressBar);
                }

                StatusTextBlock.Text = "Status: Getting file size...";
                downloader.GetFileSize();

                // Bắt đầu tải và cập nhật từng ProgressBar
                downloader.ProgressChanged += (int threadIndex, int progress) =>
                {
                    // Cập nhật ProgressBar của từng luồng riêng biệt
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        progressBars[threadIndex].Value = progress;
                    });
                };

                StatusTextBlock.Text = "Status: Downloading...";
                await Task.Run(() => downloader.StartDownload());

                StatusTextBlock.Text = "Status: Download completed successfully!";
                await ShowMessageAsync("Download completed successfully!");
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Status: Error occurred.";
                await ShowMessageAsync($"An error occurred: {ex.Message}");
            }
        }

        private bool ValidateInputs(string downloadUrl, string savePath, int numberOfThreads, string fileName, string extension)
        {
            if (string.IsNullOrEmpty(downloadUrl))
            {
                _ = ShowMessageAsync("Please enter a download link.");
                return false;
            }

            if (string.IsNullOrEmpty(savePath))
            {
                _ = ShowMessageAsync("Please enter a save path.");
                return false;
            }

            if (string.IsNullOrEmpty(fileName)) {
                _ = ShowMessageAsync("Please enter a file name.");
                return false;
            }

            if (string.IsNullOrEmpty(extension))
            {
                _ = ShowMessageAsync("Please enter a file extension.");
                return false;
            }

            if (numberOfThreads < 1)
            {
                _ = ShowMessageAsync("Number of threads must be at least 1.");
                return false;
            }

            return true;
        }

        private async Task ShowMessageAsync(string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "Notification",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
