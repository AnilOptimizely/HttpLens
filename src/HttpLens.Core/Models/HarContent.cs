using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpLens.Core.Models
{
    sealed class HarContent
    {
        public long Size { get; set; }
        public string MimeType { get; set; } = "";
        public string? Text { get; set; }
    }
}
