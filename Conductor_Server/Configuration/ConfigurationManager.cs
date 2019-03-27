using MoreLinq;
using Conductor_Server.Commands;
using Conductor_Server.Utility;
using Conductor_Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Conductor_Server.Configuration
{
    public class ConfigurationManager : ManagerBase
    {
        FilesystemManager _filesystemManager;
        string configFilePath => System.IO.Path.Combine(_filesystemManager.ConfigPath, "config.xml");

        public ConductorConfiguration CurrentConfiguration { get; private set; }

        public ConfigurationManager(FilesystemManager pFilesystemManager)
        {
            _filesystemManager = pFilesystemManager;
        }

        public override void Initialize()
        {
            //load configuration file
            var config = LoadConfig();
            if (config == null)
                CreateConfigIfnotExists();
            else
            {
                CurrentConfiguration = config;
                NotifyNewLogMessageEvent("Configuration loaded");
            }
                
            base.Initialize();
        }

        public void UpdateConfiguration(ConductorConfiguration pNewConfig)
        {
            //only update certain fields
            //update reserve nodes
            CurrentConfiguration.ReserveNodes = pNewConfig.ReserveNodes;

            //update versions
            foreach (var remoteVersion in pNewConfig.Versions)
            {
                bool matchFound = false;
                foreach (var localVersion in CurrentConfiguration.Versions)
                {
                    //find match
                    if(remoteVersion.ToString() == localVersion.ToString())
                    {
                        //only target models is editable
                        localVersion.TargetModels = remoteVersion.TargetModels;
                        matchFound = true;
                        break;
                    }
                }

                //if no local match found, add new version
                if(!matchFound)
                {
                    CurrentConfiguration.Versions.Add(remoteVersion);
                    NotifyNewLogMessageEvent($"New remote configuration detected, added {remoteVersion.ToString()} to list.");
                    //TODO file handlings
                }                   
            }
            //if no remote match found, assume version got deleted
            var deletedVersions = CurrentConfiguration.Versions.Where(t => !pNewConfig.Versions.Any(u => u.ToString() == t.ToString()));
            if(deletedVersions.Any())
            {
                foreach(var deletedVersion in deletedVersions)
                {
                    CurrentConfiguration.Versions.Remove(deletedVersion);
                    NotifyNewLogMessageEvent($"Remote version missing, assuming deletion. Removed {deletedVersion.ToString()} from list.");
                }
            }

            CurrentConfiguration.LastUpdated = DateTime.Now;
            SaveConfig();
        }

        public void SaveConfig()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ConductorConfiguration));
            using (System.IO.FileStream stream = new System.IO.FileStream(configFilePath, System.IO.FileMode.Create))
            {
                serializer.Serialize(stream, CurrentConfiguration);
                stream.Close();
            }
        }

        public void SaveLocalVersions(List<Conductor_Shared.Version> pVersions)
        {
            foreach (var version in pVersions)
                _filesystemManager.SaveVersionToFile(version);
        }

        ConductorConfiguration LoadConfig()
        {
            if (!System.IO.File.Exists(configFilePath))
                return null;
             
            XmlSerializer serializer = new XmlSerializer(typeof(ConductorConfiguration));
            ConductorConfiguration result;
            using (System.IO.FileStream stream = new System.IO.FileStream(configFilePath, System.IO.FileMode.Open))
            {
                result = serializer.Deserialize(stream) as ConductorConfiguration;
                stream.Close();
            }
            return result;
        }

        void CreateConfigIfnotExists()
        {
            if (!System.IO.File.Exists(configFilePath))
            {
                CurrentConfiguration = CreateDefaultConfig();
                SaveConfig();
            }
        }

        ConductorConfiguration CreateDefaultConfig()
        {
            var config = new ConductorConfiguration()
            {
                ReserveNodes = 0
                //remaining data gets fetched from current structure
            };

            NotifyNewLogMessageEvent("Default configuration created");
            return config;
        }
    }
}
