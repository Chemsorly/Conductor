using Conductor_Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Conductor_Client.Client
{
    public static class Training
    {
        public static TrainingResultPackage RunTraining(DirectoryInfo pWorkingDirectory,
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
            pClientStatus.CurrentWorkParameters = pWorkPackage.Version.TrainingCommands.First().Parameters;
            pSendStatusUpdate(pClientStatus);

            //create result package
            var trainingResultPackage = new TrainingResultPackage()
            {
                InWorkPackage = pWorkPackage,
                MachineData = Machine.Machine.GetMachineData()
            };

            //run process
            pNotifyLogMessageEvent("[Log] Create worker process.");
            DateTime startTime = DateTime.UtcNow;
            String errorMessage = String.Empty;
            foreach (var command in pWorkPackage.Version.TrainingCommands)
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
                    UseShellExecute = false
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
                            if (args.Data != null)
                            {
                                if (Regex.IsMatch(args.Data, @"loss: nan - val_loss: nan"))
                                    errorMessage = "NaN found in training output";
                                else if (Regex.IsMatch(args.Data, @"utility.exceptions.ConductorError"))
                                    //e.g. utility.exceptions.ConductorError: __EncodePrediction() takes 6 positional arguments but 7 were given
                                    errorMessage = "Error found in training output";

                                if (!String.IsNullOrWhiteSpace(errorMessage))
                                {
                                    pNotifyLogMessageEvent($"{errorMessage}. Aborting!: {args.Data}");
                                    process.Kill();
                                }
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
                catch (Exception) { throw; }
            }
            pNotifyLogMessageEvent("[Log] Process finished.");
            if (!String.IsNullOrWhiteSpace(errorMessage))
                throw new Exception(errorMessage);

            trainingResultPackage.ClientStatusAtEnd = pClientStatus;
            trainingResultPackage.DurationTime = DateTime.UtcNow - startTime;
            trainingResultPackage.FinishTimestamp = DateTime.UtcNow;

            //get results
            return trainingResultPackage;
        }
    }
}
