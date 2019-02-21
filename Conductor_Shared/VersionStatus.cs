using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Conductor_Shared
{
    /// <summary>
    /// class containing information about the status of a version object
    /// </summary>
    public class VersionStatus
    {
        /// <summary>
        /// list of trained and evaluated models
        /// </summary>
        [XmlIgnore]
        public List<VersionStatusModel> CurrentModels { get; set; } = new List<VersionStatusModel>();
    }
}
