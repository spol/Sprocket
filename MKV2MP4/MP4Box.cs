using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using System.Threading;

namespace MKV2MP4
{
    class MP4Box : ExternalProcess
    {
        public override String Name { get { return "Muxing"; } }
        private string CurrentTaskName = null;
        private int CurrentTask = 0;
        private int TotalTracks = 0;

        public MP4Box(String Path)
        {
            ProgramPath = Path;
        }

        protected override void TaskCompletedSpecific(IAsyncResult ar, out bool Cancelled)
        {
            // get the original worker delegate and the AsyncOperation instance
            CombineWorkerDelegate worker =
              (CombineWorkerDelegate)((AsyncResult)ar).AsyncDelegate;

            // finish the asynchronous operation
            worker.EndInvoke(out Cancelled, ar);
        }

        public void CombineAsync(List<String> Files, String OutputFile)
        {
            CombineWorkerDelegate worker = new CombineWorkerDelegate(CombineWorker);
            AsyncCallback completedCallback = new AsyncCallback(TaskCompletedCallback);

            lock (_sync)
            {
                if (_isRunning)
                    throw new InvalidOperationException("The control is currently busy.");

                async = AsyncOperationManager.CreateOperation(null);
                AsyncContext Context = new AsyncContext();
                bool Cancelled;
                worker.BeginInvoke(Files, OutputFile, Context, out Cancelled, completedCallback, async);
                _context = Context;
                _isRunning = true;
            }
        }

        private delegate void CombineWorkerDelegate(List<String> Files, String OutputFile, AsyncContext context, out bool Cancelled);
        public void CombineWorker(List<String> Files, String OutputFile, AsyncContext Context, out bool Cancelled)
        {
            StringBuilder Args = new StringBuilder();
            TotalTracks = Files.Count;
            foreach (String File in Files)
            {
                Args.Append("-add \"" + File + "\" ");
            }
            Args.Append("\"" + OutputFile + "\"");

            RunExternalProcess(Args.ToString(), Context, out Cancelled, new DataReceivedEventHandler(MP4BoxProcess_OutputDataReceived));
        }

        private void MP4BoxProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && e.Data.Length > 0)
            {
                if (e.Data.StartsWith("ISO File Writing:") || e.Data.StartsWith("Importing"))
                {
                    Int32 NewProgress;
                    String TaskName;
                    if (e.Data.StartsWith("Importing"))
                    {
                        Regex R = new Regex(@"^Importing (.*):.*[^\d](\d+)/100");
                        Match M = R.Match(e.Data);
                        NewProgress = Convert.ToInt32(M.Groups[2].Value);
                        String NewTask = M.Groups[1].Value;
                        if (CurrentTaskName != NewTask)
                        {
                            CurrentTaskName = NewTask;
                            CurrentTask++;
                        }

                        TaskName = "Muxing";
                        String TaskDesc = String.Format("Stream {0}/{1}", CurrentTask, TotalTracks);

                        if (NewProgress != Progress && !_cancelling)
                        {
                            Progress = NewProgress;
                            // raise the progress changed event
                            ExternalProcessProgressChangedEventArgs eArgs = new ExternalProcessProgressChangedEventArgs(
                              Progress, CurrentTask, TotalTracks, TaskName, TaskDesc, null);
                            async.Post(delegate(object ea)
                            { OnTaskProgressChanged((ExternalProcessProgressChangedEventArgs)ea); },
                              eArgs);
                        }
                    }
                    else
                    {
                        Regex R = new Regex(@"(\d+)/100");
                        Match M = R.Match(e.Data);
                        NewProgress = Convert.ToInt32(M.Groups[1].Value);

                        if (NewProgress != Progress && !_cancelling)
                        {
                            Progress = NewProgress;
                            // raise the progress changed event
                            ExternalProcessProgressChangedEventArgs eArgs = new ExternalProcessProgressChangedEventArgs(
                              Progress, 1, 1, "Writing", null, null);
                            async.Post(delegate(object ea)
                            { OnTaskProgressChanged((ExternalProcessProgressChangedEventArgs)ea); },
                              eArgs);
                        }
                    }

                    //Console.Write(e.Data);
                    //Console.CursorLeft = 0;
                }
                else
                {
//                    Console.WriteLine(e.Data);
                }
            }
        }
    }
}
