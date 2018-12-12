using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Conductor_Server.Utility;
using Conductor_Shared;
using Version = Conductor_Shared.Version;

namespace Conductor_Server.Commands
{
    class ResultManager : ManagerBase
    {
        private FilesystemManager _filesystemManager;       

        public ResultManager(FilesystemManager pFilesystemManager)
        {
            _filesystemManager = pFilesystemManager;
        }

        public bool CheckIfResultExists(Version pVersion, String pGUID)
        {
            return _filesystemManager.CheckIfFileExists(pVersion, pGUID);
        }

        public MetaStruct VerifyAndSave(TrainingResultPackage pResults)
        {
            //check for completeness
            if(!pResults.IsValidResult())
            {
                NotifyNewLogMessageEvent($"[ERROR] Attempting to write incomplete results for {pResults.InWorkPackage.GUID.ToString()}");
                return null;
            }

            //check if already exist
            if (_filesystemManager.CheckIfFileExists(pResults.InWorkPackage.Version, pResults.InWorkPackage.GUID.ToString()))
            {
                NotifyNewLogMessageEvent($"[ERROR] Attempting to write duplicate results for {pResults.InWorkPackage.GUID.ToString()}");
                return null;
            }

            try
            {
                return _filesystemManager.WriteResultsToFilesystem(pResults);
            }
            catch (Exception ex)
            {
                NotifyNewLogMessageEvent($"[ERROR] {ex.Message}");
                return null;
            }
        }
    }
}
