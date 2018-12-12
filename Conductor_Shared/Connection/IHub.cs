using System;
using System.Collections.Generic;
using System.Text;

namespace Conductor_Shared.Connection
{
    public interface IHub
    {
        /// <summary>
        /// initially sends information about machine data
        /// </summary>
        /// <param name="pMachineData">Machine data structure</param>
        void Connect(MachineData pMachineData);

        /// <summary>
        /// fetches a work unit from the server to process
        /// </summary>
        WorkPackage FetchWork(MachineData pMachineData);

        /// <summary>
        /// sends result to server after work unit is completed; TODO: define result unit
        /// </summary>
        void SendResults(PredictionResultPackage pResults);

        /// <summary>
        /// updates the computing machines status
        /// </summary>
        /// <param name="pStatus">client status structure</param>
        void UpdateStatus(ClientStatus pStatus);

        /// <summary>
        /// sends the latest console output
        /// </summary>
        /// <param name="pMessage">the message</param>
        void SendConsoleMessage(String pMessage);
    }
}
