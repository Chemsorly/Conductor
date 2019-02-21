using System;
using System.Collections.Generic;
using System.Text;

namespace Conductor_Shared
{
    /// <summary>
    /// class containing a command to execute
    /// </summary>
    public class VersionCommand
    {
        /// <summary>
        /// the file to execute (e.g. python)
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// the supplied arguments (e.g. python file)
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// TODO: currently unused, only used as running variable?
        /// </summary>
        public string Parameters { get; set; }
    }
}
