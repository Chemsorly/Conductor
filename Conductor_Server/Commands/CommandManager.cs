using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Conductor_Server.Assets;
using Conductor_Server.Configuration;
using Conductor_Server.Utility;
using Conductor_Shared;
using Conductor_Shared.Enums;

namespace Conductor_Server.Commands
{
    public class CommandManager : ManagerBase
    {
        const int maxQueuesize = 400;

        public delegate void PredictionFinished(EnsemblePrediction pEnsembleprediction);
        public event PredictionFinished PredictionFinishedEvent;
        public event VersionManager.StatusChanged StatusChangedEvent;

        private ResultManager _resultManager;
        private VersionManager _versionManager;
        private FilesystemManager _filesystemManager;
        private EnsembleManager _ensemblemanager;
        private ConfigurationManager _configManager;

        //workitems
        public AsyncObservableCollection<WorkItem> QueuedWorkItems { get; set; }
        public AsyncObservableCollection<WorkItem> ActiveWorkItems { get; set; }

        public List<Conductor_Shared.Version> Versions => _versionManager.AvailableVersions;
        public Conductor_Shared.Version CurrentVersion { get => _versionManager?.CurrentVersion; set { _versionManager.CurrentVersion = value; } }


        List<Model.WorkerClient> connectedClients;
        


        public CommandManager(ConfigurationManager pConfigurationManager, FilesystemManager pFsManager, List<Model.WorkerClient> pConnectedClients)
        {
            _configManager = pConfigurationManager;
            _filesystemManager = pFsManager;
            connectedClients = pConnectedClients;
        }

        public override void Initialize()
        {
            QueuedWorkItems = new AsyncObservableCollection<WorkItem>();
            ActiveWorkItems = new AsyncObservableCollection<WorkItem>();

            _versionManager = new VersionManager(_configManager, _filesystemManager);
            _versionManager.NewLogMessageEvent += NotifyNewLogMessageEvent;
            _versionManager.RequestModelsToTrainEvent += _versionManager_RequestModelsToTrainEvent;
            _versionManager.StatusChangedEvent += StatusChangedEvent;
            _versionManager.Initialize();

            _resultManager = new ResultManager(_filesystemManager);
            _resultManager.NewLogMessageEvent += NotifyNewLogMessageEvent;
            _resultManager.Initialize();

            _ensemblemanager = new EnsembleManager();
            _ensemblemanager.NewLogMessageEvent += NotifyNewLogMessageEvent;
            _ensemblemanager.Initialize();

            base.Initialize();
        }

        /// <summary>
        /// gets called when version manager detects a version count mismatch between target and actual versions. queues up as many models to train as necessary
        /// </summary>
        /// <param name="pVersion">the target version</param>
        /// <param name="pNumber">the amount of models to train. function will substract any model currently in training</param>
        private void _versionManager_RequestModelsToTrainEvent(Conductor_Shared.Version pVersion, int pNumber)
        {
            //check if there are any models in training or already queued, if yes substract from the amount
            var amount = pNumber - 
                ActiveWorkItems.Count(t => t.Version.ToString() == pVersion.ToString() && t.WorkType == WorkPackageType.Training) -
                QueuedWorkItems.Count(t => t.Version.ToString() == pVersion.ToString() && t.WorkType == WorkPackageType.Training); //compare by string since reference won't work
            if (amount > 0)
            {
                //fetch files
                var files = _versionManager.GetFilesForVersion(pVersion);
                for (int i = 0; i < amount; i++)
                {
                    AddNewWorkItem(pVersion, files, WorkPackageType.Training, Guid.NewGuid());
                }
                NotifyNewLogMessageEvent($"Requested {amount} more models for training.");
            }
        }

