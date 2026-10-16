using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoInspect
{
    public class Connection
    {
        public string Protocol { get; set; } = null!;
        public string LocalAddress { get; set; } = null!;
        public string ForeignAddress { get; set; } = null!;
        public string State { get; set; } = null!;
        public int PID { get; set; }
    }
}
