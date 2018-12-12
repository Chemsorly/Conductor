using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Conductor_Server.Configuration;
using Conductor_Server.Utility;
using Conductor_Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Conductor_Server.Client
{
    public class SignalRClientManager : ManagerBase
    {
        public bool IsConnected { get; private set; }
        String Endpoint;

        public delegate void PredictionRequested(PredictionRequestPackage pRequestPackage);
        public event PredictionRequested PredictionRequestedEvent;
        public delegate void ConfigurationUpdated(ConductorConfiguration pConfiguration);
        public event ConfigurationUpdated ConfigurationUpdatedEvent;
        public delegate void Connected();
        public event Connected ConnectedEvent;

        HubConnection _connection;

        public void Initialize(String pEndpoint)
        {
            base.Initialize();
            Endpoint = pEndpoint;

            _connection = new HubConnectionBuilder().WithUrl(Endpoint).Build();
            _connection.On("RequestRNNPrediction", (PredictionRequestPackage pPackage) =>
            {
                PredictionRequestedEvent?.Invoke(pPackage);
            });
            _connection.On("UpdateConfig", (ConductorConfiguration pConfig) =>
            {
                ConfigurationUpdatedEvent?.Invoke(pConfig);
            });
            _connection.Closed += async delegate (Exception arg)
            {
                NotifyNewLogMessageEvent("Connection closed... restarting...");
                IsConnected = false;
                await ConnectAsync();
            };
        }

        public async Task ConnectAsync()
        {
            try
            {
                await _connection.StartAsync();
                NotifyNewLogMessageEvent($"Connected to hub: {Endpoint}");
                IsConnected = true;
                ConnectedEvent?.Invoke();                
            }
            catch (Exception ex)
            {
                NotifyNewLogMessageEvent($"Something went wrong while connecting to {Endpoint}: {ex.Message}");
                IsConnected = false;
                Task.Delay(30000).Wait();
                await ConnectAsync();
            }
        }

        public async void SendResults(RNN_EnsemblePrediction pResults)
        {
            try
            {
                await _connection.InvokeAsync("ReportRNNPrediction", pResults);
                NotifyNewLogMessageEvent("Prediction reported");
            }
            catch (Exception ex)
            {
                NotifyNewLogMessageEvent($"Something went wrong while sending results: {ex.Message}");
            }
        }

        public async void SendConfig(ConductorConfiguration pConfig)
        {
            try
            {
                await _connection.InvokeAsync("ReportConfiguration", pConfig);
                NotifyNewLogMessageEvent("Configuration reported");
            }
            catch (Exception ex)
            {
                NotifyNewLogMessageEvent($"Something went wrong while sending configuration: {ex.Message}");
            }
        }

        public async void SendUpdate(ConductorStatus pStatus)
        {
            try
            {
                await _connection.InvokeAsync("ReportStatus", pStatus);
            }
            catch (Exception ex)
            {
                NotifyNewLogMessageEvent($"Something went wrong while sending status: {ex.Message}");
            }
        }
    }
}
