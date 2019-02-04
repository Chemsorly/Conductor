using System;
using System.Collections.Generic;
using System.Text;

namespace Conductor_Shared
{
    /// <summary>
    /// class containing information about a trained model.
    /// </summary>
    public class VersionStatusModel
    {
        /// <summary>
        /// timestamp the model was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// the filename of the model (usually {GUID}.h5
        /// </summary>
        public String ModelFileName { get; set; }

        /// <summary>
        /// duration of the training
        /// </summary>
        public TimeSpan TrainingTime { get; set; }

        /// <summary>
        /// dictionary containing metrics to evaluate the model (e.g. mcc, etc.)
        /// </summary>
        public List<VersionStatusModelMetricItem> Metrics { get; set; } = new List<VersionStatusModelMetricItem>();
    }
}
