using System;
using System.Collections.Generic;
using System.Text;

namespace Conductor_Shared
{
    public class WorkPackage
    {
        public WorkType WorkType { get; set; }
        public Guid GUID { get; set; }
        public List<File> InFiles { get; set; }


        public Version Version { get; set; }
    }
}
