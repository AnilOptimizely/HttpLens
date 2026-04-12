using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpLens.Core.Models
{
    sealed class HarLog
    {
        public string Version { get; set; } = "1.2";
        public HarCreator Creator { get; set; } = new();
        public List<HarEntry> Entries { get; set; } = [];
    }
}
