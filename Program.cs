using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;

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

        private static void ShowBanner()
        {
            Console.WriteLine(@"__________                __      __                       
\______   \ ____ ________/  \    /  \_____ _______   ____  
 |       _// __ \\___   /\   \/\/   /\__  \\_  __ \_/ __ \ 
 |    |   \  ___/ /    /  \        /  / __ \|  | \/\  ___/ 
 |____|_  /\___  >_____ \  \__/\  /  (____  /__|    \___  >
        \/     \/      \/       \/        \/            \/   ");
        }

        private static void DeleteOldFiles()
        {
            if (Directory.Exists(InstallDirectory))
            {
                Directory.Delete(InstallDirectory, true);
                Console.WriteLine("[INFO] Old files deleted.");
            }
        }

        private static void SaveBuildInfo(string version)
        {
            File.WriteAllText(VersionFilePath, version);
            Console.WriteLine($"[INFO] Updated build version saved: {version}");
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
                Console.WriteLine($"[INFO] Latest version found on GitHub: {tagName}");
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
                Console.WriteLine("[INFO] Additional files already downloaded.");
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
                return;
            }

            Console.WriteLine("\n[INFO] Additional files downloaded. Extracting...");
            ZipFile.ExtractToDirectory(AdditionalZipLocation, CurrentDirectory);
            File.Delete(AdditionalZipLocation);
            Console.WriteLine("[INFO] Additional files installation complete.");

            string binDirectory = Path.Combine(CurrentDirectory, "bin");
            if (Directory.Exists(binDirectory))
            {
                Console.WriteLine("[INFO] bin folder exists after extraction.");
            }
            else
            {
                PrintError("[WARNING] bin folder does not exist after extraction.");
            }
        }

        private static void CreateRequiredDirectories()
        {
            var workspaceDirectory = Path.Combine(CurrentDirectory, "workspace");
            if (!Directory.Exists(workspaceDirectory))
            {
                Directory.CreateDirectory(workspaceDirectory);
                Console.WriteLine("[INFO] Created 'workspace' directory.");
            }
        }

        private static void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Write($"\r[INFO] Downloading... {e.ProgressPercentage}% ({e.BytesReceived}/{e.TotalBytesToReceive} bytes)");
        }

        private static void Wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                PrintError($"Download error: {e.Error?.Message}");
                return;
            }

            Console.WriteLine("\n[INFO] Download complete. Extracting files...");
            ZipFile.ExtractToDirectory(ZipLocation, InstallDirectory);
            SaveBuildInfo(latestVersion);
            File.Delete(ZipLocation);
            Console.WriteLine("[INFO] Installation complete. Launching application...");

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

        private static void Main(string[] args)
        {
            Console.Title = "RezWare Bootstrapper";
            ShowBanner();
            Console.WriteLine("[INFO] Checking for updates...");

            try
            {
                var releaseInfo = GetLatestReleaseInfo();
                latestVersion = releaseInfo.Item1;
                var zipUrl = releaseInfo.Item2;

                if (!IsUpToDate(latestVersion))
                {
                    Console.WriteLine($"[INFO] New update available: RezWare {latestVersion}. Updating...");
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
                    Console.WriteLine("[INFO] You are already using the latest version.");
                    if (File.Exists(ExecutablePath))
                    {
                        System.Diagnostics.Process.Start(ExecutablePath);
                    }
                    else
                    {
                        PrintError("RezWare.exe not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
            }

            CreateRequiredDirectories();
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            Environment.Exit(0);
        }
    }
}