        public void CreateEnsemblePrediction(PredictionRequestPackage pRequestPackage)
        {
            //create work packages 
            if (CurrentVersion != null && QueuedWorkItems.Count < maxQueuesize)
            {
                //create ensemble prediction
                var pred = _ensemblemanager.CreateNewPrediction(pRequestPackage);
                var path = System.IO.Path.Combine(_filesystemManager.AssetsPath, CurrentVersion.ToString());
                var currentmodels = _versionManager.CurrentVersion.Status.CurrentModels;

                //only get model files where we have metadata
                var modelfiles = _versionManager.GetModelsForVersion(CurrentVersion)
                    .Where(t => currentmodels.Any(u => u.ModelFileName == t.Key))
                    .Select(t => t.Value)
                    .ToList();
                var files = _versionManager.GetFilesForVersion(CurrentVersion);

                //make predictions according to amount of model files
                NotifyNewLogMessageEvent($"Creating ensemble prediction for {CurrentVersion.ToString()} with {modelfiles.Count()} models");
                pred.TargetPredictions = modelfiles.Count();

                //temporarily write predictiondata
                var tempguid = Guid.NewGuid();
                System.IO.File.WriteAllLines(System.IO.Path.Combine(path,tempguid.ToString()),pRequestPackage.PredictionData);

                //generate one work item for each model
                foreach (var model in modelfiles)
                {
                    //make a list from all files and one model file
                    List<File> sendFiles = new List<File>();
                    sendFiles.AddRange(files); //script files
                    sendFiles.Add(model); //model file
                    sendFiles.Add(new File() //prediction data
                    {
                        DirectoryStructure = String.Empty,
                        Filename = "predictiondata.csv",
                        FileData = System.IO.File.ReadAllBytes(System.IO.Path.Combine(path, tempguid.ToString()))
                    });

                    var guid = AddNewWorkItem(CurrentVersion, sendFiles, WorkPackageType.Prediction, Guid.NewGuid());
                    pred.WorkPackageIDs.Add(guid);
                }
                //cleanup
                System.IO.File.Delete(System.IO.Path.Combine(path, tempguid.ToString()));
            }else
                NotifyNewLogMessageEvent("Prediction requested, but no usable version found.");
        }

        public WorkPackage FetchWork(OSEnum pOS, String pAssignedClient)
        {
            //abort if queue is empty
            if (!QueuedWorkItems.Any())
                return null;

            //get client
            var client = connectedClients.FirstOrDefault(t => t.ID == pAssignedClient);
            var priorityClients = GetPriorityClientsCount();
            WorkItem workPackage = null;
            if (client != null)
            {
                var clientIndex = connectedClients.IndexOf(client);
                //check if priority conditions are met: the top n clients in client list are never assigned to training where n is the priority clients count                
                if (clientIndex != -1 && priorityClients > clientIndex)
                {
                    workPackage = QueuedWorkItems.FirstOrDefault(t => t.WorkType == WorkPackageType.Prediction);
                    if (workPackage == null)
                    {
                        NotifyNewLogMessageEvent($"Priority request received from {pAssignedClient}, but no prediction workpackage was found.");
                        return null;
                    }                        
                    NotifyNewLogMessageEvent($"Priority request received from {pAssignedClient} for {workPackage.WorkItemID}");
                }
            }           

            //get first item in list and create package for it
            if(workPackage == null)
                workPackage = QueuedWorkItems.First();           

            //move from queued to active
            QueuedWorkItems.Remove(workPackage);
            ActiveWorkItems.Add(workPackage);

            var package = workPackage.Start(pOS, pAssignedClient);
            return package;
        }

        public void ReceiveTrainingResults(TrainingResultPackage pResults)
        {
            //if save results have been successfully saved, remove item from active operations
            var resultMeta = _resultManager.VerifyAndSave(pResults);
            if (resultMeta != null)
            {
                NotifyNewLogMessageEvent($"Attempt to remove \"{pResults.InWorkPackage.GUID}\" from queue.");

                //remove from active
                var workitem = ActiveWorkItems.FirstOrDefault(t => t.WorkItemID == pResults.InWorkPackage.GUID);
                if (workitem != null)
                {
                    ActiveWorkItems.Remove(workitem);
                    NotifyNewLogMessageEvent($"Removed {workitem.WorkItemID} from active work.");
                }

                //(edge case): remove from queued (e.g. restarting server)
                var queueitem = QueuedWorkItems.FirstOrDefault(t => t.WorkItemID == pResults.InWorkPackage.GUID);
                if (queueitem != null)
                {
                    QueuedWorkItems.Remove(queueitem);
                    NotifyNewLogMessageEvent($"Removed {queueitem.WorkItemID} from queue.");
                }

                //update status by reading in the files and broadcasting the current status
                _versionManager.CheckVersions();
            }
        }

