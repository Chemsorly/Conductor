using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Conductor_Client.Machine;
using Conductor_Shared;
using System.IO;
using Microsoft.AspNetCore.SignalR.Client;
using MoreLinq;

namespace Conductor_Client.Client
{
    class SignalRManager : INotifyPropertyChanged
    {
        public bool IsConnected { get; private set; } = false;

        String workdirPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "workdir");
        DirectoryInfo _workingDirectory;
        DirectoryInfo WorkingDirectory
        {
            get
            {
                if (!System.IO.Directory.Exists(workdirPath))
                    System.IO.Directory.CreateDirectory(workdirPath);

                if(_workingDirectory == null)
                    _workingDirectory = new DirectoryInfo(workdirPath);

                return _workingDirectory;
            }
        }

        private HubConnection _connection;
        private MachineData _machineData;

        public delegate void LogEventHandler(String message);
        public delegate void NewConsoleMessage(String pMessage);
        public event LogEventHandler LogEvent;

        private Timer _pollTimer;
        private bool IsWorking = false;
        private ClientStatus _clientStatus = new ClientStatus();

        private ConcurrentQueue<String> SavedLog = new ConcurrentQueue<string>();

        public SignalRManager(MachineData pMachineData)
        {
            _machineData = pMachineData;
        }

        public void Initialize(String pEndpoint)
        {
            _connection = new HubConnectionBuilder().WithUrl(pEndpoint, options =>
            {
                options.Headers.Add("authtoken", "ThisIsACustomTokenToPreventSpamBecauseFuckIt");
            }).Build();
            _connection.Closed += delegate (Exception ex)
            {
                NotifyLogMessageEvent($"Connection error: { ex?.Message}");
                IsConnected = false;
                Connect();
                return null;
            };
            Connect();

            _pollTimer?.Dispose();
            _pollTimer = new Timer(delegate(object state)
            {
                RunJob();
            }, null, 6000, 60000);
        }

        private void RunJob()
        {
            if (IsWorking || !IsConnected)
                return;

            IsWorking = true;
            bool runAgain = false;
            WorkPackage work = null;

            try
            {
                work = FetchWork();
                if (work == null)
                    return;

                CleanWorkingDirectory();
                ResultPackage result;

                if (!work.InFiles.Any())
                    throw new ArgumentException("no input files received");

                //deserialize files into target directory
                NotifyLogMessageEvent($"[Log] Deserializing {work.InFiles.Count} files ");
                foreach (var file in work.InFiles)
                {
                    var fullpath = Path.Combine(WorkingDirectory.FullName, file.DirectoryStructure, file.Filename);
                    if (!Directory.Exists(Path.Combine(WorkingDirectory.FullName, file.DirectoryStructure)))
                        Directory.CreateDirectory(Path.Combine(WorkingDirectory.FullName, file.DirectoryStructure));
                    NotifyLogMessageEvent($"[Log] Deserializing {fullpath}");
                    System.IO.File.WriteAllBytes(fullpath, file.FileData);
                }                    
                NotifyLogMessageEvent($"[Log] Deserialized {WorkingDirectory.GetFiles("*.*", SearchOption.AllDirectories).Count()} files");

                //assign work based on received package type; throw error if undefined
                switch(work.WorkType)
                {
                    case WorkType.Training:
                        NotifyLogMessageEvent($"[Log] Processing training work package");
                        result = Training.RunTraining(WorkingDirectory, work, ref _clientStatus, SendStatusUpdate, NotifyLogMessageEvent, _machineData);
                        break;
                    case WorkType.Prediction:
                        NotifyLogMessageEvent($"[Log] Processing prediction work package");
                        result = Prediction.RunPrediction(WorkingDirectory, work, ref _clientStatus, SendStatusUpdate, NotifyLogMessageEvent, _machineData);
                        break;
                    default:
                        throw new Exception("undefined work unit received");
                }

                if (result != null)
                {
                    //gather log
                    result.OutLog = FlushLog(this.SavedLog);

                    //gather out files
                    result.ResultFiles = new List<Conductor_Shared.File>();
                    var resultfiles = WorkingDirectory.EnumerateFiles().Where(t => work.InFiles.All(u => u.Filename != t.Name));                    
                    resultfiles.ForEach(file =>
                    {
                        NotifyLogMessageEvent($"[Log] Serializing {file.FullName}");
                        result.ResultFiles.Add(new Conductor_Shared.File()
                        {
                            Filename = file.Name,
                            FileData = System.IO.File.ReadAllBytes(file.FullName)
                        });
                    });

                    if (result is TrainingResultPackage)
                        SendTrainingResults(result as TrainingResultPackage);
                    else if (result is PredictionResultPackage)
                        SendPredictionResults(result as PredictionResultPackage);
                    else
                        throw new Exception("attempt to send undefined work unit");

                    runAgain = true;
                }
            }
            catch (Exception e)
            {
                NotifyLogMessageEvent($"[ERROR] {e.Message}");
                NotifyErrorMessageEvent(e.Message, work);
            }
            finally
            {
                Cleanup();
                NotifyLogMessageEvent($"[Log] Finished work package");
            }

            //run again if successful
            if (runAgain)
                RunJob();
        }

        private void Cleanup()
        {
            IsWorking = false;
            _clientStatus.IsWorking = false;
            SendStatusUpdate(_clientStatus);
            CleanWorkingDirectory();
        }

        private void Connect()
        {
            try
            {
                _connection.StartAsync()
                .ContinueWith(cont =>
                {
                    NotifyLogMessageEvent("[Log] Hub started");
                    _connection.InvokeAsync("Connect", _machineData);
                    NotifyLogMessageEvent("[Log] Connected to hub");
                    IsConnected = true;
                }, TaskContinuationOptions.OnlyOnRanToCompletion)
                .Wait();
            }            
            catch(Exception ex)
            {
                IsConnected = false;

                NotifyLogMessageEvent($"[Log] Could not connect to hub: {ex.Message}");
                Task.Delay(10000).Wait();
                Connect();
            }
        }

