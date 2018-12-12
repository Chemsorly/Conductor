using System;
using System.Collections.Generic;
using System.Text;

namespace Conductor_Shared
{
    public class ConductorStatus
    {
        /// <summary>
        /// currently connected nodes
        /// </summary>
        //[Range(0,int.MaxValue)]
        public int ConnectedNodes { get; set; }
    }
}
