using System;
using System.Collections.Generic;
using System.Text;
using Conductor_Shared.Enums;

namespace Conductor_Shared
{
    public class ClientStatus
    {
        public bool IsWorking { get; set; }
        public String LastEpochDuration { get; set; }
        public int CurrentEpoch { get; set; }
        public String CurrentWorkParameters { get; set; }
    }
}