        private void CleanWorkingDirectory()
        {
            foreach (FileInfo file in WorkingDirectory.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in WorkingDirectory.GetDirectories())
            {
                dir.Delete(true);
            }
            NotifyLogMessageEvent("[Log] Cleaned working directory");
        }

        

        private WorkPackage FetchWork()
        {
            try
            {
                NotifyLogMessageEvent("[Log] Fetch work.");
                var result = _connection.InvokeAsync<WorkPackage>("FetchWork", _machineData).Result;

                if (result != null)
                {
                    if(result.WorkType == WorkType.Training)
                        NotifyLogMessageEvent($"[Log] Training Work package received from server: {result.Version.ToString()} {result.GUID}");
                    else if (result.WorkType == WorkType.Prediction)
                        NotifyLogMessageEvent($"[Log] Prediction Work package received from server:{result.Version.ToString()} {result.GUID}");
                    else
                        NotifyLogMessageEvent($"[Log] Unknown Work package received from server: {result.Version.ToString()} {result.GUID}");
                }
                else
                    NotifyLogMessageEvent($"[Log] Requested work package. None available.");
                return result;
            }
            catch (Exception e)
            {
                NotifyLogMessageEvent($"[Log] Fetch work failed: {e.Message}");
                return null;
            }
        }

        private void SendTrainingResults(TrainingResultPackage pResults)
        {
            if (pResults == null)
                return;

            try
            {
                NotifyLogMessageEvent($"Send training results");
                _connection.InvokeAsync("SendTrainingResults", pResults).ContinueWith(t => NotifyLogMessageEvent($"Results sent."), TaskContinuationOptions.OnlyOnRanToCompletion);
            }
            catch (Exception e)
            {
                NotifyLogMessageEvent($"SendTrainingResults failed: {e.Message}");
            }
        }
        private void SendPredictionResults(PredictionResultPackage pResults)
        {
            if (pResults == null)
                return;

            try
            {
                NotifyLogMessageEvent($"Send prediction results");
                _connection.InvokeAsync("SendPredictionResults", pResults)
                    .ContinueWith(t => NotifyLogMessageEvent($"Results sent."), TaskContinuationOptions.OnlyOnRanToCompletion)
                    .ContinueWith(t => NotifyLogMessageEvent($"Something went wrong while sending the results: {t.Exception.Message}"), TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception e)
            {
                NotifyLogMessageEvent($"SendPredictionResults failed: {e.Message}");
            }
        }

        private void Connection_Received(string obj)
        {
            NotifyLogMessageEvent($"[Log] Received message from hub: {obj}");
        }


        private void NotifyLogMessageEvent(String pLogMessage)
        {
            if (!String.IsNullOrWhiteSpace(pLogMessage))
            {
                //add to saved log
                SavedLog.Enqueue(pLogMessage);

                //check for special messages
                CheckForStatusMessages(pLogMessage);

                LogEvent?.Invoke(pLogMessage);
                SendConsoleMessage($"{pLogMessage}");
            }
        }

        private void NotifyErrorMessageEvent(String pErrorMessage, WorkPackage pWorkPackage)
        {
            if(!String.IsNullOrWhiteSpace(pErrorMessage))
            {
                //gather log
                var outlog = FlushLog(this.SavedLog);
                //clear payload
                pWorkPackage.InFiles.Clear();
                SendErrorMessage(pErrorMessage, outlog, pWorkPackage);
            }            
        }

        private void CheckForStatusMessages(String pMessage)
        {
            //check for epoch match
            var epochmatch = Regex.Matches(pMessage, @"Epoch \d+\/\d+");
            if (epochmatch.Count > 0)
            {
                //get match
                var match = epochmatch[0].Value;

                //get first numbers
                var matches = Regex.Matches(match, @"\d+");

                //set value
                int value;
                if (int.TryParse(matches[0].Value, out value))
                {
                    _clientStatus.CurrentEpoch = value;
                    SendStatusUpdate(_clientStatus);
                }
                
            }

            //check for duration match
            var durationmatch = Regex.Matches(pMessage, @"\d+s");
            if (durationmatch.Count > 0)
            {
                _clientStatus.LastEpochDuration = durationmatch[0].Value;
                SendStatusUpdate(_clientStatus);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region server functions

        /// <summary>
        /// sends a console message to the server
        /// </summary>
        /// <param name="pConsoleMessage"></param>
        private void SendConsoleMessage(String pConsoleMessage)
        {
            try
            {
                _connection.InvokeAsync("SendConsoleMessage", pConsoleMessage);
            }
            catch (Exception)
            {

            }
        }

        private void SendStatusUpdate(ClientStatus pClientStatus)
        {
            try
            {
                _connection.InvokeAsync("UpdateStatus", pClientStatus);
            }
            catch (Exception)
            {

            }
        }

        private void SendErrorMessage(String pErrorMessage, List<String> pErrorLog, WorkPackage pWorkPackage)
        {
            try
            {
                _connection.InvokeAsync("SendErrorMessage", pErrorMessage, pErrorLog, pWorkPackage);
            }
            catch (Exception)
            {

            }
        }

        private static List<String> FlushLog(ConcurrentQueue<String> pLog)
        {
            List<String> outlog = new List<string>();
            while (!pLog.IsEmpty)
            {
                String item;
                pLog.TryDequeue(out item);
                outlog.Add(item);
            }
            return outlog;
        }

        #endregion


    }
}
