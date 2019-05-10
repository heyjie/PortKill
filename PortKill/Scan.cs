using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace PortKill
{
    internal class Scan
    {
        public string type { get; set; }
        public string source { get; set; }
        public string dest { get; set; }
        public TCP_CONNECTION_STATE state { get; set; }
        public int owningPid { get; set; }
        public string ProcessName { get; set; }
        public Icon ProcessIcon { get; set; }

        public Scan(string type, string source, string dest, TCP_CONNECTION_STATE state, int owningPid,string ProcessName)
        {
            this.type = type;
            this.source = source;
            this.dest = dest;
            this.state = state;
            this.owningPid = owningPid;
            this.ProcessName = ProcessName;
        }
    }
}
