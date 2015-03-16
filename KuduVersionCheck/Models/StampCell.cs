using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KuduVersionCheck.Models
{
    public class StampCell
    {
        public StampCell(string val = "")
        {
            Value = val;
        }

        public string Value { get; set; }
        public string Style { get; set; }

        public override string ToString()
        {
            return Value;
        }
    }
}
