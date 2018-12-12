using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Conductor_Shared;
using Conductor_Shared.Enums;

namespace Conductor_Client.Machine
{
    public class Machine
    {
        public static MachineData GetMachineData()
        {
            MachineData mdata = new MachineData();
            mdata.ContainerVersion = GetContainerVersion();
            mdata.OperatingSystem = GetOperatingSystem();
            mdata.ProcessingUnit = GetProcessingUnitType();
            mdata.Name = GetContainerName();          

            return mdata;
        }

        public static Dictionary<String,String> GetEnvironmentVariables()
        {
            var envs = System.Environment.GetEnvironmentVariables();
            Dictionary<String, String> result = new Dictionary<string, string>();
            foreach (DictionaryEntry env in envs)
                result.Add(env.Key.ToString(), env.Value.ToString());
            return result;
        }

        static String GetContainerName()
        {
            return System.Net.Dns.GetHostName();
        }

        static OSEnum GetOperatingSystem()
        {
            var osvar = Environment.GetEnvironmentVariable("CONDUCTOR_OS");
            if (osvar == "ubuntu")
                return OSEnum.Ubuntu;
            else if (osvar == "windows")
                return OSEnum.Windows;
            else
                return OSEnum.undefined;
        }

        static ProcessingUnitEnum GetProcessingUnitType()
        {
            var puvar = Environment.GetEnvironmentVariable("CONDUCTOR_TYPE");
            if (puvar == "cpu")
                return ProcessingUnitEnum.CPU;
            else if (puvar == "gpu")
                return ProcessingUnitEnum.GPU;
            else
                return ProcessingUnitEnum.undefined;
        }

        static String GetContainerVersion()
        {
            return Environment.GetEnvironmentVariable("CONDUCTOR_VERSION") ?? "dev";
        }
    }
}
