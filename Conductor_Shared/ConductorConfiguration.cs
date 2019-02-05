using MoreLinq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Conductor_Shared
{
    public class ConductorConfiguration
    {
        /// <summary>
        /// timestamp the configuration has been created
        /// gets created once
        /// </summary>
        public DateTime Created { get; set; } = DateTime.Now;

        /// <summary>
        /// timestamp of last update
        /// gets updated once a field updates
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// list of available versions in the backend
        /// </summary>
        public List<Version> Versions { get; set; } = new List<Version>();

        /// <summary>
        /// the currently selected version
        /// </summary>
        public Version CurrentVersion { get; set; }

        /// <summary>
        /// nodes kept in reserve in percent that are on standby for predictions and do not train
        /// </summary>
        //[Range(0, 1)]
        public double ReserveNodes { get; set; }
    }
}
