using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Updater code based on HDTUpdate from Hearthstone Deck Tracker

namespace Updater
{
    class Program
    {
        public const string title = "Arena Helper Updater";
        public const string tempdir = "temp";
        public const string plugindir = "temp\\ArenaHelper";
        public const string targetdir = "Plugins\\ArenaHelper";
        public const string UpdaterFileName = "Updater.exe";
        public const string UpdaterFileNameNew = "Updater_new.exe";
        public const string userAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)";

        static void Main(string[] args)
        {
            Console.Title = title;
            Console.CursorVisible = false;

            if (args.Length != 2)
            {
                Console.WriteLine("Invalid arguments");
                return;
            }

            try
            {
                // Wait for HDT to shut down
                Thread.Sleep(1000);

                int procid = int.Parse(args[0]);
                if (Process.GetProcesses().Any(p => p.Id == procid))
                {
                    Process.GetProcessById(procid).Kill();
                    Console.WriteLine("Killed Hearthstone Deck Tracker process");
                }
            }
            catch
            {
                return;
            }

            Task update = Update(args[1]);
            update.Wait();
        }

        private static async Task Update(string url)
        {
            try
            {
                // Get filename of url
                string filename = "update.zip";
                Uri uri = new Uri(url);
                if (uri.IsFile)
                {
                    filename = System.IO.Path.GetFileName(uri.LocalPath);
                }
                string filepath = Path.Combine(tempdir, filename);

                // Make sure temp directory exists
                Console.WriteLine("Creating temporary directory");
                if (Directory.Exists(tempdir))
                    Directory.Delete(tempdir, true);
                Directory.CreateDirectory(tempdir);

                // Download the file
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("user-agent", userAgent);

                    var lockobj = new object();
                    Console.WriteLine("Downloading latest version... 0%");
                    wc.DownloadProgressChanged += (sender, e) =>
                    {
                        lock (lockobj)
                        {
                            Console.CursorLeft = 0;
                            Console.CursorTop = 1;
                            Console.WriteLine("Downloading latest version... {0}/{1}KB ({2}%)", e.BytesReceived / (1024), e.TotalBytesToReceive / (1024), e.ProgressPercentage);
                        }
                    };
                    await wc.DownloadFileTaskAsync(url, filepath);
                }

                // Extract file
                Console.WriteLine("Extracting files...");
                ZipFile.ExtractToDirectory(filepath, tempdir);

                // Copy files
                CopyFiles(tempdir, plugindir, targetdir);

                // Start HDT
                Process.Start("Hearthstone Deck Tracker.exe");
            }
            catch
            {
                Console.WriteLine("There was a problem while auto-updating. Press any key to go to the project page and update manually.");
                Console.ReadKey();
                Process.Start(@"https://github.com/rembound/Arena-Helper");
            }
            finally
            {
                try
                {
                    // Delete temp directory
                    if (Directory.Exists(tempdir))
                        Directory.Delete(tempdir, true);

                    Console.WriteLine("Update installed successfully!");
                }
                catch
                {
                    Console.WriteLine("Could not delete temporary directory");
                }
            }
        }

        private static void CopyFiles(string dir, string newpath, string targetpath)
        {
            foreach (var subdir in Directory.GetDirectories(dir))
            {
                foreach (var file in Directory.GetFiles(subdir))
                {
                    var newdir = subdir.Replace(newpath, targetpath);
                    if (!Directory.Exists(newdir))
                        Directory.CreateDirectory(newdir);

                    // Write file
                    var newfilepath = file.Replace(newpath, targetpath);
                    Console.WriteLine("Writing {0}", newfilepath);
                    if (file.Contains(UpdaterFileName))
                        newfilepath = newfilepath.Replace(UpdaterFileName, UpdaterFileNameNew);
                    File.Copy(file, newfilepath, true);
                }

                // Recurse into subdir
                CopyFiles(subdir, newpath, targetpath);
            }
        }
    }
}
