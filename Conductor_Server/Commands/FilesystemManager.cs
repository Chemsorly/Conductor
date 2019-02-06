using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Conductor_Server.Utility;
using Conductor_Shared;
using Version = Conductor_Shared.Version;

namespace Conductor_Server.Commands
{
    public class FilesystemManager : ManagerBase
    {
        //ENV RUNTIMEENV="docker"
        //System.IO.Path.Combine(Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)? "AppData" : "Home"), "Conductor");
        private String BasePath;
        public String AssetsPath => System.IO.Path.Combine(BasePath, "assets");
        public String ConfigPath => System.IO.Path.Combine(BasePath, "config");
        public String LogPath => System.IO.Path.Combine(ConfigPath, "log.txt");


        public override void Initialize()
        {
            //check runtime environment
            if (Environment.GetEnvironmentVariable("RUNTIMEENV") == "docker")
                //if app runs in docker, use current dir as workdir
                BasePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "workdir");
            else
                //if app runs natively on windows or linux, use appdata or home folder
                BasePath = System.IO.Path.Combine(Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "AppData" : "Home"), "Conductor");

            //check dirs
            CreateIfNotExists(BasePath);
            CreateIfNotExists(AssetsPath);
            CreateIfNotExists(ConfigPath);

            base.Initialize();
        }

        public bool CheckIfFileExists(Version pVersion, String pGUID)
        {
            //check directory
            var dir = System.IO.Path.Combine(AssetsPath, pVersion.ToString(), "models", pGUID);
            if (!System.IO.Directory.Exists(dir))
                return false;

            //check if file exists
            return new DirectoryInfo(dir).GetFiles().Any(t => t.Extension == "h5");
        }

        void CreateIfNotExists(String pPath)
        {
            if (!System.IO.Directory.Exists(pPath))
            {
                NotifyNewLogMessageEvent($"Directory {pPath} does not exist. Creating...");
                System.IO.Directory.CreateDirectory(pPath);
                NotifyNewLogMessageEvent($"Directory {pPath} created");
            }
        }

        public DirectoryInfo GetVersionRootDirectory(Version pVersion)
        {
            var path = System.IO.Path.Combine(AssetsPath, pVersion.ToString());
            CreateIfNotExists(path);
            return new DirectoryInfo(path);
        }

        public DirectoryInfo GetVersionModelsDirectory(Version pVersion)
        {
            var path = System.IO.Path.Combine(AssetsPath, pVersion.ToString(), "models");
            CreateIfNotExists(path);
            return new DirectoryInfo(path);
        }

        public DirectoryInfo GetVersionErrorlogsDirectory(Version pVersion)
        {
            var path = System.IO.Path.Combine(AssetsPath, pVersion.ToString(), "errorlogs");
            CreateIfNotExists(path);
            return new DirectoryInfo(path);
        }

        public List<String> GetModelResults(Version pVersion, VersionStatusModel pModel)
        {
            if(!String.IsNullOrWhiteSpace(pModel.ModelFileName))
            {
                //get dir
                var dir = GetVersionModelsDirectory(pVersion);

                //get results file (*.csv)
                var modelsdir = System.IO.Path.Combine(dir.FullName, pModel.ModelFileName);
                var file = new DirectoryInfo(modelsdir).GetFiles().FirstOrDefault(t => t.Name.Contains("results.csv"));

                if(file != null)
                    return System.IO.File.ReadAllLines(file.FullName).ToList();
            }
            return null;
        }

        public MetaStruct WriteResultsToFilesystem(TrainingResultPackage pResults)
        {

            //write transmitted files
            var modelsdir = System.IO.Path.Combine(GetVersionModelsDirectory(pResults.InWorkPackage.Version).FullName,pResults.InWorkPackage.GUID.ToString());
            CreateIfNotExists(modelsdir);
            foreach (var file in pResults.ResultFiles)
            {
                var filepath = new FileInfo(System.IO.Path.Combine(modelsdir,file.DirectoryStructure,file.Filename));
                CreateIfNotExists(filepath.Directory.FullName);
                System.IO.File.WriteAllBytes(filepath.FullName, file.FileData);
            }

            //write meta file
            MetaStruct metaData = new MetaStruct
            {
                Duration = pResults.DurationTime,
                Epochs = pResults.ClientStatusAtEnd.CurrentEpoch,
                LastEpochDuration = pResults.ClientStatusAtEnd.LastEpochDuration,
                NodeName = pResults.MachineData.Name,
                OS = pResults.MachineData.OperatingSystem.ToString(),
                Version = pResults.MachineData.ContainerVersion,
                ProcessingUnit = pResults.MachineData.ProcessingUnit.ToString(),
                Timestamp = pResults.FinishTimestamp,
            };
            using (var filestream = new FileStream(System.IO.Path.Combine(modelsdir,"meta.xml"), FileMode.CreateNew))
            {
                var xmlserializer = new XmlSerializer(typeof(MetaStruct));
                xmlserializer.Serialize(filestream, metaData);
                filestream.Close();
            }
            //write logfile
            System.IO.File.WriteAllLines(System.IO.Path.Combine(modelsdir, "console.log"), pResults.OutLog);

            NotifyNewLogMessageEvent($"Saved results to file for {pResults.InWorkPackage.GUID}");
            return metaData;
        }

        object logfile_lock = new object();
        public void WriteLogMessageToFilesystem(String pMessage)
        {
            try
            {
                lock(logfile_lock)
                    System.IO.File.AppendAllText(LogPath, pMessage + Environment.NewLine);
            }
            catch(Exception)
            {
                //fail silently
            }            
        }

        public void WriteErrorlogToFilesystem(Version pVersion, List<String> pLogmessage, Guid pErrorguid)
        {
            var path = this.GetVersionErrorlogsDirectory(pVersion).FullName;

            //create up to 10 error files per guid
            for(int i = 0; i < 10; i++)
            {
                var filepath = System.IO.Path.Combine(path, $"{pErrorguid.ToString()}-{i}.log");
                if(!System.IO.File.Exists(filepath))
                {
                    //write if file not exist
                    System.IO.File.WriteAllLines(filepath, pLogmessage);
                    break;
                }
            }
        }

        private String CleanParameters(String pParameters)
        {
            return pParameters.Replace(",", ".");
        }

        public Version LoadVersionFromFile(FileInfo pFile)
        {
            if (!System.IO.File.Exists(pFile.FullName))
                return null;

            XmlSerializer deserializer = new XmlSerializer(typeof(Version));
            Version result;
            using (System.IO.FileStream stream = new System.IO.FileStream(pFile.FullName, System.IO.FileMode.Open))
            {
                result = deserializer.Deserialize(stream) as Version;
                stream.Close();
            }

            //overwrite name field, since it's provided via directory name
            if (result != null)
                result.Name = pFile.Directory.Name;

            return result;
        }

        public void SaveVersionToFile(Version pVersion)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Conductor_Shared.Version));
            var path = GetVersionRootDirectory(pVersion).FullName;
            using (System.IO.FileStream stream = new System.IO.FileStream(Path.Combine(path,"version.xml"), System.IO.FileMode.Create))
            {
                serializer.Serialize(stream, pVersion);
                stream.Close();
            }
        }
    }
    public class MetaStruct
    {
        public TimeSpan Duration { get; set; }
        public int Epochs { get; set; }
        public String LastEpochDuration { get; set; }
        public String NodeName { get; set; }
        public String Version { get; set; }
        public String OS { get; set; }
        public String ProcessingUnit { get; set; }
        public DateTime Timestamp { get; set; }
        public VersionStatusModel PredictionModel { get; set; }
    }
}
