using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;

namespace Bootstrapper
{
    public static class Program
    {
        private static readonly string CurrentDirectory = Environment.CurrentDirectory;
        private static readonly string ZipLocation = Path.Combine(CurrentDirectory, "RezWare.zip");
        private static readonly string InstallDirectory = Path.Combine(CurrentDirectory, "RezWare");
        private static readonly string AdditionalZipLocation = Path.Combine(CurrentDirectory, "bin.zip");
        private static readonly string VersionFilePath = Path.Combine(CurrentDirectory, "build.txt");
        private static readonly string ExecutablePath = Path.Combine(InstallDirectory, "RezWare.exe");
        private const string GitHubApiUrl = "https://api.github.com/repos/RezWare-SoftWare/BootstrapperRez/releases/latest";
        private const string AdditionalZipUrl = "https://cdn.discordapp.com/attachments/1299965685832749059/1303310504433025154/bin.zip?ex=672b49fe&is=6729f87e&hm=70ff481c8698dd95f104e35297fc2146ad6a1a0985e8ba16e2c3ee7752acc89b&";

        private static TaskCompletionSource<bool> _downloadTaskCompletionSource = new TaskCompletionSource<bool>();

        private static void ShowBanner()
        {
            Console.Clear();

            string banner = @"
__________                __      __                       
\______   \ ____ ________/  \    /  \_____ _______   ____  
 |       _// __ \\___   /\   \/\/   /\__  \\_  __ \_/ __ \ 
 |    |   \  ___/ /    /  \        /  / __ \|  | \/\  ___/ 
 |____|_  /\___  >_____ \  \__/\  /  (____  /__|    \___  >
        \/     \/      \/       \/        \/            \/   
";

            string[] bannerLines = banner.Split('\n');
            int consoleWidth = Console.WindowWidth;

            Console.ForegroundColor = ConsoleColor.DarkYellow;

            for (int i = 0; i < bannerLines.Length; i++)
            {
                int padding = (consoleWidth - bannerLines[i].Length) / 2;
                Console.WriteLine(new string(' ', Math.Max(padding, 0)) + bannerLines[i]);
            }

            Console.WriteLine();
            Console.ResetColor();
        }

        private static void DeleteOldFiles()
        {
            if (Directory.Exists(InstallDirectory))
            {
                Directory.Delete(InstallDirectory, true);
                Console.WriteLine("[*] Old files deleted.");
            }
        }

        private static void SaveBuildInfo(string version)
        {
            File.WriteAllText(VersionFilePath, version);
            Console.WriteLine($"[*] Updated build version saved: {version}");
        }

        private static (string, string) GetLatestReleaseInfo()
        {
            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", "BootstrapperApp");
                var json = client.DownloadString(GitHubApiUrl);
                var tagNameStart = json.IndexOf("\"tag_name\":\"") + 12;
                var tagNameEnd = json.IndexOf("\"", tagNameStart);
                var tagName = json.Substring(tagNameStart, tagNameEnd - tagNameStart);
                var downloadUrlStart = json.IndexOf("\"browser_download_url\":\"") + 24;
                var downloadUrlEnd = json.IndexOf("\"", downloadUrlStart);
                var zipUrl = json.Substring(downloadUrlStart, downloadUrlEnd - downloadUrlStart);
                Console.WriteLine($"[*] Latest version found on GitHub: {tagName}");
                return (tagName, zipUrl);
            }
        }

        private static bool IsUpToDate(string latestVersion)
        {
            if (!File.Exists(VersionFilePath)) return false;
            var currentVersion = File.ReadAllText(VersionFilePath).Trim();
            return currentVersion == latestVersion;
        }

