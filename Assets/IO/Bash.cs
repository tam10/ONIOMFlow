using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EL = Constants.ErrorLevel;
using System.Threading.Tasks;
using System.Text;

public static class Bash {

	static Dictionary<int, Process> processDict = new Dictionary<int, Process>();

	public static Process StartBashProcess(string command, string directory="", bool logOutput=false, bool logError=false) {

		List<string> bashRCs = new List<string> {".bashrc", ".bash_profile"};
		foreach (string bashRC in bashRCs) {
			string bashRCPath = Path.Combine(Settings.home, bashRC);
			if (File.Exists(bashRCPath)) {
				command = string.Format("source {0}; {1}", bashRCPath, command);
				break;
			} else {
				UnityEngine.Debug.LogFormat("{0} doesn't exist", bashRCPath);
			}
		}

		command = string.Format ("-c \'{0}\'", command);

		Process process = new Process();
		ProcessStartInfo StartInfo = new ProcessStartInfo();
		StartInfo.FileName = "/bin/bash";

		foreach (DictionaryEntry de in System.Environment.GetEnvironmentVariables()) {
			StartInfo.EnvironmentVariables[(string)de.Key] = (string)de.Value;
		}

		StartInfo.Arguments = command;
		StartInfo.WorkingDirectory = directory == "" ? Settings.tempFolder : directory;
		StartInfo.CreateNoWindow = true;
		StartInfo.UseShellExecute = false;
		process.EnableRaisingEvents = false;

		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Running command: {0}",
			command
		);

		CustomLogger.LogFormat(
			EL.VERBOSE,
			"Working Directory: {0}. Log Output: {1}. Log Error: {2}",
			command,
			logOutput,
			logError
		);

		StartInfo.RedirectStandardOutput = true;
		if (logOutput) {
			process.OutputDataReceived += new DataReceivedEventHandler(
				(s, e) => {if (e.Data != null) {CustomLogger.LogOutput(e.Data);}}
			);
		}

		StartInfo.RedirectStandardError = true;
		if (logError) {
			process.ErrorDataReceived += new DataReceivedEventHandler(
				(s, e) => {if (e.Data != null) {CustomLogger.LogOutput(e.Data);}}
			);
		}

		process.StartInfo = StartInfo;
		process.Start();

		if (logOutput) {
			process.BeginOutputReadLine();
		}

		if (logError) {
			process.BeginErrorReadLine();
		}
		

		int pid = process.Id;
		processDict[pid] = process;

		return process;

