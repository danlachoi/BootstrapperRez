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
        private static readonly string VersionFilePath = Path.Combine(CurrentDirectory, "build.txt");
        private static readonly string ExecutablePath = Path.Combine(InstallDirectory, "RezWareUi.exe");
        private const string GitHubApiUrl = "https://api.github.com/repos/RezWare-SoftWare/BootstrapperRez/releases/latest";

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
            foreach (string line in bannerLines)
            {
                int padding = (consoleWidth - line.Length) / 2;
                Console.WriteLine(new string(' ', Math.Max(padding, 0)) + line);
            }
            Console.WriteLine();
            Console.ResetColor();
        }

        private static void DeleteOldFiles()
        {
            if (Directory.Exists(InstallDirectory))
            {
                Console.WriteLine("[*] Deleting old files, but preserving the 'scripts' folder...");
                foreach (var directory in Directory.GetDirectories(InstallDirectory))
                {
                    if (Path.GetFileName(directory) != "scripts")
                    {
                        Directory.Delete(directory, true);
                    }
                }
                foreach (var file in Directory.GetFiles(InstallDirectory))
                {
                    File.Delete(file);
                }
                Console.WriteLine("[*] Old files deleted successfully, 'scripts' folder preserved.");
            }
            else
            {
                Console.WriteLine("[*] No old files to delete.");
            }
        }

        private static void SaveBuildInfo(string version)
        {
            File.WriteAllText(VersionFilePath, version);
            Console.WriteLine($"[*] Build version {version} saved successfully.");
        }

        private static (string, string, string) GetLatestReleaseInfo()
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

                var changelogStart = json.IndexOf("\"body\":\"") + 8;
                var changelogEnd = json.IndexOf("\"", changelogStart);
                var changelog = changelogStart > 7 ? json.Substring(changelogStart, changelogEnd - changelogStart) : null;

                Console.WriteLine($"[*] Latest version found: {tagName}");
                return (tagName, zipUrl, changelog);
            }
        }

        private static bool IsUpToDate(string latestVersion)
        {
            if (!File.Exists(VersionFilePath)) return false;
            var currentVersion = File.ReadAllText(VersionFilePath).Trim();
            return currentVersion == latestVersion;
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
                PrintError("Error: 'RezWareUi.exe' not found.");
            }

            _downloadTaskCompletionSource.SetResult(true);
        }

        private static string latestVersion;

        private static void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {message}");
            Console.ResetColor();
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
                var changelog = releaseInfo.Item3;

                if (!IsUpToDate(latestVersion))
                {
                    Console.WriteLine($"[*] New version available: RezWare {latestVersion}. Updating...");
                    if (!string.IsNullOrEmpty(changelog))
                    {
                        Console.WriteLine("[+] ChangeLog for this version:");
                        Console.WriteLine($"    {changelog}");
                    }

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
                PrintError($"Error checking for updates: {ex.Message}");
            }

            Console.WriteLine("\n[!] The application will close in 3 seconds...");

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
                PrintError("Error: 'RezWareUi.exe' not found.");
            }
        }
    }
}
