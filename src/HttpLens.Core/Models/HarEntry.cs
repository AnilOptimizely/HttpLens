using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpLens.Core.Models
{
    sealed class HarEntry
    {
        public string StartedDateTime { get; set; } = "";
        public double Time { get; set; }
        public HarRequest Request { get; set; } = new();
        public HarResponse Response { get; set; } = new();
        public HarCache Cache { get; set; } = new();
        public HarTimings Timings { get; set; } = new();
    }
}
