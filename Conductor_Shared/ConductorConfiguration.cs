using MoreLinq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Conductor_Shared
{
    public class ConductorConfiguration
    {
        /// <summary>
        /// timestamp the configuration has been created
        /// gets created once
        /// </summary>
        public DateTime Created { get; set; } = DateTime.Now;

        /// <summary>
        /// timestamp of last update
        /// gets updated once a field updates
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// list of available versions in the backend
        /// </summary>
        public List<Version> Versions { get; set; } = new List<Version>();

        /// <summary>
        /// the currently selected version
        /// </summary>
        public Version CurrentVersion { get; set; }

        /// <summary>
        /// nodes kept in reserve in percent that are on standby for predictions and do not train
        /// </summary>
        //[Range(0, 1)]
        public double ReserveNodes { get; set; }
    }

    /// <summary>
    /// a configuration set containing training files, prediction scripts and trained models
    /// </summary>
    public class Version
    {
        /// <summary>
        /// timestamp of when the version has been created
        /// gets created once
        /// </summary>
        public DateTime Created { get; set; } = DateTime.Now;

        /// <summary>
        /// timestamp of when the version has been updated
        /// gets updated once a field updates
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// contains the major version. needs to be greater than zero
        /// gets created once
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// defines the type of dataset to handle special cases
        /// Generic: the default, does not use any special treatment
        /// Cargo2000: uses a special bucketing algorithm
        /// </summary>
        public DatasetType DatasetType { get; set; }

        /// <summary>
        /// a zipped data blob that contains:
        /// trainingdata.csv - file containing the training data
        /// trainingscript.py - file(s) containing the python files for training
        /// predictionscript.py - file(s) containing the python files for prediction
        /// the commands are then executed on the (extracted) root
        /// 
        /// gets created once
        /// </summary>
        [XmlIgnore]
        public byte[] ZippedDataBlob { get; set; }

        /// <summary>
        /// status about the version, e.g. how many models are trained and how many are missing
        /// gets updated before transmission
        /// </summary>
        public Status Status { get; set; }

        /// <summary>
        /// set of commands being executed during training
        /// gets created once
        /// </summary>
        public List<Command> TrainingCommands { get; set; } = new List<Command>();

        /// <summary>
        /// set of commands being executed during prediction
        /// gets created once
        /// </summary>
        public List<Command> PredictionCommands { get; set; } = new List<Command>();

        /// <summary>
        /// number of models to train
        /// </summary>
        public int TargetModels { get; set; }

        /// <summary>
        /// ctor
        /// </summary>
        public Version() { }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="pMajor">major version</param>
        /// <param name="pMinor">minor version</param>
        public Version(String pName)
        {
            this.Name = pName;
        }

        /// <summary>
        /// overwrite ToString() to get a better representation of the version
        /// </summary>
        /// <returns>the version as vx.y string</returns>
        public override string ToString()
        {
            return this.Name;
        }
    }

    /// <summary>
    /// class containing information about the status of a version object
    /// </summary>
    public class Status
    {
        /// <summary>
        /// list of trained and evaluated models
        /// </summary>
        [XmlIgnore]
        public List<PredictionModel> CurrentModels { get; set; } = new List<PredictionModel>();
    }

    /// <summary>
    /// class containing information about a trained model.
    /// </summary>
    public class PredictionModel
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
        public List<MetricItem> Metrics { get; set; } = new List<MetricItem>();
    }

    /// <summary>
    /// contains a metric (name) and a value for it
    /// </summary>
    public class MetricItem
    {
        /// <summary>
        /// the name for the metric (e.g. mcc)
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// the value of the metric
        /// </summary>
        public double Value { get; set; }
    }

    /// <summary>
    /// class containing a command o execute
    /// </summary>
    public class Command
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

    public enum DatasetType { Generic, Cargo2000 }
}
