using Conductor_Shared.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Conductor_Shared
{
    public class EnsemblePrediction
    {

        public EnsemblePrediction()
        {
            SinglePredictions = new List<SinglePrediction>();
            WorkPackageIDs = new List<Guid>();
        }

        /// <summary>
        /// the ID for the train to predict
        /// </summary>
        public PredictionRequestPackage RequestPackage { get; set; }
        
        /// <summary>
        /// returns the average prediction value
        /// </summary>
        public double AveragePrediction => SinglePredictions.Average(t => t.PredictedValue);

        public double MedianPrediction => SinglePredictions.Median(t => t.PredictedValue);

        public int TargetPredictions { get; set; }
        public List<SinglePrediction> SinglePredictions { get; set; }
        public List<Guid> WorkPackageIDs { get; set; }
    }
}