        private static void DownloadAdditionalFiles()
        {
            string binDirectory = Path.Combine(CurrentDirectory, "bin");
            if (Directory.Exists(binDirectory))
            {
                Console.WriteLine("[*] Additional files already downloaded.");
                return;
            }

            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "BootstrapperApp");
                wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                wc.DownloadFileCompleted += Wc_DownloadFileCompletedAdditional;
                wc.DownloadFileAsync(new Uri(AdditionalZipUrl), AdditionalZipLocation);
            }
        }

        private static void Wc_DownloadFileCompletedAdditional(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                PrintError($"Download error for additional files: {e.Error?.Message}");
                _downloadTaskCompletionSource.SetResult(false);
                return;
            }

            Console.WriteLine("\n[*] Additional files downloaded. Extracting...");
            ZipFile.ExtractToDirectory(AdditionalZipLocation, CurrentDirectory);
            File.Delete(AdditionalZipLocation);
            Console.WriteLine("[*] Additional files installation complete.");

            string binDirectory = Path.Combine(CurrentDirectory, "bin");
            if (Directory.Exists(binDirectory))
            {
                Console.WriteLine("[*] bin folder exists after extraction.");
            }
            else
            {
                PrintError("[!] bin folder does not exist after extraction.");
            }

            _downloadTaskCompletionSource.SetResult(true);
        }

        private static void CreateRequiredDirectories()
        {
            var workspaceDirectory = Path.Combine(CurrentDirectory, "workspace");
            if (!Directory.Exists(workspaceDirectory))
            {
                Directory.CreateDirectory(workspaceDirectory);
                Console.WriteLine("[*] Created 'workspace' directory.");
            }
        }

        private static void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Write($"\r[*] Downloading... {e.ProgressPercentage}% ({e.BytesReceived}/{e.TotalBytesToReceive} bytes)");
        }

        private static void Wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                PrintError($"Download error: {e.Error?.Message}");
                _downloadTaskCompletionSource.SetResult(false);
                return;
            }

            Console.WriteLine("\n[*] Download complete. Extracting files...");
            ZipFile.ExtractToDirectory(ZipLocation, InstallDirectory);
            SaveBuildInfo(latestVersion);
            File.Delete(ZipLocation);
            Console.WriteLine("[*] Installation complete. Launching application...");

            if (File.Exists(ExecutablePath))
            {
                System.Diagnostics.Process.Start(ExecutablePath);
            }
            else
            {
                PrintError("RezWare.exe not found.");
            }

            DownloadAdditionalFiles();
        }

        private static string latestVersion;

        private static void PrintError(string message)
        {
            var colors = new[] { ConsoleColor.Red, ConsoleColor.Yellow, ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.Magenta };
            for (int i = 0; i < message.Length; i++)
            {
                Console.ForegroundColor = colors[i % colors.Length];
                Console.Write(message[i]);
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        private static async Task Main(string[] args)
        {
            Console.Title = "RezWare Bootstrapper";
            ShowBanner();
            Console.WriteLine("[*] Checking for updates...");

            try
            {
                var releaseInfo = GetLatestReleaseInfo();
                latestVersion = releaseInfo.Item1;
                var zipUrl = releaseInfo.Item2;

                if (!IsUpToDate(latestVersion))
                {
                    Console.WriteLine($"[*] New update available: RezWare {latestVersion}. Updating...");
                    DeleteOldFiles();

                    using (var wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "BootstrapperApp");
                        wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                        wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
                        wc.DownloadFileAsync(new Uri(zipUrl), ZipLocation);
                    }
                }
                else
                {
                    Console.WriteLine("[*] You are already using the latest version.");
                    LaunchApplication();
                }
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
            }

            CreateRequiredDirectories();
            Console.WriteLine("\n[!] The application will close when end loading in 3 seconds...");

            await _downloadTaskCompletionSource.Task;

            await Task.Delay(3000);
            Environment.Exit(0);
        }

        private async static void LaunchApplication()
        {
            if (File.Exists(ExecutablePath))
            {
                Console.WriteLine("[*] Launching the application...");
                System.Diagnostics.Process.Start(ExecutablePath);

                await Task.Delay(3000);
                Environment.Exit(0);
            }
            else
            {
                PrintError("RezWare.exe not found.");
            }
        }
    }
}
