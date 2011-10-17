using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommandLine;

namespace MKV2MP4
{
    class Options
    {
        [Option(null, "add-aac",
                HelpText = "Create a two channel aac audio track if one doesn't exist.")]
        public bool AddAAC = false;

        [ValueList(typeof(List<string>))]
        public IList<string> SourceFiles = null;
    }
}
