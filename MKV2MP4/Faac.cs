using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.Remoting.Messaging;
using System.ComponentModel;

namespace MKV2MP4
{
    class Faac : ExternalProcess
    {
        public override String Name { get { return "Encoding"; } }

        public Faac(String Path)
        {
            ProgramPath = Path;
        }

        protected override void TaskCompletedSpecific(IAsyncResult ar, out bool Cancelled)
        {
            // get the original worker delegate and the AsyncOperation instance
            EncodeWorkerDelegate worker =
              (EncodeWorkerDelegate)((AsyncResult)ar).AsyncDelegate;

            // finish the asynchronous operation
            worker.EndInvoke(out Cancelled, ar);
        }

        public void EncodeAsync(String SourceFile, String DestinationFile)
        {
            EncodeWorkerDelegate worker = new EncodeWorkerDelegate(EncodeWorker);
            AsyncCallback completedCallback = new AsyncCallback(TaskCompletedCallback);

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

        private delegate void EncodeWorkerDelegate(String SourceFile, String DestinationFile, AsyncContext Context, out bool Cancelled);
        public void EncodeWorker(String SourceFile, String DestinationFile, AsyncContext Context, out bool Cancelled)
        {
            String Args = "-o \"" + DestinationFile + "\" \"" + SourceFile + "\"";
            RunExternalProcess(Args, Context, out Cancelled, new DataReceivedEventHandler(FaacProcess_OutputDataReceived));
 
        }

        private void FaacProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && e.Data.Length > 0)
            {
                Regex R = new Regex(@"\(\s*(\d+)%\)");
                Match M = R.Match(e.Data);
                if (M.Success)
                {
                    int NewProgress = Convert.ToInt32(M.Groups[1].Value);

                    if (NewProgress != Progress && !_cancelling)
                    {
                        Progress = NewProgress;
                        // raise the progress changed event
                        ExternalProcessProgressChangedEventArgs eArgs = new ExternalProcessProgressChangedEventArgs(
                          Progress, 1, 1, "Encoding", null, null);
                        async.Post(delegate(object ea)
                        { OnTaskProgressChanged((ExternalProcessProgressChangedEventArgs)ea); },
                          eArgs);
                    }
                    //Console.CursorLeft = 0;
                    //Console.Write(e.Data);
                }
                else
                {
                    //Console.WriteLine(e.Data);
                }
            }
        }

    }
}
