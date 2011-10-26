using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sprocket
{
    internal class AsyncContext
    {
      private readonly object _sync = new object();
      private bool _isCancelling = false;

      public bool IsCancelling
      {
        get
        {
          lock (_sync) { return _isCancelling; }
        }
      }

      public void Cancel()
      {
        lock (_sync) { _isCancelling = true; }
      }
    }
}
