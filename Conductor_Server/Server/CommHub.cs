using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Conductor_Server.Model;
using Conductor_Shared;
using Conductor_Shared.Connection;

namespace Conductor_Server.Server
{
    public class CommHub : Hub
    {
        internal delegate void NewClient(String pClientID);
        internal delegate void ClientDisconnected(String pClientID);
        internal delegate void MachineDataReceived(String pClientID, MachineData pMachineData);
        internal delegate void NewClientLogMessage(String pClientID, String pLogMessage);
        internal delegate void NewClientErrorMessage(String pClient, String pLogMessage, List<String> pErrorLog, WorkPackage pWorkPackage);
        internal delegate void ClientStatusUpdated(String pClientID, ClientStatus pClientStatus);

        internal static event NewClient NewClientEvent;
        internal static event ClientDisconnected ClientDisconnectedEvent;
        internal static event MachineDataReceived MachineDataReceivedEvent;
        internal static event NewClientLogMessage NewClientLogMessageEvent;
        internal static event NewClientErrorMessage NewClientErrorMessageEvent;
        internal static event Core.NewLogMessage NewLogMessageEvent;
        internal static event ClientStatusUpdated ClientStatusUpdatedEvent;
        internal static event SignalRServerManager.WorkRequested WorkRequestedEvent;
        internal static event SignalRServerManager.TrainingResultsReceived TrainingResultsReceivedEvent;
        internal static event SignalRServerManager.PredictionResultsReceived PredictionResultsReceivedEvent;

        public override Task OnConnectedAsync()
        {
            NewClientEvent?.Invoke(this.Context?.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            ClientDisconnectedEvent?.Invoke(this.Context?.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        public void Connect(MachineData pMachineData)
        {
            MachineDataReceivedEvent?.Invoke(this.Context?.ConnectionId,pMachineData);
        }

        public WorkPackage FetchWork(MachineData pMachineData)
        {
            //debug test
            NewLogMessageEvent?.Invoke($"New Work Request received from {this.Context?.ConnectionId}");
            var work = WorkRequestedEvent?.Invoke(pMachineData.OperatingSystem, this.Context?.ConnectionId);
            return work;
        }

        public void SendTrainingResults(TrainingResultPackage pResults)
        {
            //debug
            NewLogMessageEvent?.Invoke($"Training files received from {this.Context?.ConnectionId} with {pResults.ResultFiles.Sum(t => t.FileData.Length)} bytes in {pResults.ResultFiles.Count} files");
            TrainingResultsReceivedEvent?.Invoke(pResults, this.Context?.ConnectionId);
        }
        public void SendPredictionResults(PredictionResultPackage pResults)
        {
            //debug
            NewLogMessageEvent?.Invoke($"Prediction results received from {this.Context?.ConnectionId} with value {pResults.Prediction.PredictedValue}");
            PredictionResultsReceivedEvent?.Invoke(pResults, this.Context?.ConnectionId);
        }

        public void UpdateStatus(ClientStatus pStatus)
        {
            ClientStatusUpdatedEvent?.Invoke(this.Context?.ConnectionId, pStatus);
        }

        public void SendConsoleMessage(String pMessage)
        {
            NewClientLogMessageEvent?.Invoke(this.Context?.ConnectionId,pMessage);
        }

        public void SendErrorMessage(String pErrorMessage, List<String> pErrorLog, WorkPackage pWorkPackage)
        {
            NewClientErrorMessageEvent?.Invoke(this.Context?.ConnectionId, pErrorMessage, pErrorLog, pWorkPackage);
        }

    }
}
