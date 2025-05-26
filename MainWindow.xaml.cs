using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using ytDownloaderWPF.Classes;
using System.IO.Compression;
using System.Net;


namespace ytDownloaderWPF
{
    public partial class MainWindow : Window
    {
        private readonly YoutubeDL _youtubeDL;
        private string _fetchedMetadataTitle = "video";

        private readonly Brush RedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff0033"));
        private readonly Brush GrayBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2c2c2a"));

        public MainWindow()
        {
            InitializeComponent();

            _youtubeDL = new YoutubeDL();

            // Don't check/download in the constructor!
            // The window will now show up as soon as possible.
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await EnsureYtDlpAndFfmpegAsync();

            // Now that everything is downloaded, set up paths as before
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string ytdlpFullPath = Path.Combine(exeDirectory, "yt-dlp.exe");
            string ffmpegFullPath = Path.Combine(exeDirectory, "ffmpeg.exe");

            if (!File.Exists(ytdlpFullPath))
            {
                MessageBox.Show("yt-dlp.exe could not be found or downloaded in app directory.\nPlease check your internet connection or manually download yt-dlp.exe and place it here:\n" + exeDirectory, "Missing yt-dlp", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }
            if (!File.Exists(ffmpegFullPath))
            {
                MessageBox.Show("ffmpeg.exe could not be found or downloaded in app directory.\nPlease check your internet connection or manually download ffmpeg.exe and place it here:\n" + exeDirectory, "Missing ffmpeg", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            _youtubeDL.YoutubeDLPath = ytdlpFullPath;
            _youtubeDL.FFmpegPath = ffmpegFullPath;
            _youtubeDL.OutputFolder = exeDirectory;
        }

        private async Task EnsureYtDlpAndFfmpegAsync()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string ytdlpExe = Path.Combine(exeDir, "yt-dlp.exe");
            string ffmpegExe = Path.Combine(exeDir, "ffmpeg.exe");

            bool needsYtdlp = !File.Exists(ytdlpExe);
            bool needsFfmpeg = !File.Exists(ffmpegExe);

            if (needsYtdlp || needsFfmpeg)
            {
                string msg = "Required components will be downloaded automatically:\n";
                if (needsYtdlp) msg += "- yt-dlp.exe\n";
                if (needsFfmpeg) msg += "- ffmpeg.exe\n";
                msg += "\nThis requires internet access and may take a minute.";
                MessageBox.Show(msg, "MJYTDownloader First Run", MessageBoxButton.OK, MessageBoxImage.Information);

                using (var client = new WebClient())
                {
                    if (needsYtdlp)
                    {
                        string ytdlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
                        await client.DownloadFileTaskAsync(ytdlpUrl, ytdlpExe);
                    }

                    if (needsFfmpeg)
                    {
                        string ffmpegZipUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
                        string ffmpegZip = Path.Combine(exeDir, "ffmpeg.zip");
                        await client.DownloadFileTaskAsync(ffmpegZipUrl, ffmpegZip);

                        // Extract ffmpeg.exe
                        try
                        {
                            using (var zip = ZipFile.OpenRead(ffmpegZip))
                            {
                                // Look for ffmpeg.exe in the extracted zip (varies, so do it flexibly)
                                foreach (var entry in zip.Entries)
                                {
                                    if (entry.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                                    {
                                        entry.ExtractToFile(ffmpegExe, true);
                                        break;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            if (File.Exists(ffmpegZip))
                                File.Delete(ffmpegZip);
                        }
                    }
                }

                MessageBox.Show("Components downloaded! You can now use all features.", "MJYTDownloader", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            string videoUrl = VideoLinkTextBox.Text.Trim();
            if (string.IsNullOrEmpty(videoUrl))
            {
                MessageBox.Show("Please enter a video link.",
                                "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FetchButton.IsEnabled = false;
            FetchButton.Background = GrayBrush;
            FetchButton.Content = "Fetching...";

            QualityComboBox.Items.Clear();
            ThumbnailImage.Source = null;
            DownloadProgressBar.Value = 0;
            StatusLabel.Text = "";

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
                if (!string.IsNullOrEmpty(metadata.Title))
                    _fetchedMetadataTitle = metadata.Title;

                // Thumbnail
                if (!string.IsNullOrEmpty(metadata.Thumbnail))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(metadata.Thumbnail, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        ThumbnailImage.Source = bitmap;
                    }
                    catch { /* ignore thumbnail errors */ }
                }

                // Show all video formats (will merge with bestaudio)
                foreach (var format in metadata.Formats)
                {
                    if (format.VideoCodec == "none") continue;

                    string formatString = $"{format.FormatId}+bestaudio";
                    string resolution = (format.Width > 0 && format.Height > 0)
                        ? $"{format.Width}x{format.Height}"
                        : "unknown";
                    string displayText = $"{format.FormatId} ({format.FormatNote}, {format.Extension}, {resolution}) + bestaudio";
                    QualityComboBox.Items.Add(new ComboBoxItemWrapper(formatString, displayText));
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
                FetchButton.IsEnabled = true;
                FetchButton.Background = RedBrush;
                FetchButton.Content = "Fetch Metadata";
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            string videoUrl = VideoLinkTextBox.Text.Trim();
            if (string.IsNullOrEmpty(videoUrl))
            {
                MessageBox.Show("Please enter a video link.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Save Video As",
                Filter = "MP4 file|*.mp4|All files|*.*",
                FileName = _fetchedMetadataTitle + ".mp4"
            };
            bool? result = dialog.ShowDialog();
            if (result != true)
                return;

            string folderPath = Path.GetDirectoryName(dialog.FileName) ?? AppDomain.CurrentDomain.BaseDirectory;
            string fileName = Path.GetFileName(dialog.FileName);

            DownloadButton.IsEnabled = false;
            DownloadButton.Background = GrayBrush;
            DownloadButton.Content = "Downloading...";

            DownloadProgressBar.Value = 0;
            StatusLabel.Text = "Starting download...";
            DownloadProgressBar.IsIndeterminate = true;

            try
            {
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string ytdlpPath = Path.Combine(exeDirectory, "yt-dlp.exe");
                string ffmpegPath = Path.Combine(exeDirectory, "ffmpeg.exe");

                string outputTemplate = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(fileName)) + ".%(ext)s";

                long expectedFileSize = 0;
                string actualOutputPath = null;

                // -- For future: can improve this by parsing [download] lines for % and total size --
                System.Text.RegularExpressions.Regex sizeRegex = new System.Text.RegularExpressions.Regex(@"of ([\d.]+[KMGT]?i?B)", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = ytdlpPath,
                    Arguments = $"-f bestvideo+bestaudio/best --merge-output-format mp4 --ffmpeg-location \"{ffmpegPath}\" -o \"{outputTemplate}\" \"{videoUrl}\" --newline",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process proc = new Process { StartInfo = psi })
                {
                    proc.Start();

                    // Read StandardError for progress info
                    var stderrReader = System.Threading.Tasks.Task.Run(() =>
                    {
                        while (!proc.StandardError.EndOfStream)
                        {
                            string line = proc.StandardError.ReadLine();
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            // Example line: [download]  23.1% of 48.88MiB at  1.18MiB/s ETA 00:41
                            if (expectedFileSize == 0)
                            {
                                var match = sizeRegex.Match(line);
                                if (match.Success)
                                {
                                    expectedFileSize = ParseSize(match.Groups[1].Value);
                                }
                            }
                        }
                    });

                    // Read StandardOutput in background (prevents blocking)
                    var stdoutReader = System.Threading.Tasks.Task.Run(() =>
                    {
                        while (!proc.StandardOutput.EndOfStream)
                        {
                            string line = proc.StandardOutput.ReadLine();
                            // Optional: You can parse actualOutputPath here if needed
                        }
                    });

                    // Monitor file size every 0.5s and update progress bar
                    await System.Threading.Tasks.Task.Run(async () =>
                    {
                        string partFile = Path.Combine(folderPath, Path.GetFileNameWithoutExtension(fileName) + ".part");
                        string finalFile = Path.Combine(folderPath, Path.GetFileName(fileName));
                        while (!proc.HasExited)
                        {
                            if (expectedFileSize > 0)
                            {
                                string fileToCheck = File.Exists(partFile) ? partFile : finalFile;
                                if (File.Exists(fileToCheck))
                                {
                                    long sizeNow = new FileInfo(fileToCheck).Length;
                                    double percent = Math.Min(100.0, (sizeNow * 100.0) / expectedFileSize);

                                    Dispatcher.Invoke(() =>
                                    {
                                        DownloadProgressBar.IsIndeterminate = false;
                                        DownloadProgressBar.Value = percent;
                                        StatusLabel.Text = $"Downloading... {percent:F1}%";
                                    });
                                }
                            }
                            else
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    DownloadProgressBar.IsIndeterminate = true;
                                    StatusLabel.Text = "Getting file size...";
                                });
                            }
                            await System.Threading.Tasks.Task.Delay(500);
                        }
                    });

                    await proc.WaitForExitAsync();
                    await stderrReader;
                    await stdoutReader;

                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgressBar.IsIndeterminate = false;
                        DownloadProgressBar.Value = 100;
                        StatusLabel.Text = "Download complete!";
                    });
                }

                MessageBox.Show("Download completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                try
                {
                    Process.Start("explorer.exe", $"/select,\"{Path.Combine(folderPath, fileName)}\"");
                }
                catch { }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Download failed.";
                MessageBox.Show("Download failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DownloadButton.IsEnabled = true;
                DownloadButton.Background = RedBrush;
                DownloadButton.Content = "Download";
            }
        }

        // Utility: parse "12.3MiB" or "1.23GiB" to bytes
        private long ParseSize(string size)
        {
            if (string.IsNullOrWhiteSpace(size)) return 0;
            size = size.Trim().ToUpperInvariant();

            double multiplier = 1;
            if (size.EndsWith("GIB")) { multiplier = 1024 * 1024 * 1024; size = size.Replace("GIB", ""); }
            else if (size.EndsWith("GB")) { multiplier = 1000 * 1000 * 1000; size = size.Replace("GB", ""); }
            else if (size.EndsWith("MIB")) { multiplier = 1024 * 1024; size = size.Replace("MIB", ""); }
            else if (size.EndsWith("MB")) { multiplier = 1000 * 1000; size = size.Replace("MB", ""); }
            else if (size.EndsWith("KIB")) { multiplier = 1024; size = size.Replace("KIB", ""); }
            else if (size.EndsWith("KB")) { multiplier = 1000; size = size.Replace("KB", ""); }
            else if (size.EndsWith("B")) { multiplier = 1; size = size.Replace("B", ""); }

            if (double.TryParse(size.Trim(), out double value))
                return (long)(value * multiplier);
            return 0;
        }
    }
}
