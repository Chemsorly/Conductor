using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Conductor_Server.Utility;
using Conductor_Shared.Enums;

namespace Conductor_Server.Model
{
    public class WorkerClient
    {
        public String ID { get; set; }
        public String MachineName { get; set; }
        public String ContainerVersion { get; set; }
        public OSEnum OperatingSystem { get; set; }
        public ProcessingUnitEnum ProcessingUnit { get; set; }

        //client status parameters
        public bool IsWorking { get; set; }
        public String LastEpochDuration { get; set; }
        public int CurrentEpoch { get; set; }
        public String CurrentWorkParameters { get; set; }
    }
}