		//ProcessStartInfo procInfo = new ProcessStartInfo {
		//	UseShellExecute = false,
		//	RedirectStandardOutput = true,
		//	RedirectStandardError = true,
		//	CreateNoWindow = false,
		//	FileName = "/bin/bash",
		//	WorkingDirectory = directory == "" ? Settings.tempFolder : directory,
		//	Arguments = command
		//};
//
		//foreach (DictionaryEntry de in System.Environment.GetEnvironmentVariables()) {
		//	procInfo.EnvironmentVariables[(string)de.Key] = (string)de.Value;
		//}
//
		//Process process = new Process { StartInfo = procInfo };
//
		//process.Start();
//
		//if (logOutput) {
		//	process.OutputDataReceived += LogInfo;
		//	process.BeginOutputReadLine();
		//}
		//if (logError) {
		//	process.ErrorDataReceived += LogError;
		//	process.BeginErrorReadLine();
		//}
		//
		//int pid = process.Id;
		//processDict[pid] = process;
		//
		//return process;
	}

	public static bool CommandExists(string command) {
		string commandCheck = string.Format("which {0}", command);
		Process process = StartBashProcess(commandCheck);
		
        process.WaitForExit();

		string output = process.StandardOutput.ReadToEnd();

    	return (!string.IsNullOrEmpty(output));
	}

	private static void LogInfo(object sender, DataReceivedEventArgs eventArgs) {
		CustomLogger.Log(EL.INFO, eventArgs.Data);
	}

	private static void LogError(object sender, DataReceivedEventArgs eventArgs) {
		CustomLogger.Log(EL.ERROR, eventArgs.Data);
	}

	public static void CloseAllProcesses() {
		foreach (Process process in processDict.Values) {
			if (!process.HasExited)
				process.Close();
		}
	}

	public static IEnumerator ExecuteShellCommand(
		string command, 
		ProcessResult result,
		string directory="",
		bool logOutput=true, 
		bool logError=true
	) {

		List<string> bashRCs = new List<string> {".bashrc", ".bash_profile"};
		foreach (string bashRC in bashRCs) {
			string bashRCPath = Path.Combine(Settings.home, bashRC);
			if (File.Exists(bashRCPath)) {
				command = string.Format("source {0}; {1}", bashRCPath, command);
				break;
			} else {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Bash Profile File {0} doesn't exist", 
					bashRCPath
				);
			}
		}
		command = string.Format ("-c \'{0}\'", command);
		directory = directory == "" ? Settings.tempFolder : directory;

		CustomLogger.Log(EL.INFO, directory);

        using (Process process = new Process()) {
            process.StartInfo.FileName = "/bin/bash";
			process.StartInfo.WorkingDirectory = directory;
            process.StartInfo.Arguments = command;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            StringBuilder outputBuilder = new StringBuilder();
            StringBuilder errorBuilder = new StringBuilder();


            using (System.Threading.AutoResetEvent outputCloseEvent = new System.Threading.AutoResetEvent(false))
            using (System.Threading.AutoResetEvent errorCloseEvent = new System.Threading.AutoResetEvent(false)) {
                System.Threading.AutoResetEvent copyOutputCloseEvent = outputCloseEvent;

				if (logOutput) {
					process.OutputDataReceived += (s, e) => {
						// Output stream is closed (process completed)
						if (string.IsNullOrEmpty(e.Data)) {
							copyOutputCloseEvent.Set();
						} else {
							CustomLogger.LogOutput(
								e.Data
							);
						}
					};
				} else {
					process.OutputDataReceived += (s, e) => {
						// Output stream is closed (process completed)
						if (string.IsNullOrEmpty(e.Data)) {
							copyOutputCloseEvent.Set();
						} else {
							outputBuilder.AppendLine(e.Data);
						}
					};
				}

                System.Threading.AutoResetEvent copyErrorCloseEvent = errorCloseEvent;

				if (logError) {
					process.ErrorDataReceived += (s, e) => {
					// Error stream is closed (process completed)
						if (string.IsNullOrEmpty(e.Data)) {
							copyErrorCloseEvent.Set();
						} else {
							errorBuilder.AppendLine(e.Data);
							CustomLogger.LogOutput(
								e.Data
							);
						}
					};
				} else {
					process.ErrorDataReceived += (s, e) => {
					// Error stream is closed (process completed)
						if (string.IsNullOrEmpty(e.Data)) {
							copyErrorCloseEvent.Set();
						} else {
							errorBuilder.AppendLine(e.Data);
						}
					};
				}

                bool isStarted;

                try {
                    isStarted = process.Start();
					CustomLogger.LogFormat(
						EL.INFO,
						"Starting command (PID {0}): {1}",
						process.Id,
						command
					);
                } catch (System.Exception error) {
                    result.Completed = true;
                    result.ExitCode = -1;
                    result.Output = error.Message;

					CustomLogger.LogFormat(
						EL.ERROR,
						"Couldn't start process with command: {0}{1}{2}",
						command,
						FileIO.newLine,
						error.StackTrace
					);

                    isStarted = false;
                }

                if (isStarted) {
                    // Read the output stream first and then wait because deadlocks are possible
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

					while (!process.HasExited) {
						yield return null;
					}

					process.WaitForExit();

                    result.Completed = true;
                    result.ExitCode = process.ExitCode;

                    result.Output = $"{outputBuilder}{errorBuilder}";

					yield break;
                } else {
                    result.Completed = true;
                    result.ExitCode = -1;
					CustomLogger.LogFormat(
						EL.ERROR,
						"Couldn't start process with command: {0}",
						command
					);
				}
					
            }
        }

    }


    public class ProcessResult {
        public bool Completed = false;
        public int? ExitCode = -1;
        public string Output = "";
    }
}
