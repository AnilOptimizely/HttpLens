using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpLens.Core.Models
{
    sealed class HarResponse
    {
        public int Status { get; set; }
        public string StatusText { get; set; } = "";
        public string HttpVersion { get; set; } = "";
        public List<HarNameValue> Headers { get; set; } = [];
        public HarContent Content { get; set; } = new();
        public long BodySize { get; set; }
    }
}
