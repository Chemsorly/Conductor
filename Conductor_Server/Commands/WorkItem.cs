using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Conductor_Shared;
using Conductor_Shared.Enums;
using Version = Conductor_Shared.Version;

namespace Conductor_Server.Commands
{
    public class WorkItem
    {
        public delegate void OnTimeoutHappened(WorkItem sender);

        public event OnTimeoutHappened OnTimeoutHappenedEvent;

        public WorkPackage WorkPackage { get; private set; }
        public WorkType WorkType {get;}
        public Guid WorkItemID { get; }
        public String Parameters { get; }
        public String ClientID { get; private set; }
        public DateTime StartDate { get; private set; }
        public List<File> Files { get; private set; }
        public Version Version { get; set; }

        private System.Timers.Timer _timeoutTimer;
        private int TimeoutValue => WorkType == WorkType.Training ? 302400000 : 1200000; 
        //604.800.000 = 7d, 86.400.000s = 24h; 43.200.000s = 12h; 1.200.000s = 20m

        public WorkItem(Version pVersion, List<File> pFiles, WorkType pWorkType, Guid pGuid)
        {
            //specify default values
            this.Version = pVersion;
            this.WorkType = pWorkType;
            StartDate = DateTime.MinValue;
            WorkItemID = pGuid;
            Files = pFiles;
        }

        public WorkPackage Start(OSEnum pOS, String pAssignedClient)
        {
            StartDate = DateTime.UtcNow;
            ClientID = pAssignedClient;

            _timeoutTimer = new System.Timers.Timer();
            _timeoutTimer.Interval = TimeoutValue;
            _timeoutTimer.Elapsed += (sender, args) =>
            {
                _timeoutTimer.Stop();
                OnTimeoutHappenedEvent?.Invoke(this);
                StartDate = DateTime.MinValue;
                ClientID = String.Empty;
                WorkPackage = null;
                _timeoutTimer = null;
            };
            _timeoutTimer.Start();

            return new WorkPackage()
            {
                GUID = this.WorkItemID,
                WorkType = this.WorkType,
                Version = this.Version,
                InFiles = this.Files                
            };
        }

        public void Finish()
        {
            this._timeoutTimer.Stop();
        }

    }
}
