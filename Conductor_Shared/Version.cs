using Conductor_Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Conductor_Shared
{
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
        public VersionStatus Status { get; set; }

        /// <summary>
        /// set of commands being executed during training
        /// gets created once
        /// </summary>
        public List<VersionCommand> TrainingCommands { get; set; } = new List<VersionCommand>();

        /// <summary>
        /// set of commands being executed during prediction
        /// gets created once
        /// </summary>
        public List<VersionCommand> PredictionCommands { get; set; } = new List<VersionCommand>();

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
}
