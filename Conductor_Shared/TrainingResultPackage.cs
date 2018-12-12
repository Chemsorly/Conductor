using System;
using System.Collections.Generic;
using System.Text;
using MoreLinq;
using System.Linq;

namespace Conductor_Shared
{
    public class TrainingResultPackage : ResultPackage
    {
        /// <summary>
        /// check if the result package is valid by performing several integrity checks
        /// </summary>
        /// <returns>true if valid</returns>
        public bool IsValidResult()
        {
            //check if model file is included in training package
            if (this.ResultFiles.All(t => !t.Filename.Contains(".h5")))
                return false;

            return true;
        }
    }
}
