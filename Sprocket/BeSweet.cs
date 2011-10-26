using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.Remoting.Messaging;
using System.ComponentModel;

namespace Sprocket
{
    class BeSweet : ExternalProcess
    {
        public override String Name { get { return "Decoding"; } }
        public BeSweet(String Path)
        {
            ProgramPath = Path;
        }
        private TimeSpan Duration { get; set; }

        protected override void TaskCompletedSpecific(IAsyncResult ar, out bool Cancelled)
        {
            // get the original worker delegate and the AsyncOperation instance
            DecodeWorkerDelegate worker =
              (DecodeWorkerDelegate)((AsyncResult)ar).AsyncDelegate;

            // finish the asynchronous operation
            worker.EndInvoke(out Cancelled, ar);
        }

        public void DecodeAsync(String SourceFile, String DestinationFile, TimeSpan Duration)
        {
            DecodeWorkerDelegate worker = new DecodeWorkerDelegate(DecodeWorker);
            AsyncCallback completedCallback = new AsyncCallback(TaskCompletedCallback);

            this.Duration = Duration;

            lock (_sync)
            {
                if (_isRunning)
                    throw new InvalidOperationException("The control is currently busy.");

                async = AsyncOperationManager.CreateOperation(null);
                AsyncContext Context = new AsyncContext();
                bool Cancelled;
                worker.BeginInvoke(SourceFile, DestinationFile, Context, out Cancelled, completedCallback, async);
                _context = Context;
                _isRunning = true;
            }
        }

        private delegate void DecodeWorkerDelegate(String SourceFile, String DestinationFile, AsyncContext context, out bool Cancelled);
        public void DecodeWorker(String SourceFile, String DestinationFile, AsyncContext Context, out bool Cancelled)
        {
            String Args = "-core( -input \"" + SourceFile + "\" -2ch -output \"" + DestinationFile + "\" )";

            RunExternalProcess(Args, Context, out Cancelled, new DataReceivedEventHandler(BesweetProcess_OutputDataReceived));
        }

        private void BesweetProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && e.Data.Length > 0)
            {//[00:00:00:000]
                //Regex R = new Regex(@"^\[\d\d:\d\d:\d\d:\d\d\d\]");
                Regex R = new Regex(@"^\[(\d\d:\d\d:\d\d:\d\d\d)\].*transcoding");
                Match M = R.Match(e.Data);
                if (M.Success)
                {
                    Int32 Sep = M.Groups[1].Value.LastIndexOf(':');
                    TimeSpan CurrentPosition = TimeSpan.Parse(M.Groups[1].Value.Remove(Sep, 1).Insert(Sep, "."));
                    Int32 NewProgress = Convert.ToInt32(Math.Round(CurrentPosition.TotalMilliseconds / Duration.TotalMilliseconds * 100));

                    if (NewProgress != Progress && !_cancelling)
                    {
                        Progress = NewProgress;
                        // raise the progress changed event
                        ExternalProcessProgressChangedEventArgs eArgs = new ExternalProcessProgressChangedEventArgs(
                          Progress, 1, 1, "Decoding", null, null);
                        async.Post(delegate(object ea)
                        { OnTaskProgressChanged((ExternalProcessProgressChangedEventArgs)ea); },
                          eArgs);
                    }
                    //Console.CursorLeft = 0;
                    //Console.Write(M.Groups[1].Value);

                }
                else
                {
                    //Console.WriteLine(e.Data);
                }
            }
        }
    }
}
