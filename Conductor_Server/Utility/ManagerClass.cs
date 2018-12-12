using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Conductor_Server.Utility
{
    public abstract class ManagerBase : INotifyPropertyChanged
    {
        public delegate void NewLogMessage(String pLogMessage);

        public event NewLogMessage NewLogMessageEvent;
        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void Initialize()
        {
            NotifyNewLogMessageEvent($"{this.GetType().Name} initialized.");
        }

        protected void NotifyNewLogMessageEvent(String pMessage)
        {
            NewLogMessageEvent?.Invoke(pMessage);
        }

        protected void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
