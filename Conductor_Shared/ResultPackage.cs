using System;
using System.Collections.Generic;
using System.Text;

namespace Conductor_Shared
{
    public abstract class ResultPackage
    {
        public WorkPackage InWorkPackage { get; set; }
        public TimeSpan DurationTime { get; set; }
        public ClientStatus ClientStatusAtEnd { get; set; }
        public MachineData MachineData { get; set; }
        public List<String> OutLog { get; set; }
        public List<File> ResultFiles { get; set; }
        public DateTime FinishTimestamp { get; set; }
    }
}