        public void ReceivePredictionResults(PredictionResultPackage pResults)
        {
            //remove from active
            var workitem = ActiveWorkItems.FirstOrDefault(t => t.WorkItemID == pResults.InWorkPackage.GUID);
            if (workitem != null)
            {
                workitem.Finish();
                ActiveWorkItems.Remove(workitem);
                NotifyNewLogMessageEvent($"Removed {workitem.WorkItemID} from active work.");
            }

            //(edge case): remove from queued (e.g. restarting server)
            var queueitem = QueuedWorkItems.FirstOrDefault(t => t.WorkItemID == pResults.InWorkPackage.GUID);
            if (queueitem != null)
            {
                queueitem.Finish();
                QueuedWorkItems.Remove(queueitem);
                NotifyNewLogMessageEvent($"Removed {queueitem.WorkItemID} from queue.");
            }

            var ensemble = _ensemblemanager.SubmitPrediction(pResults);
            if(ensemble != null)
            {
                //!= null equals finished
                PredictionFinishedEvent?.Invoke(ensemble);
            }
            NotifyNewLogMessageEvent($"Received Prediction result for {pResults.InWorkPackage.GUID}: {nameof(pResults.Prediction.PredictedValue)}={pResults.Prediction.PredictedValue}.");
        }

        public void ReceiveErrorFromWorker(List<String> pErrorLog, WorkPackage pWorkPackage)
        {
            //handle error log: save to file
            _versionManager?.ReportError(pWorkPackage.Version, pErrorLog, pWorkPackage.GUID);

            //handle workpackage: manually trigger timeout handler
            HandleError(pWorkPackage.GUID);
        }

        public void UpdateConfiguration(ConductorConfiguration pConfiguration) { this._versionManager.UpdateConfiguration(pConfiguration); }
        public void ReportConfiguration() { this._versionManager.ReportVersions(); }

        private Guid AddNewWorkItem(Conductor_Shared.Version pVersion, List<File> pFiles, WorkPackageType pWorkType, Guid pGuid)
        {
            var newitem = new WorkItem(pVersion, pFiles, pWorkType, pGuid);
            newitem.OnTimeoutHappenedEvent += HandleTimeout;
            QueuedWorkItems.Add(newitem);
            return newitem.WorkItemID;
        }

        void HandleError(Guid pGuid)
        {
            var workitem = ActiveWorkItems.FirstOrDefault(t => t.WorkItemID == pGuid);
            if (workitem != null)
                HandleTimeout(workitem);
        }

        void HandleTimeout(WorkItem pWorkItem)
        {
            var workitem = ActiveWorkItems.FirstOrDefault(t => t.WorkItemID == pWorkItem.WorkItemID);
            if (workitem != null)
            {
                workitem.Finish();
                ActiveWorkItems.Remove(workitem);
                NotifyNewLogMessageEvent($"Removed {workitem.WorkItemID} from active work because of timeout or error.");

                //add to queue again
                AddNewWorkItem(workitem.Version, workitem.Files, workitem.WorkType, workitem.WorkItemID);
            }

            ////check if result items already exist; if it does, skip it
            //if (_resultManager.CheckIfResultExists(pParameters))
            //    return;
        }

        /// <summary>
        /// calculates the current amount of clients kept as priority for incoming prediction results. those clients do not train
        /// </summary>
        /// <returns>current amount of priority clients depending on the total number of clients</returns>
        int GetPriorityClientsCount()
        {
            //check if reserve nodes are used
            if (_configManager.CurrentConfiguration.ReserveNodes == 0d)
                return 0;

            //multiply active connected clients with the factor; if less than 1, use 1 as default
            var priorityClients = _configManager.CurrentConfiguration.ReserveNodes * connectedClients.Count;
            if (priorityClients < 1)
                priorityClients = 1;

            return (int)priorityClients;
        }
    }
}
