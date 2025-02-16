using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;               // For SaveFileDialog
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using ytDownloaderWPF.Classes;

namespace ytDownloaderWPF
{
    public partial class MainWindow : Window
    {
        private readonly YoutubeDL _youtubeDL;
        // Store the last fetched title so we can use it as the default file name
        private string _fetchedMetadataTitle = "video";

        // Colors
        private readonly Brush RedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff0033"));
        private readonly Brush GrayBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2c2c2a"));

        public MainWindow()
        {
            InitializeComponent();

            _youtubeDL = new YoutubeDL();

            // Build absolute path to yt-dlp.exe, assuming it is in the same folder as your .exe.
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string ytdlpFullPath = Path.Combine(exeDirectory, "yt-dlp.exe");

            _youtubeDL.YoutubeDLPath = ytdlpFullPath;
            _youtubeDL.OutputFolder = exeDirectory; // Default output folder
        }

        /// <summary>
        /// Fetch metadata from the provided video URL.
        /// </summary>
        private async void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            string videoUrl = VideoLinkTextBox.Text.Trim();
            if (string.IsNullOrEmpty(videoUrl))
            {
                MessageBox.Show("Please enter a video link.",
                                "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Disable button, show status
            FetchButton.IsEnabled = false;
            FetchButton.Background = GrayBrush;
            FetchButton.Content = "Fetching...";

            // Clear previous results.
            QualityComboBox.Items.Clear();
            ThumbnailImage.Source = null;
            DownloadProgressBar.Value = 0;

            try
            {
                var fetchResult = await _youtubeDL.RunVideoDataFetch(videoUrl);
                if (!fetchResult.Success)
                {
                    string errors = fetchResult.ErrorOutput != null
                        ? string.Join(", ", fetchResult.ErrorOutput)
                        : "Unknown error";

                    MessageBox.Show("Failed to fetch video metadata: " + errors,
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var metadata = fetchResult.Data;
                // Store the title for default naming
                if (!string.IsNullOrEmpty(metadata.Title))
                {
                    _fetchedMetadataTitle = metadata.Title;
                }

                // Load thumbnail image if available.
                if (!string.IsNullOrEmpty(metadata.Thumbnail))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(metadata.Thumbnail, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ThumbnailImage.Source = bitmap;
                }

                // Populate the ComboBox with available format options.
                foreach (var format in metadata.Formats)
                {
                    if (string.IsNullOrEmpty(format.FormatId))
                        continue;

                    string displayText = $"{format.FormatId} - {format.Format} - {format.FormatNote}";
                    QualityComboBox.Items.Add(new ComboBoxItemWrapper(format.FormatId, displayText));
                }

                if (QualityComboBox.Items.Count > 0)
                {
                    QualityComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error fetching metadata: " + ex.Message,
                                "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable button, revert color & text
                FetchButton.IsEnabled = true;
                FetchButton.Background = RedBrush;
                FetchButton.Content = "Fetch Metadata";
            }
        }

        /// <summary>
        /// Download the video using the selected format.
        /// </summary>
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            string videoUrl = VideoLinkTextBox.Text.Trim();
            if (string.IsNullOrEmpty(videoUrl))
            {
                MessageBox.Show("Please enter a video link.",
                                "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (QualityComboBox.SelectedItem is not ComboBoxItemWrapper selectedItem)
            {
                MessageBox.Show("Please select a quality option.",
                                "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Let the user pick where to save the file
            var dialog = new SaveFileDialog
            {
                Title = "Save Video As",
                Filter = "MP4 file|*.mp4|All files|*.*",
                FileName = _fetchedMetadataTitle + ".mp4"
            };
            bool? result = dialog.ShowDialog();
            if (result != true)
            {
                // User canceled
                return;
            }

            // Set the output folder & filename to the user's choice
            string folderPath = Path.GetDirectoryName(dialog.FileName) ?? AppDomain.CurrentDomain.BaseDirectory;
            string fileName = Path.GetFileName(dialog.FileName);

            _youtubeDL.OutputFolder = folderPath;
            _youtubeDL.OutputFileTemplate = fileName;

            // Create a progress handler for download progress.
            // (Multiplying by 100 ensures the bar goes from 0 to 100.)
            var progressHandler = new Progress<DownloadProgress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    DownloadProgressBar.Value = p.Progress * 100;
                });
            });

            // Disable button, show status
            DownloadButton.IsEnabled = false;
            DownloadButton.Background = GrayBrush;
            DownloadButton.Content = "Downloading...";

            try
            {
                DownloadProgressBar.Value = 0;
                var downloadResult = await _youtubeDL.RunVideoDownload(
                    videoUrl,
                    selectedItem.Value,
                    YoutubeDLSharp.Options.DownloadMergeFormat.Unspecified,
                    YoutubeDLSharp.Options.VideoRecodeFormat.None,
                    CancellationToken.None,
                    progress: progressHandler);

                if (downloadResult.Success)
                {
                    MessageBox.Show("Download completed successfully!",
                                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Download failed.",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error during download: " + ex.Message,
                                "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable button, revert color & text
                DownloadButton.IsEnabled = true;
                DownloadButton.Background = RedBrush;
                DownloadButton.Content = "Download";
            }
        }
    }

   
}
