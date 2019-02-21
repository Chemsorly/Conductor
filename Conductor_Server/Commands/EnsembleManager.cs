using Conductor_Server.Utility;
using Conductor_Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conductor_Server.Commands
{
    public class EnsembleManager : ManagerBase
    {
        public AsyncObservableCollection<EnsemblePrediction> EnsemblePredictions { get; set; }

        public EnsembleManager()
        {
            EnsemblePredictions = new AsyncObservableCollection<EnsemblePrediction>();
        }

        public EnsemblePrediction CreateNewPrediction(PredictionRequestPackage pPredictionRequest)
        {
            NotifyNewLogMessageEvent("Create new prediction");
            var pred = new EnsemblePrediction()
            {
                RequestPackage = pPredictionRequest
            };

            EnsemblePredictions.Add(pred);
            return pred;
        }

        public EnsemblePrediction SubmitPrediction(PredictionResultPackage pPrediction)
        {
            var target = EnsemblePredictions.FirstOrDefault(t => t.WorkPackageIDs.Any(u => u == pPrediction.InWorkPackage.GUID));
            if(target != null)
            {
                target.SinglePredictions.Add(pPrediction.Prediction);

                //check if ensembleprediction is full
                if (target.SinglePredictions.Count >= target.TargetPredictions)
                {
                    EnsemblePredictions.Remove(target);
                    return target;
                }                    
            }
            return null;
        }


        public override void Initialize()
        {
            base.Initialize();
        }
    }
}
