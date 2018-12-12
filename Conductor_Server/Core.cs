using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Conductor_Server.Client;
using Conductor_Server.Commands;
using Conductor_Server.Model;
using Conductor_Server.Server;
using Conductor_Server.Utility;
using Conductor_Shared;
using Conductor_Shared.Enums;
using Conductor_Server.Configuration;

namespace Conductor_Server
{
    public class Core : ManagerBase
    {
        private SignalRServerManager _signalrservermanager;
        private SignalRClientManager _signalrclientmanager;
        private CommandManager _commandManager;
        private ConfigurationManager _configManager;
        private FilesystemManager _filesystemManager;

        //forwarded events from SignalR manager
        public event SignalRServerManager.ClientUpdated ClientUpdatedEvent;
        public event SignalRServerManager.NewClient NewClientEvent;
        public event SignalRServerManager.ClientDisconnected ClientDisconnectedEvent;
        public event SignalRServerManager.NewClientLogMessage NewConsoleLogMessage;

        public AsyncObservableCollection<WorkItem> QueuedWorkItems => _commandManager?.QueuedWorkItems;
        public AsyncObservableCollection<WorkItem> ActiveWorkItems => _commandManager?.ActiveWorkItems;
        public List<Conductor_Shared.Version> Versions => _commandManager?.Versions;
        public Conductor_Shared.Version CurrentVersion { get { return _commandManager?.CurrentVersion; } set { _commandManager.CurrentVersion = value; } }
        public List<WorkerClient> ConnectedClients => _signalrservermanager?.ConnectedClients;

