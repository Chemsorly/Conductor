using Conductor_Server.Commands;
using Conductor_Server.Utility;
using Conductor_Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Conductor_Server_Core.Assets.Evaluation
{
    public class EvaluationManager : ManagerBase
    {
        //default values
        internal static readonly double BucketGranularity = 0.1; //creates a bucket every 0.1 of completion
        internal static readonly int BucketingType = 1;
        internal static readonly double FmetricBeta = 1;
        FilesystemManager fsManager;

        public EvaluationManager(FilesystemManager pFsManager)
        {
            fsManager = pFsManager;
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public List<MetricItem> EvaluateModel(Conductor_Shared.Version pVersion, PredictionModel pModel)
        {
            try
            {
                //check if version defines a special bucketing algorithm
                int bucketing = BucketingType;
                switch (pVersion.DatasetType)
                {
                    case DatasetType.Generic:
                        bucketing = 1; break;
                    case DatasetType.Cargo2000:
                        bucketing = 2; break;
                    default:
                        bucketing = 1; break;
                }

                //get raw data 
                var data = fsManager.GetModelResults(pVersion, pModel);
                if (data != null && data.Count > 1)
                {
                    //generate lines
                    List<Line> lines = Line.GetLinesFromData(data, pVersion.DatasetType == DatasetType.Generic);

                    //generate buckets from lines
                    List<Bucket> buckets = Bucketing.CreateBuckets(BucketGranularity, BucketingType, lines);

                    List<MetricItem> metrics = new List<MetricItem>();
                    if (buckets.Any(t => !double.IsNaN(t.MCC)))
                        metrics.Add(new MetricItem() { Name = "MCC", Value = buckets.Where(t => !double.IsNaN(t.MCC)).Average(t => t.MCC) });
                    if (buckets.Any(t => !double.IsNaN(t.Accuracy)))
                        metrics.Add(new MetricItem() { Name = "Accuracy", Value = buckets.Where(t => !double.IsNaN(t.Accuracy)).Average(t => t.Accuracy) });
                    if (buckets.Any(t => !double.IsNaN(t.Precision)))
                        metrics.Add(new MetricItem() { Name = "Precision", Value = buckets.Where(t => !double.IsNaN(t.Precision)).Average(t => t.Precision) });
                    if (buckets.Any(t => !double.IsNaN(t.Recall)))
                        metrics.Add(new MetricItem() { Name = "Recall", Value = buckets.Where(t => !double.IsNaN(t.Recall)).Average(t => t.Recall) });
                    if (buckets.Any(t => !double.IsNaN(t.FMeasure)))
                        metrics.Add(new MetricItem() { Name = "FMetric", Value = buckets.Where(t => !double.IsNaN(t.FMeasure)).Average(t => t.FMeasure) });
                    if (buckets.Any(t => !double.IsNaN(t.Specificity)))
                        metrics.Add(new MetricItem() { Name = "Speceficity", Value = buckets.Where(t => !double.IsNaN(t.Specificity)).Average(t => t.Specificity) });
                    if (buckets.Any(t => !double.IsNaN(t.NegativePredictedValue)))
                        metrics.Add(new MetricItem() { Name = "Negative Predictions", Value = buckets.Where(t => !double.IsNaN(t.NegativePredictedValue)).Average(t => t.NegativePredictedValue) });
                    if (buckets.Any(t => !double.IsNaN(t.FalsePositiveRate)))
                        metrics.Add(new MetricItem() { Name = "False Positive Rate", Value = buckets.Where(t => !double.IsNaN(t.FalsePositiveRate)).Average(t => t.FalsePositiveRate) });
                    if (buckets.Any(t => !double.IsNaN(t.MAE)))
                        metrics.Add(new MetricItem() { Name = "MAE", Value = buckets.Where(t => !double.IsNaN(t.MAE)).Average(t => t.MAE) });
                    if (buckets.Any(t => !double.IsNaN(t.MSE)))
                        metrics.Add(new MetricItem() { Name = "MSE", Value = buckets.Where(t => !double.IsNaN(t.MSE)).Average(t => t.MSE) });
                    if (buckets.Any(t => !double.IsNaN(t.RMSE)))
                        metrics.Add(new MetricItem() { Name = "RMSE", Value = buckets.Where(t => !double.IsNaN(t.RMSE)).Average(t => t.RMSE) });
                    if (buckets.Any(t => !double.IsNaN(t.RAE)))
                        metrics.Add(new MetricItem() { Name = "RAE", Value = buckets.Where(t => !double.IsNaN(t.RAE)).Average(t => t.RAE) });
                    if (buckets.Any(t => !double.IsNaN(t.RSE)))
                        metrics.Add(new MetricItem() { Name = "RSE", Value = buckets.Where(t => !double.IsNaN(t.RSE)).Average(t => t.RSE) });
                    if (buckets.Any(t => !double.IsNaN(t.RRSE)))
                        metrics.Add(new MetricItem() { Name = "RRSE", Value = buckets.Where(t => !double.IsNaN(t.RRSE)).Average(t => t.RRSE) });
                    return metrics;
                }                
            }
            catch(Exception ex)
            {
                NotifyNewLogMessageEvent($"something went wrong while evaluating {pVersion.ToString()} {pModel.ModelFileName}: {ex.Message}");
            }
            
            return null;
        }
    }
}

