using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Sprocket
{
    class ExternalProcessProgressChangedEventArgs : ProgressChangedEventArgs
    {
        private int _currentTask;
        private int _totalTasks;
        private string _currentTaskName;
        private string _currentTaskDesc;

        public int CurrentTask { get { return _currentTask; } }
        public int TotalTasks { get { return _totalTasks; } }
        public String CurrentTaskName { get { return _currentTaskName; } }
        public String CurrentTaskDesc { get { return _currentTaskDesc; } }

        public ExternalProcessProgressChangedEventArgs(int ProgressPercentage, int CurrentTask, int TotalTasks, String CurrentTaskName, String CurrentTaskDesc, object UserState)
            : base(ProgressPercentage, UserState)
        {
            _currentTask = CurrentTask;
            _totalTasks = TotalTasks;
            _currentTaskName = CurrentTaskName;
            _currentTaskDesc = CurrentTaskDesc;
        }
    }
}