        public override void Initialize()
        {
            //get frontend target
            var predictionTarget = GetPredictionTarget();

            //init fs manager
            _filesystemManager = new FilesystemManager();
            _filesystemManager.NewLogMessageEvent += CoreNotifyLogMessage;
            _filesystemManager.Initialize();

            //configuration manager
            _configManager = new ConfigurationManager(_filesystemManager);
            _configManager.NewLogMessageEvent += CoreNotifyLogMessage;
            _configManager.Initialize();

            //forward signalr manager
            _signalrservermanager = new SignalRServerManager();
            _signalrservermanager.NewClientEvent +=delegate(WorkerClient client) { NewClientEvent?.Invoke(client); ClientsUpdated(_signalrservermanager.ConnectedClients.Count); };
            _signalrservermanager.ClientDisconnectedEvent += delegate(WorkerClient client) { ClientDisconnectedEvent?.Invoke(client); ClientsUpdated(_signalrservermanager.ConnectedClients.Count); };
            _signalrservermanager.ClientUpdatedEvent += delegate(WorkerClient client) { ClientUpdatedEvent?.Invoke(client); };
            _signalrservermanager.NewLogMessageEvent += CoreNotifyLogMessage;
            _signalrservermanager.NewConsoleLogMessage += delegate (WorkerClient pClient, string message) { NewConsoleLogMessage?.Invoke(pClient,message); };
            _signalrservermanager.NewClientErrorMessageEvent += delegate (WorkerClient pClient, String pErrorMessage, List<String> pErrorLog, WorkPackage pWorkPackage) { _commandManager?.ReceiveErrorFromWorker(pErrorLog, pWorkPackage); };
            _signalrservermanager.WorkRequestedEvent += SignalrmanagerOnWorkRequestedEvent;
            _signalrservermanager.TrainingResultsReceivedEvent += SignalrmanagerOnTrainingResultsReceivedEvent;
            _signalrservermanager.PredictionResultsReceivedEvent += SignalrmanagerOnPredictionResultsReceivedEvent;
            _signalrservermanager.Initialize();

            //frontend signalr manager
            _signalrclientmanager = new SignalRClientManager();
            _signalrclientmanager.NewLogMessageEvent += CoreNotifyLogMessage;
            _signalrclientmanager.PredictionRequestedEvent += _signalrclientmanager_PredictionRequestedEvent;
            _signalrclientmanager.ConfigurationUpdatedEvent += delegate (ConductorConfiguration pConfiguration) { this._commandManager.UpdateConfiguration(pConfiguration); };
            _signalrclientmanager.ConnectedEvent += delegate () { _commandManager.ReportConfiguration();  };
            if (!String.IsNullOrWhiteSpace(predictionTarget))
            {   //only connect to frontend if a target is specified
                _signalrclientmanager.Initialize($"{predictionTarget}/server/rnn");
                _signalrclientmanager.ConnectAsync()
                    .ContinueWith(t =>
                    {
                        this.ClientsUpdated(_signalrservermanager.ConnectedClients.Count);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
            else this.CoreNotifyLogMessage("SignalRClientManager will not attempt to connect to a frontend because none was specified.");

            //create command manager
            _commandManager = new CommandManager(_configManager, _filesystemManager, _signalrservermanager.ConnectedClients);
            _commandManager.NewLogMessageEvent += CoreNotifyLogMessage;
            _commandManager.PredictionFinishedEvent += _commandManager_PredictionFinishedEvent;
            _commandManager.StatusChangedEvent += _commandManager_StatusChangedEvent;
            _commandManager.Initialize();

            base.Initialize();
        }


        private String GetPredictionTarget()
        {
            String predictionTarget = String.Empty;

            //check startup args
            var startupargsargs = Environment.GetCommandLineArgs();
            if (startupargsargs.Count() == 2)
                predictionTarget = startupargsargs[1];

            //check env
            if(predictionTarget == string.Empty)
                predictionTarget = Environment.GetEnvironmentVariable("CONDUCTOR_HOST");

            return predictionTarget;
        }

        private void _commandManager_StatusChangedEvent(ConductorConfiguration pBackendConfiguration)
        {
            if (_signalrclientmanager.IsConnected)
                _signalrclientmanager.SendConfig(pBackendConfiguration);
            else
                CoreNotifyLogMessage("Trying to send new status, but target host is not connected");
        }

        private void _commandManager_PredictionFinishedEvent(RNN_EnsemblePrediction pEnsembleprediction)
        {
            if (_signalrclientmanager.IsConnected)
                _signalrclientmanager.SendResults(pEnsembleprediction);
            else
                CoreNotifyLogMessage("Trying to send results, but target host is not connected");
        }

        private void _signalrclientmanager_PredictionRequestedEvent(PredictionRequestPackage pRequestPackage)
        {
            _commandManager.CreateEnsemblePrediction(pRequestPackage);
        }

        private void SignalrmanagerOnPredictionResultsReceivedEvent(PredictionResultPackage pResults, string pClientID)
        {
            _commandManager.ReceivePredictionResults(pResults);
        }

        private void SignalrmanagerOnTrainingResultsReceivedEvent(TrainingResultPackage pResults, string pClientID)
        {
            _commandManager.ReceiveTrainingResults(pResults);
        }

        private WorkPackage SignalrmanagerOnWorkRequestedEvent(OSEnum pos, string pclientid)
        {
            return _commandManager.FetchWork(pos, pclientid);
        }

        private void ClientsUpdated(int pNumber)
        {
            if (_signalrclientmanager.IsConnected)
                _signalrclientmanager.SendUpdate(new ConductorStatus()
                {
                    ConnectedNodes = pNumber
                });
        }

         void CoreNotifyLogMessage(String pMessage)
        {
            var msg = $"[{DateTime.UtcNow:G}] " +
            $"[CL:{(ConnectedClients != null ? ConnectedClients.Count : 0)} " +
            $"QW:{(QueuedWorkItems != null ? QueuedWorkItems.Count : 0)} " +
            $"AW:{(ActiveWorkItems != null ? ActiveWorkItems.Count : 0)}] " +
            $"{pMessage}";

            //notify console
            this.NotifyNewLogMessageEvent(msg);

            //notify log writer
            this._filesystemManager?.WriteLogMessageToFilesystem(msg);
        }
    }
}
