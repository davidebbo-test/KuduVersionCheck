using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace KuduVersionCheck.Models
{
    public class StampEntriesViewModel
    {
        public IEnumerable<string> Columns { get; set; }
        public IEnumerable<IGrouping<string, StampEntry>> Groups { get; set; }
        public bool ShowConsole { get; set; }
    }
}