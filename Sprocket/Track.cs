using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sprocket
{
    enum TrackType
    {
        Video, Audio
    }
    class Track
    {
        public Int32 Number { get; set; }
        public TrackType Type { get; set; }
        public String Codec { get; set; }
    }
}
