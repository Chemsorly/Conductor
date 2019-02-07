using MoreLinq;
using Conductor_Server.Commands;
using Conductor_Server.Configuration;
using Conductor_Server.Utility;
using Conductor_Server_Core.Assets.Evaluation;
using Conductor_Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Serialization;
using Version = Conductor_Shared.Version;

namespace Conductor_Server.Assets
{
    public class VersionManager : ManagerBase
    {
        public delegate void RequestModelsToTrain(Version pVersion, int pNumber);
        public event RequestModelsToTrain RequestModelsToTrainEvent;
        public delegate void StatusChanged(ConductorConfiguration pBackendConfiguration);
        public event StatusChanged StatusChangedEvent;


        public List<Version> AvailableVersions => configurationManager.CurrentConfiguration.Versions;
        public Version CurrentVersion { get => configurationManager.CurrentConfiguration.CurrentVersion;   set { configurationManager.CurrentConfiguration.CurrentVersion = value; } }

        ConfigurationManager configurationManager;
        FilesystemManager fsManager;
        EvaluationManager evManager;

        DirectoryInfo AssetRoot { get; set; }
        String AssetRootPath { get; }

        Timer versionCheckTimer;
        object versionCheckLock = new object();

        public VersionManager(ConfigurationManager pConfigurationManager, FilesystemManager pFsManager)
        {
            configurationManager = pConfigurationManager;
            fsManager = pFsManager;
            evManager = new EvaluationManager(fsManager);
            evManager.NewLogMessageEvent += NotifyNewLogMessageEvent;
            evManager.Initialize();
            AssetRootPath = fsManager.AssetsPath; ;
        }

        public override void Initialize()
        {
            //assuming FS manager gets initialized before and creates directory if not exist            
            this.AssetRoot = new DirectoryInfo(AssetRootPath);
            if (AvailableVersions.Any())
            {
                //automatically set latest on startup
                configurationManager.CurrentConfiguration.CurrentVersion = configurationManager.CurrentConfiguration.Versions.MaxBy(t => t.Created).FirstOrDefault();
                NotifyNewLogMessageEvent($"Selected {configurationManager.CurrentConfiguration.CurrentVersion} as current version on startup");
            }
            CheckVersions();
            versionCheckTimer = new Timer();
            versionCheckTimer.Interval = 3600000; //3.6m equals one hour
            versionCheckTimer.AutoReset = true;
            versionCheckTimer.Elapsed += delegate (object sender, ElapsedEventArgs e) {
                NotifyNewLogMessageEvent("Periodic version check");
                this.CheckVersions();
                this.ReportVersions();
            };
            versionCheckTimer.Start();

            base.Initialize();
        }

        internal List<Conductor_Shared.File> GetFilesForVersion(Version pVersion)
        {
            //returns and serializes all files in the root directory. explicitly exclude models folder, as models are fetched via secondary call
            List<FileInfo> fileInfos = new List<FileInfo>();

            var root = fsManager.GetVersionRootDirectory(pVersion);

            root.GetFiles().ForEach(t => fileInfos.Add(t));
            root.GetDirectories().Where(t => t.Name != "models" && t.Name != "evaluation").SelectMany(t => t.GetFiles()).ForEach(t => fileInfos.Add(t));

            return fileInfos.Select(t => new Conductor_Shared.File()
            {
                DirectoryStructure = t.Directory.FullName != root.FullName ? Filesystem.MakeRelativePath(root.FullName, t.Directory.FullName) : String.Empty,
                Filename = t.Name,
                FileData = System.IO.File.ReadAllBytes(t.FullName)
            }).ToList();
        }

        internal Dictionary<String,Conductor_Shared.File> GetModelsForVersion(Version pVersion)
        {
            //get version models folder
            var modelsdir = fsManager.GetVersionModelsDirectory(pVersion);
            var modelDict = new Dictionary<String, Conductor_Shared.File>();
            foreach (var model in modelsdir.GetFiles().Where(t => t.Extension == ".h5"))
                modelDict.Add(Path.GetFileNameWithoutExtension(model.Name),
                    new Conductor_Shared.File()
                    {
                        DirectoryStructure = String.Empty,
                        Filename = "model-latest.h5",
                        FileData = System.IO.File.ReadAllBytes(model.FullName)
                    });
            return modelDict;
        }

        internal void ReportError(Version pVersion, List<string> pErrorLog, Guid pErrorguid)
        {
            //get local version object since this was sent via serialization
            var version = AvailableVersions.FirstOrDefault(t => t.ToString() == pVersion.ToString());
            if(version != null)
                fsManager.WriteErrorlogToFilesystem(pVersion, pErrorLog, pErrorguid);
        }

