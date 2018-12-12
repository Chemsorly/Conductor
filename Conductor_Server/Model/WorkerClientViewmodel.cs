using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Conductor_Server.Utility;
using Conductor_Shared.Enums;

namespace Conductor_Server.Model
{
    public class WorkerClientViewmodel : INotifyPropertyChanged
    {
        private WorkerClient _client;
        private String _id = String.Empty;
        private String _machineName = String.Empty;
        private String _containerVersion = String.Empty;
        private OSEnum? _operatingSystem = null;
        private ProcessingUnitEnum? _processingUnit = null;
        private bool _isWorking = false;
        private String _lastEpochDuration = String.Empty;
        private int _currentEpoch = 0;
        private String _currentWorkParameters = String.Empty;
        private DateTime _lastUpdate = DateTime.MinValue;

        public WorkerClientViewmodel(WorkerClient pClient)
        {
            _client = pClient;
        }

        public String ID
        {
            get => _id;
            set { _id = value; OnPropertyChanged();}
        }
        public String MachineName
        {
            get => _machineName;
            set { _machineName = value; OnPropertyChanged(); }
        }
        public String ContainerVersion
        {
            get => _containerVersion;
            set { _containerVersion = value; OnPropertyChanged(); }
        }
        public OSEnum OperatingSystem
        {
            get => _operatingSystem ?? OSEnum.undefined;
            set { _operatingSystem = value; OnPropertyChanged(); }
        }
        public ProcessingUnitEnum ProcessingUnit
        {
            get => _processingUnit ?? ProcessingUnitEnum.undefined;
            set { _processingUnit = value; OnPropertyChanged(); }
        }

        private AsyncObservableCollection<String> _logMessages;
        public AsyncObservableCollection<String> LogMessages
        {
            get { if(_logMessages == null) _logMessages = new AsyncObservableCollection<string>(); return _logMessages;}
            set
            {
                _logMessages = value;
                OnPropertyChanged();
            }
        }

        public bool IsWorking
        {
            get => _isWorking;
            set { _isWorking = value; OnPropertyChanged(); }
        }

        public String LastEpochDuration
        {
            get => _lastEpochDuration;
            set { _lastEpochDuration = value; OnPropertyChanged();
            }
        }
        public int CurrentEpoch
        {
            get => _currentEpoch;
            set
            {
                _currentEpoch = value; OnPropertyChanged();
            }
        }
        public String CurrentWorkParameters
        {
            get => _currentWorkParameters;
            set
            {
                _currentWorkParameters = value; OnPropertyChanged();
            }
        }

        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set { _lastUpdate = value; OnPropertyChanged(); }
        }

        public void UpdateValues(WorkerClient pClient)
        {
            ContainerVersion = pClient.ContainerVersion;
            OperatingSystem = pClient.OperatingSystem;
            ProcessingUnit = pClient.ProcessingUnit;
            IsWorking = pClient.IsWorking;
            LastEpochDuration = pClient.LastEpochDuration;
            CurrentEpoch = pClient.CurrentEpoch;
            CurrentWorkParameters = pClient.CurrentWorkParameters;
            MachineName = pClient.MachineName;
            _lastUpdate = DateTime.UtcNow;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
