using Conductor_Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Conductor_Shared
{
    public class WorkPackage
    {
        public WorkPackageType WorkType { get; set; }
        public Guid GUID { get; set; }
        public List<File> InFiles { get; set; }


        public Version Version { get; set; }
    }
}
