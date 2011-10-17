using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.ComponentModel;
using System.Runtime.Remoting.Messaging;
using System.Diagnostics;

namespace MKV2MP4
{
    abstract class ExternalProcess
    {
        public abstract String Name { get; }
        protected String ProgramPath;
        protected bool _isRunning;
        public bool IsRunning { get { return _isRunning; } }
        protected AsyncContext _context = null;
        public AsyncOperation async;
        protected Int32 Progress = 0;
        protected readonly object _sync = new object();
        public event AsyncCompletedEventHandler TaskCompleted;
        protected bool _cancelling;

        protected void RunExternalProcess(String Args, AsyncContext Context, out bool Cancelled, DataReceivedEventHandler DataHandler)
        {
            Cancelled = false;
            ProcessStartInfo StartInfo = new ProcessStartInfo();
            StartInfo.CreateNoWindow = true;
            StartInfo.UseShellExecute = false;
            StartInfo.FileName = ProgramPath;
            StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            StartInfo.Arguments = Args.ToString();
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;

            try
            {
                Process ExtProcess = Process.Start(StartInfo);
                ExtProcess.OutputDataReceived += DataHandler;
                ExtProcess.ErrorDataReceived += DataHandler;
//                ExtProcess.ErrorDataReceived += new DataReceivedEventHandler(ExtProcess_ErrorDataReceived);
                ExtProcess.BeginErrorReadLine();
                ExtProcess.BeginOutputReadLine();
                while (!ExtProcess.HasExited)
                {
                    if (Context.IsCancelling)
                    {
                        ExtProcess.Kill();
                        ExtProcess.WaitForExit();
                        Cancelled = true;
                        return;
                    }
                    Thread.Sleep(250);
                }

                ExtProcess.WaitForExit();
            }
            catch (Exception E)
            {
                throw E;
//                Console.WriteLine(E.Message);
//                Environment.Exit(0);
            }
        }

        void ExtProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        protected virtual void OnTaskCompleted(AsyncCompletedEventArgs e)
        {
            if (TaskCompleted != null)
                TaskCompleted(this, e);
        }

        protected abstract void TaskCompletedSpecific(IAsyncResult ar, out bool Cancelled);
        protected void TaskCompletedCallback(IAsyncResult ar)
        {
            bool Cancelled;
            AsyncOperation async = (AsyncOperation)ar.AsyncState;
            TaskCompletedSpecific(ar, out Cancelled);

            // clear the running task flag
            lock (_sync)
            {
                _isRunning = false;
                _context = null;
            }

            // raise the completed event
            AsyncCompletedEventArgs completedArgs = new AsyncCompletedEventArgs(null,
              Cancelled, null);
            async.PostOperationCompleted(
              delegate(object e) { OnTaskCompleted((AsyncCompletedEventArgs)e); },
              completedArgs);
        }

        public void CancelAsync()
        {
            lock (_sync)
            {
                _cancelling = true;
                if (_context != null)
                {
                    _context.Cancel();
                }
            }
        }

        public event EventHandler<ExternalProcessProgressChangedEventArgs> TaskProgressChanged;

        protected virtual void OnTaskProgressChanged(ExternalProcessProgressChangedEventArgs e)
        {
            if (TaskProgressChanged != null)
                TaskProgressChanged(this, e);
        }
    }
}
