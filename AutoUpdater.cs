using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace ArenaHelper
{
    class AutoUpdater
    {
        private static string TempDir = Path.Combine(Config.AppDataPath, "ArenaHelperTemp");
        private static string TempPluginDir = Path.Combine(Config.AppDataPath, "ArenaHelperTemp", "ArenaHelper");
        private static string TargetPluginDir = Path.Combine(Config.AppDataPath, "Plugins", "ArenaHelper");

        public const string userAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)";

        public static async Task<bool> Update(string url)
        {
            bool status = true;
            try
            {
                // Get filename of url
                string filename = "update.zip";
                Uri uri = new Uri(url);
                if (uri.IsFile)
                {
                    filename = System.IO.Path.GetFileName(uri.LocalPath);
                }
                string filepath = Path.Combine(TempDir, filename);

                // Make sure temp directory exists
                Log.Info("Creating temporary directory");
                if (Directory.Exists(TempDir))
                    Directory.Delete(TempDir, true);
                Directory.CreateDirectory(TempDir);

                // Download the file
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("user-agent", userAgent);

                    var lockobj = new object();
                    Log.Info("Downloading latest version of Arena Helper... ");
                    await wc.DownloadFileTaskAsync(url, filepath);
                }

                // Extract file
                Log.Info("Extracting files...");
                ZipFile.ExtractToDirectory(filepath, TempDir);

                // Copy files
                CopyFiles(TempDir, TempPluginDir, TargetPluginDir);
            }
            catch
            {
                status = false;
            }
            finally
            {
                try
                {
                    // Delete temp directory
                    if (Directory.Exists(TempDir))
                        Directory.Delete(TempDir, true);

                    Log.Info("Update installed successfully!");
                }
                catch
                {
                    Log.Info("Could not delete temporary directory");
                }
            }
            return status;
        }

        private static void CopyFiles(string dir, string newpath, string targetpath)
        {
            Log.Info("CopyFiles dir: " + dir);
            Log.Info("CopyFiles newpath: " + newpath);
            Log.Info("CopyFiles targetpath: " + targetpath);
            foreach (var subdir in Directory.GetDirectories(dir))
            {
                var newdir = subdir.Replace(newpath, targetpath);
                if (!Directory.Exists(newdir))
                    Directory.CreateDirectory(newdir);

                foreach (var file in Directory.GetFiles(subdir))
                {
                    // Write file
                    var newfilepath = file.Replace(newpath, targetpath);
                    Log.Info("Writing " + newfilepath);
                    File.Copy(file, newfilepath, true);
                }

                // Recurse into subdir
                CopyFiles(subdir, newpath, targetpath);
            }
        }
    }
}
