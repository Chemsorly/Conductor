using Conductor_Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Conductor_Client.Client
{
    public static class Prediction
    {
        public static PredictionResultPackage RunPrediction(DirectoryInfo pWorkingDirectory,
            WorkPackage pWorkPackage,
            ref ClientStatus pClientStatus,
            Action<ClientStatus> pSendStatusUpdate,
            Action<String> pNotifyLogMessageEvent,
            MachineData pMachine)
        {
            //init client status
            pClientStatus.IsWorking = true;
            pClientStatus.CurrentEpoch = 0;
            pClientStatus.LastEpochDuration = "none";
            pClientStatus.CurrentWorkParameters = pWorkPackage.Version.PredictionCommands.First().Parameters;
            pSendStatusUpdate(pClientStatus);

            //create result package
            var predictionResultPackage = new PredictionResultPackage()
            {
                InWorkPackage = pWorkPackage,
                MachineData = Machine.Machine.GetMachineData()
            };

            //run process
            pNotifyLogMessageEvent("[Log] Create worker process.");
            DateTime startTime = DateTime.UtcNow;
            SinglePrediction predictionResult = null;
            foreach (var command in pWorkPackage.Version.PredictionCommands)
            {
                pNotifyLogMessageEvent($"[Log] Create process for: {command.FileName} {command.Arguments} in {pWorkingDirectory.FullName}");
                var startInfo = new ProcessStartInfo()
                {
                    FileName = command.FileName,
                    WorkingDirectory = pWorkingDirectory.FullName,
                    Arguments = command.Arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = false,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                //set env
                if (pMachine.ProcessingUnit == Conductor_Shared.Enums.ProcessingUnitEnum.CPU)
                {
                    startInfo.Environment.Add("CONDUCTOR_TYPE", "cpu");
                    Console.WriteLine($"process env CONDUCTOR_TYPE set to 'cpu'");
                }
                else if (pMachine.ProcessingUnit == Conductor_Shared.Enums.ProcessingUnitEnum.GPU)
                {
                    startInfo.Environment.Add("CONDUCTOR_TYPE", "gpu");
                    Console.WriteLine($"process env CONDUCTOR_TYPE set to 'gpu'");
                }

                try
                {
                    using (var process = Process.Start(startInfo))
                    {
                        process.OutputDataReceived += (sender, args) =>
                        {
                            //intercept out stream for prediction in log
                            if (args.Data != null && Regex.IsMatch(args.Data, @"PredictedValue=-?\d+.\d+"))
                            {
                                var match = Regex.Match(args.Data, @"PredictedValue=-?\d+.\d+");
                                var split = match.Value.Split('=');
                                predictionResult = new SinglePrediction()
                                {
                                    PredictedValue = Double.Parse(split[1])
                                };
                            }
                            pNotifyLogMessageEvent(args.Data);
                        };
                        process.ErrorDataReceived += (sender, args) => pNotifyLogMessageEvent(args.Data);
                        pNotifyLogMessageEvent($"[Log] Starting process for: {command.FileName} {command.Arguments}");
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit();
                        pNotifyLogMessageEvent($"[Log] Finished process for: {command.FileName} {command.Arguments}");
                    }
                }
                catch(Exception) { throw; }
            }
            pNotifyLogMessageEvent("[Log] Process finished.");

            predictionResultPackage.ClientStatusAtEnd = pClientStatus;
            predictionResultPackage.DurationTime = DateTime.UtcNow - startTime;
            predictionResultPackage.FinishTimestamp = DateTime.UtcNow;

            if (predictionResult != null)
                predictionResultPackage.Prediction = predictionResult;
            else
                throw new Exception("[ERROR] no prediction found in log");

            //get results
            return predictionResultPackage;
        }
    }
}
