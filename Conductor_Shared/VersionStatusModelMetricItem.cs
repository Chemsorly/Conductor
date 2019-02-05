using System;
using System.Collections.Generic;
using System.Text;

namespace Conductor_Shared
{
    /// <summary>
    /// contains a metric (name) and a value for it
    /// </summary>
    public class VersionStatusModelMetricItem
    {
        /// <summary>
        /// the name for the metric (e.g. mcc)
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// the value of the metric
        /// </summary>
        public double Value { get; set; }
    }
}
