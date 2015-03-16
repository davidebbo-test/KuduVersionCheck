using System;
using System.Collections.Generic;

namespace KuduVersionCheck.Models
{
    public class StampEntry
    {
        public string Name { get; set; }
        public string Environment { get; set; }
        public bool Mismatch { get; set; }
        public string TestSiteUrl { get; set; }
        public IDictionary<string, StampCell> Data { get; set; }
        public string ConsoleUrl { get; set; }
        public TimeSpan Duration { get; set; }
        public string Style { get; set; }
    }
}