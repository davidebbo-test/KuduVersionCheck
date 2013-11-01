using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace KuduVersionCheck.Models
{
    [DataContract]
    public class StampEntry
    {
        public string Name { get; set; }

        public string TestSite { get; set; }

        [DataMember(Name = "commit_id")]
        public string CommitId { get; set; }

        [DataMember(Name = "kudu_version")]
        public string KuduVersion { get; set; }

        [DataMember(Name = "stamp_version")]
        public string StampVersion { get; set; }

        [DataMember(Name = "install_time")]
        public DateTime InstallTime { get; set; }
    }
}