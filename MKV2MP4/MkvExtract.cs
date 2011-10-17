using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.ComponentModel;
using System.Runtime.Remoting.Messaging;

namespace MKV2MP4
{
    class MkvExtract : ExternalProcess
    {
        public List<String> Files;
        public override String Name { get { return "Extracting"; } }

        public MkvExtract(String Path)
        {
            ProgramPath = Path;
        }

        protected override void TaskCompletedSpecific(IAsyncResult ar, out bool Cancelled)
        {
            // get the original worker delegate and the AsyncOperation instance
            ExtractTracksWorkerDelegate worker =
              (ExtractTracksWorkerDelegate)((AsyncResult)ar).AsyncDelegate;

            // finish the asynchronous operation
            worker.EndInvoke(out Cancelled, ar);
        }

        public void ExtractTracksAsync(String SourceVideo, String ExtractPath, List<Track> Tracks)
        {
            ExtractTracksWorkerDelegate worker = new ExtractTracksWorkerDelegate(ExtractTracksWorker);
            AsyncCallback completedCallback = new AsyncCallback(TaskCompletedCallback);

            lock (_sync)
            {
                if (_isRunning)
                    throw new InvalidOperationException("The control is currently busy.");

                async = AsyncOperationManager.CreateOperation(null);
                AsyncContext Context = new AsyncContext();
                bool Cancelled;
                worker.BeginInvoke(SourceVideo, ExtractPath, Tracks, Context, out Cancelled, completedCallback, async);
                _context = Context;
                _isRunning = true;
            }
        }

        private delegate void ExtractTracksWorkerDelegate(String SourceVideo, String ExtractPath, List<Track> Tracks, AsyncContext Context, out bool Cancelled);

        private void ExtractTracksWorker(String SourceVideo, String ExtractPath, List<Track> Tracks, AsyncContext Context, out bool Cancelled)
        {
            StringBuilder Args = new StringBuilder();
            Args.Append("tracks " + SourceVideo + " ");
            Files = new List<string>();
            foreach (Track T in Tracks)
            {
                String Extension;
                switch (T.Codec)
                {
                    case "A_AC3":
                        Extension = "ac3";
                        break;
                    case "V_MPEG4/ISO/AVC":
                        Extension = "h264";
                        break;
                    default:
                        Extension = "track";
                        break;
                }
                String FileName = ExtractPath + "\\" + T.Number.ToString() + "." + Extension;
                Args.Append(T.Number.ToString() + ":" + "\"" + FileName + "\"" + " ");
                Files.Add(FileName);
            }

            RunExternalProcess(Args.ToString(), Context, out Cancelled, new DataReceivedEventHandler(MkvExtractProcess_OutputDataReceived));
        }

        private void MkvExtractProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && e.Data.Length > 0)
            {
                if (e.Data.StartsWith("Progress: "))
                {
                    Regex R = new Regex(@"(\d+)%");
                    Match M = R.Match(e.Data);
                    Int32 NewProgress = Convert.ToInt32(M.Groups[1].Value);

                    if (NewProgress != Progress && !_cancelling)
                    {
                        Progress = NewProgress;
                        // raise the progress changed event
                        ExternalProcessProgressChangedEventArgs eArgs = new ExternalProcessProgressChangedEventArgs(
                          Progress, 1, 1, "Extracting", null, null);
                        async.Post(delegate(object ea)
                        { OnTaskProgressChanged((ExternalProcessProgressChangedEventArgs)ea); },
                          eArgs);
                    }
                }
                else
                {
//                    Console.WriteLine(e.Data);
                }
            }
        }

    }
}
