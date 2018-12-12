using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Conductor_Client.Client;

namespace Conductor_Client
{
    class Program
    {
        private static Client.SignalRManager _client;


        static void Main(string[] args)
        {
            //enforce decimal encoding
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            CultureInfo.DefaultThreadCurrentCulture = customCulture;

            //envs
            var envs = Machine.Machine.GetEnvironmentVariables();
            foreach (var env in envs)
                Console.WriteLine($"ENV: {env.Key}={env.Value}");

            //get target host
            String targetHost = String.Empty;
            if (envs.Any(t => t.Key == "CONDUCTOR_HOST"))
                targetHost = envs["CONDUCTOR_HOST"];

            //if no target host specified in env, check args
            if (String.IsNullOrWhiteSpace(targetHost))
            {
                if (!args.Any())
                {
                    Console.WriteLine("No args specified");
                    return;
                }
                targetHost = args[0];
            }

            //fetch machine data from env variables
            var machinedata = Machine.Machine.GetMachineData();

            //container version
            Console.WriteLine($"Container version: {machinedata.ContainerVersion}");
            Console.WriteLine($"OS: {machinedata.OperatingSystem}");
            Console.WriteLine($"Processing Unit: {machinedata.ProcessingUnit}");
            Console.WriteLine($"Name: {machinedata.Name}");



            //start
            Console.WriteLine($"Target host: {targetHost}");
            Task.Factory.StartNew(() =>
            {
                _client = new SignalRManager(machinedata);
                _client.LogEvent += _client_LogEvent;
                _client.Initialize($"http://{CleanHoststring(targetHost)}/signalr");
            });
            Console.ReadKey();
        }

        private static void _client_LogEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static String CleanHoststring(String pInput)
        {
            //remove http://, https:// 
            return pInput.Replace("http://", String.Empty).Replace("https://", String.Empty);
        }
    }
}