        internal void CheckVersions()
        {
            //lock the version check, just in case two get triggered at the same time
            lock(versionCheckLock)
            {
                //additionally check local folders for any local versions that is not in AvailableVersions
                var localVersions = new DirectoryInfo(fsManager.AssetsPath).GetDirectories()
                    .Where(t => !AvailableVersions.Any(u => u.Name == t.Name))
                    .SelectMany(t => t.GetFiles().Where(u => u.Name == "version.xml"))
                    .Select(t => fsManager.LoadVersionFromFile(t))
                    .Where(t => t != null).ToList();
                List<Version> versionsToCheck = new List<Version>();
                versionsToCheck.AddRange(AvailableVersions);
                versionsToCheck.AddRange(localVersions);

                //iterates through versions and checks the model files and their metadata; updates status object
                NotifyNewLogMessageEvent($"Checking versions.");
                if (!versionsToCheck.Any())
                {
                    NotifyNewLogMessageEvent($"No versions found");
                    return;
                }
                NotifyNewLogMessageEvent($"Checking {versionsToCheck.Count} versions. (Global: {AvailableVersions.Count()}, Local: {localVersions.Count()})");
                foreach (var version in versionsToCheck)
                {
                    //update status
                    version.Status = UpdateStatus(version, version.Status);

                    //check if model count is bigger or equals target count, if not create more models
                    if (version.Status.CurrentModels.Count < version.TargetModels)
                    {
                        int amount = version.TargetModels - version.Status.CurrentModels.Count;
                        NotifyNewLogMessageEvent($"Not enough models found for {version.ToString()}. {amount} models are missing");
                        RequestModelsToTrainEvent?.Invoke(version, amount);
                    }
                }
                NotifyNewLogMessageEvent($"Checked {versionsToCheck.Count} versions.");
                configurationManager.SaveConfig();
                configurationManager.SaveLocalVersions(localVersions);
            }            
        }

        internal void ReportVersions()
        {
            StatusChangedEvent?.Invoke(configurationManager.CurrentConfiguration);
        }

        internal void UpdateConfiguration(ConductorConfiguration pConfiguration) {
            this.configurationManager.UpdateConfiguration(pConfiguration);
            NotifyNewLogMessageEvent("Configuration updated.");
            StatusChangedEvent?.Invoke(configurationManager.CurrentConfiguration);
        }

        VersionStatus UpdateStatus(Version pVersion, VersionStatus pOldStatus = null)
        {
            //get version models folder
            var dir = fsManager.GetVersionModelsDirectory(pVersion);

            //iterate through files (h5 models, log logfiles, xml meta data and (if applicable) result csv files
            List<FileInfo> modelFiles = new List<FileInfo>(), logFiles = new List<FileInfo>(), resultFiles = new List<FileInfo>();
            Dictionary<FileInfo, FileInfo> metaMapping = new Dictionary<FileInfo, FileInfo>();

            foreach (var modelDir in dir.GetDirectories())
            {
                //get all dirs where there's at least one h5 model file
                var files = modelDir.GetFiles("*", SearchOption.AllDirectories);
                var modelFile = files.FirstOrDefault(t => t.Name.Contains(".h5"));
                var metaFile = files.FirstOrDefault(t => t.Name == "meta.xml");
                var logFile = files.FirstOrDefault(t => t.Name == "console.log");
                var resultFile = files.FirstOrDefault(t => t.Name.Contains("results.csv"));

                //check if model and meta files exist, create mapping if they do
                if(modelFile == null)
                    NotifyNewLogMessageEvent($"No model file found for {modelDir.Name}");
                else
                {
                    if (metaFile == null)
                        NotifyNewLogMessageEvent($"No meta file found for {modelDir.Name}");
                    else
                        metaMapping.Add(modelFile, metaFile);
                }

                //check if log and result files exist
                if (logFile == null)
                    NotifyNewLogMessageEvent($"No log file found for {modelDir.Name}");
                if (resultFile == null)
                    NotifyNewLogMessageEvent($"No result file found for {modelDir.Name}");

                //additional log files
                //var additionalFiles = files.Where(t => t != modelFile && t != metaFile && t != logFile && t != resultFile));
                //foreach (var file in additionalFiles)
                //{
                //    if (file.Name.Contains(".epochlog"))
                //        logFiles.Add(file);
                //    else
                //        NotifyNewLogMessageEvent($"Unexpected file found {file.FullName}");
                //}
            }
       
            //create status object
            VersionStatus status = new VersionStatus();
            Parallel.ForEach(metaMapping, model =>
            {
                //get metadata
                var xmlserializer = new XmlSerializer(typeof(MetaStruct));
                MetaStruct metaData;
                using (var fs = new FileStream(model.Value.FullName, FileMode.Open))
                {
                    metaData = (MetaStruct)xmlserializer.Deserialize(fs);
                    fs.Close();
                }

                //update status (reuse old stuff if applicable                
                var modelFileName = model.Key.Directory.Name;
                var oldStatusModel = pOldStatus != null ? pOldStatus.CurrentModels.FirstOrDefault(t => t.ModelFileName == modelFileName) : null;

                var predictionModel = new VersionStatusModel()
                {
                    CreatedAt = metaData.Timestamp,
                    TrainingTime = metaData.Duration,
                    ModelFileName = modelFileName,
                    Metrics = metaData.PredictionModel != null ? metaData.PredictionModel.Metrics : null
                };

                //check if previous metrics exist, if yes, skip
                if (metaData.PredictionModel != null && metaData.PredictionModel.Metrics.Any())
                {
                    //check previous meta struct fule
                    predictionModel = metaData.PredictionModel;
                }
                else if (oldStatusModel != null && oldStatusModel.Metrics.Any())
                {
                    //check for any reference in status
                    predictionModel.Metrics = oldStatusModel.Metrics;
                }
                else
                {
                    //no metrics found, generate new metrics and add to meta struct file
                    predictionModel.Metrics = evManager.EvaluateModel(pVersion, predictionModel);
                    metaData.PredictionModel = predictionModel;
                    using (var fs = new FileStream(model.Value.FullName, FileMode.Create))
                    {
                        xmlserializer.Serialize(fs, metaData);
                        fs.Close();
                    }
                }
                lock(status)
                    status.CurrentModels.Add(predictionModel);
            });

            return status;
        }


    }
}
