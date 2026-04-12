namespace HttpLens.Core.Models
{
    sealed class HarRequest
    {
        public string Method { get; set; } = "";
        public string Url { get; set; } = "";
        public string HttpVersion { get; set; } = "";
        public List<HarNameValue> Headers { get; set; } = [];
        public List<HarNameValue> QueryString { get; set; } = [];
        public long BodySize { get; set; }
        public HarPostData? PostData { get; set; }
    }
}
