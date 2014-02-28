using System.Collections.Generic;

namespace KuduVersionCheck.Models
{
    public class StampEntry
    {
        public string Name { get; set; }
        public string TestSiteUrl { get; set; }
        public IDictionary<string, string> Data { get; set; }
        public string ConsoleUrl { get; set; }
    }
}