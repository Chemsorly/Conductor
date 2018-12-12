using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Conductor_Server.Model;
using Conductor_Server.Utility;
using Conductor_Shared;
using Conductor_Shared.Enums;

namespace Conductor_Server.Server
{
    public class SignalRServerManager : ManagerBase
    {
        public delegate void NewClient(WorkerClient pClient);
        public delegate void ClientUpdated(WorkerClient pClient);
        public delegate void ClientDisconnected(WorkerClient pClient);
        public delegate void NewClientLogMessage(WorkerClient pClient, String pMessage);
        public delegate void NewClientErrorMessage(WorkerClient pClient, String pLogMessage, List<String> pErrorLog, WorkPackage pWorkPackage);
        public delegate WorkPackage WorkRequested(OSEnum pOS, String pClientID);
        public delegate void TrainingResultsReceived(TrainingResultPackage pResults, String pClientID);
        public delegate void PredictionResultsReceived(PredictionResultPackage pResults, String pClientID);
        public List<WorkerClient> ConnectedClients { get; } = new List<WorkerClient>();

        public event ClientUpdated ClientUpdatedEvent;
        public event NewClient NewClientEvent;
        public event ClientDisconnected ClientDisconnectedEvent;
        public event NewClientLogMessage NewConsoleLogMessage;
        public event NewClientErrorMessage NewClientErrorMessageEvent;
        public event WorkRequested WorkRequestedEvent;
        public event TrainingResultsReceived TrainingResultsReceivedEvent;
        public event PredictionResultsReceived PredictionResultsReceivedEvent;

        public override void Initialize()
        {
            CommHub.NewClientEvent += NotifyNewClientEvent;
            CommHub.ClientDisconnectedEvent += NotifyClientDisconnectedEvent;
            CommHub.NewLogMessageEvent += NotifyNewLogMessageEvent;
            CommHub.NewClientLogMessageEvent += NewClientLogMessageEvent;
            CommHub.NewClientErrorMessageEvent += NewclientErrorMessageEvent;
            CommHub.MachineDataReceivedEvent += (id, data) =>
            {
                var client = ConnectedClients.FirstOrDefault(t => t.ID == id);
                if (client != null)
                {
                    client.ContainerVersion = data.ContainerVersion;
                    client.OperatingSystem = data.OperatingSystem;
                    client.ProcessingUnit = data.ProcessingUnit;
                    client.MachineName = data.Name;
                    NotifyClientUpdatedEvent(client);
                }
            };
            CommHub.ClientStatusUpdatedEvent += (id, status) =>
            {
                var client = ConnectedClients.FirstOrDefault(t => t.ID == id);
                if (client != null)
                {
                    client.IsWorking = status.IsWorking;
                    client.LastEpochDuration = status.LastEpochDuration;
                    client.CurrentEpoch = status.CurrentEpoch;
                    client.CurrentWorkParameters = status.CurrentWorkParameters;
                    NotifyClientUpdatedEvent(client);
                }
            };  
            CommHub.WorkRequestedEvent += (os, id) => WorkRequestedEvent?.Invoke(os, id);
            CommHub.TrainingResultsReceivedEvent += (results, id) => TrainingResultsReceivedEvent?.Invoke(results, id);
            CommHub.PredictionResultsReceivedEvent += (results, id) => PredictionResultsReceivedEvent?.Invoke(results, id);

            NotifyNewLogMessageEvent("Attempting to initialize SignalR listener on port 8080");
            SignalRStartup.Run("http://0.0.0.0:8080/").ContinueWith(itask =>
            {
                NotifyNewLogMessageEvent($"Could not start SignalR listener on port 8080. Are you running the application as admin? Exception: {itask.Exception.Message}");
            }, TaskContinuationOptions.OnlyOnFaulted)
            .ContinueWith(itask =>
            {
                NotifyNewLogMessageEvent($"Started SignalR listener on port 8080");
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            base.Initialize();
        }

        private void NotifyNewClientEvent(String pClientID)
        {
            lock (ConnectedClients)
            {
                //add client if not exist
                if (ConnectedClients.All(t => t.ID != pClientID))
                {
                    ConnectedClients.Add(new WorkerClient()
                    {
                        ID = pClientID
                    });
                }
                NewClientEvent?.Invoke(ConnectedClients.FirstOrDefault(t => t.ID == pClientID));
                NotifyNewLogMessageEvent($"Client connected: {pClientID}");
            }
        }

        private void NotifyClientDisconnectedEvent(String pClientID)
        {
            lock (ConnectedClients)
            {
                var client = ConnectedClients.FirstOrDefault(t => t.ID == pClientID);
                if (client != null)
                {
                    ConnectedClients.Remove(client);
                    ClientDisconnectedEvent?.Invoke(client);
                    NotifyNewLogMessageEvent($"Client disconnected: {pClientID}");
                }
            }
        }
        private void NotifyClientUpdatedEvent(WorkerClient pClient)
        {
            ClientUpdatedEvent?.Invoke(pClient);
            //NotifyNewLogMessageEvent($"CONNECT: {pClient.ID}");
        }

        private void NewClientLogMessageEvent(string pClientID, string pLogMessage)
        {
            //discard empty messages
            if (String.IsNullOrWhiteSpace(pLogMessage))
                return;

            var targetClient = ConnectedClients.FirstOrDefault(t => t.ID == pClientID);
            if (targetClient != null)
            {
                NewConsoleLogMessage?.Invoke(targetClient, pLogMessage);
            }
        }

        private void NewclientErrorMessageEvent(String pClientID, String pErrorMessage, List<String> pErrorLog,WorkPackage pWorkPackage)
        {
            //discard empty messages
            if (String.IsNullOrWhiteSpace(pErrorMessage))
                return;
            NotifyNewLogMessageEvent($"[{pClientID}] {pErrorMessage}");

            var targetClient = ConnectedClients.FirstOrDefault(t => t.ID == pClientID);
            if (targetClient != null)
            {
                NewClientErrorMessageEvent?.Invoke(targetClient, pErrorMessage, pErrorLog, pWorkPackage);
            }
        }
    }
}
