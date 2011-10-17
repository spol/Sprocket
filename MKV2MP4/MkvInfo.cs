using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MKV2MP4
{
    class MkvInfo
    {
        private List<String> MkvInfoData = new List<string>();
        private String MkvInfoPath;
        public List<Track> Tracks { get; set; }
        public TimeSpan Duration;

        public MkvInfo(String Path)
        {
            MkvInfoPath = Path;
        }

        public void Reset()
        {
            Tracks = null;
            Duration = TimeSpan.MinValue;
        }

        public void Scan(String SourceVideo)
        {
            ProcessStartInfo StartInfo = new ProcessStartInfo();
            StartInfo.CreateNoWindow = true;
            StartInfo.UseShellExecute = false;
            StartInfo.FileName = MkvInfoPath;
            StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            StartInfo.Arguments = SourceVideo;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;

            try
            {
                Process MkvInfoProcess = Process.Start(StartInfo);

                MkvInfoProcess.OutputDataReceived += new DataReceivedEventHandler(MkvExtractProcess_OutputDataReceived);
                MkvInfoProcess.ErrorDataReceived += new DataReceivedEventHandler(MkvExtractProcess_ErrorDataReceived);
                MkvInfoProcess.BeginErrorReadLine();
                MkvInfoProcess.BeginOutputReadLine();
                MkvInfoProcess.WaitForExit();
            }
            catch (Exception E)
            {
                Console.WriteLine(E.Message);
                Environment.Exit(0);
            }

            Tracks = new List<Track>();
            Track CurrentTrack = null;
            foreach (String Line in MkvInfoData)
            {
                if (Line.StartsWith("| + Duration:"))
                {
                    Regex R = new Regex(@"\((\d\d:\d\d:\d\d\.\d\d\d)\)");
                    Match M = R.Match(Line);
                    if (M.Success)
                    {
                        Duration = TimeSpan.Parse(M.Groups[1].Value);
                    }
                }
                if (Line == "| + A track")
                {
                    if (CurrentTrack != null)
                    {
                        Tracks.Add(CurrentTrack);
                    }
                    CurrentTrack = new Track();
                }
                if (Line.StartsWith("|  + Track number: "))
                {
                    CurrentTrack.Number = Convert.ToInt32(Line.Substring(19));
                }
                if (Line.StartsWith("|  + Track type: "))
                {
                    if (Line.Substring(17) == "video")
                    {
                        CurrentTrack.Type = TrackType.Video;
                    }
                    if (Line.Substring(17) == "audio")
                    {
                        CurrentTrack.Type = TrackType.Audio;
                    }
                }
                if (Line.StartsWith("|  + Codec ID: "))
                {
                    CurrentTrack.Codec = Line.Substring(15);
                }
            }
            if (CurrentTrack != null)
            {
                Tracks.Add(CurrentTrack);
            }
//            Console.WriteLine(Tracks.Count.ToString() + " Tracks Found");
 
        }

        private void MkvExtractProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            //Console.WriteLine(e.Data);
        }

        private void MkvExtractProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && e.Data.Length > 0)
            {
                MkvInfoData.Add(e.Data);
            }
        }
    }
}
