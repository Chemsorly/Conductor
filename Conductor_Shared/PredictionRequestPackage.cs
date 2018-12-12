using System;
using System.Collections.Generic;
using System.Text;

namespace Conductor_Shared
{
    public class PredictionRequestPackage
    {
        public String TrainID { get; set; }

        public List<String> PredictionData { get; set; }
    }
}
