using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Conductor_Server_Core.Assets.Evaluation
{
    internal class Bucket
    {
        public List<Line> Lines { get; set; }
        public int BucketLevel { get; set; }
        public List<String> Parameters { get; set; }

        //counts
        public int TPcount => ViolationStrings.Count(t => t == "TP");
        public int FPcount => ViolationStrings.Count(t => t == "FP");
        public int TNcount => ViolationStrings.Count(t => t == "TN");
        public int FNcount => ViolationStrings.Count(t => t == "FN");


        public List<Double> Prediction { get; set; }
        public List<String> ViolationStrings { get; set; }
        public List<Double> PredictionAccuracies { get; set; }
        public List<Double> DeviationsAbsolute { get; set; }

        //binary prediction
        public double Precision => (double)ViolationStrings.Count(t => t == "TP") / (double)ViolationStrings.Count(t => t == "TP" || t == "FP");
        public double Recall => (double)ViolationStrings.Count(t => t == "TP") / (double)ViolationStrings.Count(t => t == "TP" || t == "FN");
        public double Specificity => (double)ViolationStrings.Count(t => t == "TN") / (double)ViolationStrings.Count(t => t == "TN" || t == "FP");
        public double FalsePositiveRate => (double)ViolationStrings.Count(t => t == "FP") / (double)ViolationStrings.Count(t => t == "FP" || t == "TN");
        public double NegativePredictedValue => (double)ViolationStrings.Count(t => t == "TN") / (double)ViolationStrings.Count(t => t == "TN" || t == "TP");
        public double Accuracy => (double)ViolationStrings.Count(t => t == "TN" || t == "TP") / (double)ViolationStrings.Count;
        public double FMeasure => ((1 + Math.Pow(EvaluationManager.FmetricBeta, 2)) * Precision * Recall) / ((Math.Pow(EvaluationManager.FmetricBeta, 2) * Precision) + Recall);
        public double MCC => (double)((TPcount * TNcount) - (FPcount * FNcount)) / Math.Sqrt((double)(TPcount + FPcount) * (TPcount + FNcount) * (TNcount + FPcount) * (TNcount + FNcount));

        //regression prediction
        public double PredictionMedian => Median(PredictionAccuracies.ToArray());

        //numeric metrics
        public double MSE => DeviationsAbsolute.Sum(t => Math.Pow(t, 2)) / DeviationsAbsolute.Count;
        public double RMSE => Math.Sqrt(DeviationsAbsolute.Sum(t => Math.Pow(t, 2)) / DeviationsAbsolute.Count);
        public double MAE => DeviationsAbsolute.Sum(t => Math.Abs(t)) / DeviationsAbsolute.Count;
        public double RSE => DeviationsAbsolute.Sum(t => Math.Pow(t, 2)) / (Prediction.Sum(t => Math.Pow(t - Prediction.Average(), 2)));
        public double RRSE => Math.Sqrt(DeviationsAbsolute.Sum(t => Math.Pow(t, 2)) / (Prediction.Sum(t => Math.Pow(t - Prediction.Average(), 2))));
        public double RAE => DeviationsAbsolute.Sum(t => Math.Abs(t)) /
                                (Prediction.Sum(t => Math.Abs(t - Prediction.Average())));

        public static double Median(double[] xs)
        {
            if (xs.Length == 0)
                return 0;

            //https://stackoverflow.com/questions/4140719/calculate-median-in-c-sharp
            var ys = xs.OrderBy(x => x).ToList();
            double mid = (ys.Count - 1) / 2.0;
            return (ys[(int)(mid)] + ys[(int)(mid + 0.5)]) / 2;
        }
    }    
}
