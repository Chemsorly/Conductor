using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Conductor_Server_Core.Assets.Evaluation
{
    internal class Line
    {
        /// <summary>
        /// contains the full path to the file the line belongs to, for debugging purposes
        /// </summary>
        public String FullPathToFile { get; set; }

        public Bucket Bucket { get; set; }

        //input
        public int SequenceID { get; set; }
        public int SequenceLength { get; set; }
        public int Prefix { get; set; }
        public double TargetValue { get; set; }
        public double Completion { get; set; }
        public double GT_TargetValue { get; set; }
        public double GT_Planned { get; set; }
        public String GT_InstanceID { get; set; }
        public String PrefixActivities { get; set; }
        public String PredictedActivities { get; set; }
        public String SuffixActivities { get; set; }
        public bool Predicted_Violations { get; set; }
        public double? Reliability { get; set; }

        //output
        public double Accuracy { get; set; }
        public double DeviationFromTarget => TargetValue - GT_TargetValue;
        public bool Violation_Effective { get; set; }
        public bool Violation_Predicted { get; set; }
        public String Violation_String => CalculateViolationString(Violation_Effective, Violation_Predicted);

        public static List<Line> GetLinesFromData(List<String> inData, bool pPositiveIsViolation)
        {
            try
            {
                List<Line> output = new List<Line>();

                for (int i = 1; i < inData.Count; i++)
                {
                    //skip first line (headers)
                    //input
                    // 0 sequenceid int
                    // 1 sequencelength int
                    // 2 prefix int
                    // 3 completion float
                    // 4 prediction/target float
                    // 5 ground truth prediction float
                    // 6 ground truth planned (violation/non-violation threshold) float
                    // 7 ground truth processid string
                    // 8 prefix activities string
                    // 9 suffix activities string

                    //split
                    List<String> fields = inData[i].Split(',').ToList();
                    Line line = new Line()
                    {
                        SequenceID = int.Parse(fields[0]),
                        SequenceLength = int.Parse(fields[1]),
                        Prefix = int.Parse(fields[2]),
                        Completion = double.Parse(fields[3], CultureInfo.InvariantCulture),
                        TargetValue = double.Parse(fields[4], CultureInfo.InvariantCulture),                        
                        GT_TargetValue = double.Parse(fields[5]),
                        GT_Planned = double.Parse(fields[6]),
                        GT_InstanceID = fields[7],
                        PrefixActivities = fields[8],
                        SuffixActivities = fields[9]
                    };

                    //calculate accuracy values
                    line.Violation_Effective = pPositiveIsViolation == (line.GT_TargetValue > line.GT_Planned);
                    line.Violation_Predicted = pPositiveIsViolation == (line.TargetValue > line.GT_Planned);
                    line.Accuracy = CalculateAccuracy(line.TargetValue, line.GT_TargetValue);
                    output.Add(line);
                }
                return output;
            }
            catch (Exception)
            {
                throw;
            }

        }

        static String CalculateViolationString(bool pEffective, bool pPredicted)
        {
            if (pEffective && pPredicted)
                return "TP";
            if (!pEffective && pPredicted)
                return "FP";
            if (pEffective && !pPredicted)
                return "FN";
            if (!pEffective && !pPredicted)
                return "TN";

            throw new Exception("unexpected");
        }

        static double CalculateAccuracy(double pInput, double pReference)
        {
            return pInput > pReference
                ? (pInput - (Math.Abs(pInput - pReference) * 2)) / pReference
                : pInput / pReference;
        }
    }
}
