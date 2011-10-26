using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommandLine;
using System.IO;
using System.Threading;
using System.ComponentModel;

namespace Sprocket
{
    class Program
    {
        private static ExternalProcess CurrentProcess;
        private static bool Quiting;
        private static ExternalProcessProgressChangedEventArgs LastProgressUpdate = null;
        static void Main(string[] args)
        {
            var options = new Options();
            ICommandLineParser parser = new CommandLineParser(new CommandLineParserSettings(Console.Error));
            if (!parser.ParseArguments(args, options))
                Environment.Exit(1);

            Run(options);

            Environment.Exit(0);
        }
        
        static void Run(Options options)
        {
            List<String> Files = new List<string>();
            foreach (String FilePatterns in options.SourceFiles)
            {
                String Path, Pattern;
                if (FilePatterns.LastIndexOf('\\') > -1)
                {
                    Path = FilePatterns.Substring(0, FilePatterns.LastIndexOf('\\'));
                    Pattern = FilePatterns.Substring(FilePatterns.LastIndexOf('\\')+1);
                }
                else
                {
                    Path = ".";
                    Pattern = FilePatterns;
                }
                Files.AddRange(Directory.GetFiles(Path, Pattern).ToList());
            }
            if (Files.Count == 0)
            {
                Console.Error.WriteLine("No Files match.");
                Environment.Exit(1);
            }
            foreach (String SourceFile in Files)
            {
                Convert(SourceFile, options.AddAAC);
                if (Quiting)
                {
                    return;
                }
            }
        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Console.WriteLine("Cleaning Up...");

            if (CurrentProcess != null && CurrentProcess.IsRunning)
            {
                Console.WriteLine("Cancelling...");
                CurrentProcess.CancelAsync();
            }
            Quiting = true;
        }

        static void Convert(String Filename, Boolean AddAAC)
        {
            FileInfo FI = new FileInfo(Filename);
            String DestinationFile = FI.DirectoryName + "\\" + FI.Name.Substring(0, FI.Name.Length - FI.Extension.Length) + ".mp4";
            MkvInfo Info = new MkvInfo("mkvtoolnix\\mkvinfo.exe");
            Info.Scan(Filename);
            List<Track> Tracks = FilterTracks(Info.Tracks);

            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);

            CurrentProcess = new MkvExtract("mkvtoolnix\\mkvextract.exe");

            CurrentProcess.TaskProgressChanged += new EventHandler<ExternalProcessProgressChangedEventArgs>(CurrentProcess_TaskProgressChanged);
            String TempFolder = GetTempFolder();
            ((MkvExtract)CurrentProcess).ExtractTracksAsync(Filename, TempFolder, Tracks);
            while (CurrentProcess.IsRunning)
            {
                Thread.Sleep(1000);
            }

            if (Quiting)
            {
                CleanUp(TempFolder);
                return;
            }
            List<String> Files = ((MkvExtract)CurrentProcess).Files;
            CurrentProcess = null;

            if (AddAAC && Tracks[1].Codec == "A_AC3")
            {
                CurrentProcess = new BeSweet("besweet\\besweet.exe");
                CurrentProcess.TaskProgressChanged += new EventHandler<ExternalProcessProgressChangedEventArgs>(CurrentProcess_TaskProgressChanged);
                String WavFile = Files[1].Substring(0, Files[1].Length - new FileInfo(Files[1]).Extension.Length) + ".wav";
                ((BeSweet)CurrentProcess).DecodeAsync(Files[1], WavFile, Info.Duration);
                while (CurrentProcess.IsRunning)
                {
                    Thread.Sleep(1000);
                }
                if (Quiting)
                {
                    CleanUp(TempFolder);
                    return;
                }
                CurrentProcess = null;
                FileInfo WavFI = new FileInfo(WavFile);
                String AACFile = WavFile.Substring(0, WavFile.Length - FI.Extension.Length) + ".aac";
                CurrentProcess = new Faac("faac\\faac.exe");
                CurrentProcess.TaskProgressChanged += new EventHandler<ExternalProcessProgressChangedEventArgs>(CurrentProcess_TaskProgressChanged);
                ((Faac)CurrentProcess).EncodeAsync(WavFile, AACFile);
                while (CurrentProcess.IsRunning)
                {
                    Thread.Sleep(1000);
                }
                if (Quiting)
                {
                    CleanUp(TempFolder);
                    return;
                }
                Files.Add(AACFile);
            }
            
            CurrentProcess = new MP4Box("mp4box\\mp4box.exe");
            CurrentProcess.TaskProgressChanged += new EventHandler<ExternalProcessProgressChangedEventArgs>(CurrentProcess_TaskProgressChanged);
            ((MP4Box)CurrentProcess).CombineAsync(Files, DestinationFile);

            while (CurrentProcess.IsRunning)
            {
                Thread.Sleep(1000);
            }

            CleanUp(TempFolder);

        }

        private static void CleanUp(String TempFolder)
        {
            foreach (String TempFile in Directory.GetFiles(TempFolder))
            {
                File.Delete(TempFile);
            }
            Directory.Delete(TempFolder);
        }

        private static string GetTempFolder()
        {
            String TempFolder = Path.GetTempPath() + Path.GetRandomFileName();
            while (Directory.Exists(TempFolder))
            {
                TempFolder = Path.GetTempPath() + Path.GetRandomFileName();
            }
            Directory.CreateDirectory(TempFolder);
            Console.WriteLine(TempFolder);
            return TempFolder;
        }

        static void CurrentProcess_TaskProgressChanged(object sender, ExternalProcessProgressChangedEventArgs e)
        {
            if (LastProgressUpdate != null && (LastProgressUpdate.CurrentTask != e.CurrentTask ||
                LastProgressUpdate.CurrentTaskName != e.CurrentTaskName ||
                LastProgressUpdate.TotalTasks != e.TotalTasks))
            {
                Console.WriteLine();
            }
            if (e.CurrentTaskDesc == null)
            {
                Console.Write("[{0}] Progress: {1}%", e.CurrentTaskName,
                    e.ProgressPercentage);
            }
            else
            {
                Console.Write("[{0}] {2}: {1}%", e.CurrentTaskName,
                    e.ProgressPercentage, e.CurrentTaskDesc);
            }
            Console.CursorLeft = 0;
            LastProgressUpdate = e;
        }

        static List<Track> FilterTracks(List<Track> AllTracks)
        {
            List<Track> VideoTracks = AllTracks.FindAll(T => T.Type == TrackType.Video);
            List<Track> AudioTracks = AllTracks.FindAll(T => T.Type == TrackType.Audio);

            List<Track> ChosenTracks = new List<Track>();
            ChosenTracks.Add(VideoTracks.Find(T => T.Codec == "V_MPEG4/ISO/AVC"));

            Track AT = AudioTracks.Find(T => T.Codec == "A_AC3");

            if (AT != null)
            {
                ChosenTracks.Add(AT);
            }
            else
            {
                // TODO: find AAC Tracks.
            }

            return ChosenTracks;
        }


    }
}
