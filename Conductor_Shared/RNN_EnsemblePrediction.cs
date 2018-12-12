using Conductor_Shared.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Conductor_Shared
{
    public class RNN_EnsemblePrediction
    {

        public RNN_EnsemblePrediction()
        {
            SinglePredictions = new List<RNN_SinglePrediction>();
            WorkPackageIDs = new List<Guid>();
        }

        /// <summary>
        /// the ID for the train to predict
        /// </summary>
        public PredictionRequestPackage RequestPackage { get; set; }
        
        /// <summary>
        /// returns the average prediction value
        /// </summary>
        public double AveragePrediction => SinglePredictions.Average(t => t.PredictedBuffer);

        public double MedianPrediction => SinglePredictions.Median(t => t.PredictedBuffer);

        public int TargetPredictions { get; set; }
        public List<RNN_SinglePrediction> SinglePredictions { get; set; }
        public List<Guid> WorkPackageIDs { get; set; }
    }
}
