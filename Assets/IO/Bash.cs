using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using EL = Constants.ErrorLevel;
using TID = Constants.TaskID;
using System.Threading.Tasks;
using System.Text;

public static class Bash {

	static Dictionary<int, Process> processDict = new Dictionary<int, Process>();

	public static Process InteractiveProcess(
		string command, 
		DataReceivedEventHandler outputReceivedHandler, 
		DataReceivedEventHandler errorReceivedHandler, 
		string directory=""
	) {

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

		StartInfo.RedirectStandardOutput = true;
		process.OutputDataReceived += outputReceivedHandler;

		StartInfo.RedirectStandardError = true;
		process.ErrorDataReceived += errorReceivedHandler;

		StartInfo.RedirectStandardInput = true;

		process.StartInfo = StartInfo;

		process.Start();

		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		int pid = process.Id;
		processDict[pid] = process;
		
		return process;

	}

	public static Process StartBashProcess(string command, string directory="", bool logOutput=false, bool logError=false, bool redirectStandardInput=false) {

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

		StartInfo.RedirectStandardInput = redirectStandardInput;

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
	}

	public static bool CommandExists(string command) {
		string commandCheck = string.Format("which {0}", command);

		Process process;
		try {
			process = StartBashProcess(commandCheck);
			process.WaitForExit();

		} catch (System.ComponentModel.Win32Exception) {
			CustomLogger.LogFormat(
				EL.WARNING,
				$"Bash not installed - cannot use external command '{command}'"
			);
			return false;
		}

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

	public class ExternalCommand {

		public string name;
		public string command;
		public string commandFormat;
		public string commandPath;
		public string workingDirectory;
		public string basename;
		public string inputFileFormat;
		public string outputFileFormat;

		string inputSuffix;
		string outputSuffix;
		string suffix;

		List<string> options;
		Dictionary<string, string> environmentVariables;


		public string GetWorkingPath() => Path.Combine(Settings.projectPath, workingDirectory);
		public string GetInputName() => basename + suffix + inputSuffix + inputFileFormat;
		public string GetOutputName() => basename + suffix + outputSuffix + outputFileFormat;
		public string GetFailedOutputName() => basename + suffix + outputSuffix + "_failed." + outputFileFormat;
		public string GetInputPath() => Path.Combine(GetWorkingPath(), GetInputName());
		public string GetOutputPath() => Path.Combine(GetWorkingPath(), GetOutputName());
		public string GetFailedOutputPath() => Path.Combine(GetWorkingPath(), GetFailedOutputName());

		public string GetExecutable() => Path.Combine(commandPath, command);
		public string GetCommand(string inputPath=null, string outputPath=null) {
			string OPTIONS = string.Join(" ", options);
			string IN = inputPath ?? GetInputPath();
			string OUT = outputPath ??  GetOutputPath();
			return commandFormat
				.Replace("{COMMAND}", GetExecutable())
				.Replace("{OPTIONS}", string.Join(" ", options))
				.Replace("{IN}", IN)
				.Replace("{OUT}", OUT);
		}
		public bool succeeded => result.ExitCode == 0;
		public ProcessResult result = new ProcessResult();

		public ExternalCommand(
			string name,
			string command,
			string commandPath,
			string commandFormat,
			string workingDirectory,
			string basename,
			string inputFileFormat,
			string outputFileFormat,
			string inputSuffix,
			string outputSuffix,
			List<string> options,
			Dictionary<string, string> environmentVariables
		) {

			this.name = name;	
			this.command = command;	
			this.commandPath = commandPath;
			this.commandFormat = commandFormat;	
			this.workingDirectory = workingDirectory;	
			this.basename = basename;	
			this.inputFileFormat = inputFileFormat;	
			this.outputFileFormat = outputFileFormat;
			this.inputSuffix = inputSuffix;
			this.outputSuffix = outputSuffix;
			this.options = options;	
			this.environmentVariables = environmentVariables;	

			string workingPath = GetWorkingPath();
			if (!Directory.Exists(workingPath)) {
				Directory.CreateDirectory(workingPath);
			}
		}

		public static IEnumerator FromXML(XElement externalCommandX, Dictionary<string, ExternalCommand> externalCommands) {
			
			string ProcessFormat(string input) {
				return string.IsNullOrWhiteSpace(input) 
					? "" 
					: input.StartsWith(".")
						? input
						: "." + input;
			}

			string name = FileIO.ParseXMLAttrString(externalCommandX, "name", "");
			if (string.IsNullOrWhiteSpace(name)) {
				throw new System.Exception("name attribute is missing in external command!");
			}

			string command = FileIO.ParseXMLString(externalCommandX, "command", "");
			string commandPath = FileIO.ParseXMLString(externalCommandX, "commandPath", "");

			string commandFormat = FileIO.ParseXMLString(externalCommandX, "commandFormat", "{COMMAND} {OPTIONS} {IN} {OUT}");
			string workingDirectory = FileIO.ParseXMLString(externalCommandX, "workingDirectory", "");

			string inputFileFormat = ProcessFormat(
				FileIO.ParseXMLString(externalCommandX, "inputFileFormat", "")
			);
			string outputFileFormat = ProcessFormat(
				FileIO.ParseXMLString(externalCommandX, "outputFileFormat", "")
			);

			string inputSuffix = FileIO.ParseXMLString(externalCommandX, "inputSuffix", "");
			string outputSuffix = FileIO.ParseXMLString(externalCommandX, "outputSuffix", "");

			string basename = FileIO.ParseXMLString(externalCommandX, "basename", "geo");

			List<string> options;
			try {
				options = FileIO.ParseXMLStringList(externalCommandX, "options", "option");
			} catch {
				options = new List<string>();
			}

			Dictionary<string, string> environmentVariables;
			try {
				environmentVariables = FileIO.ParseXMLStringDictionary(externalCommandX, "environmentVariables", "var", "key");
			} catch {
				environmentVariables = new Dictionary<string, string>();
			}

			ExternalCommand externalCommand = new ExternalCommand(
				name,
				command,
				commandPath,
				commandFormat,
				workingDirectory,
				basename,
				inputFileFormat,
				outputFileFormat,
				inputSuffix,
				outputSuffix,
				options,
				environmentVariables
			);

			bool ignore = externalCommandX.Element("ignore") != null;
			if (!ignore && (string.IsNullOrWhiteSpace(command) || !CommandExists(externalCommand.GetExecutable()))) {
				yield return externalCommand.UserSetCommandPath();
				if (command != "") {
					externalCommandX.Element("command").Remove();
					externalCommandX.Element("commandPath")?.Remove();
					externalCommandX.Add(new XElement("command", externalCommand.command));
					externalCommandX.Add(new XElement("commandPath", externalCommand.commandPath));;
				} else {
					externalCommandX.Add(new XElement("ignore"));
					yield break;
				}
			}

			externalCommands[name] = externalCommand;
		}

		public void CheckCommand() {
			if (string.IsNullOrWhiteSpace(command)) {
				throw new System.Exception(string.Format(
					"External Command '{0}' is empty in Project Settings!",
					name
				));
			}
			if (!CommandExists(GetExecutable())) {
				throw new System.Exception(string.Format(
					"External Command '{0}' is not exectutable!",
					GetExecutable()
				));
			}
		}

		IEnumerator UserSetCommandPath() {
			FileSelector fileSelector = FileSelector.main;
			
			yield return fileSelector.Initialise(string.Format("Select executable file: {0}", name));

			string path = "";
			while (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
				while (!fileSelector.userResponded) {
					yield return null;
				}

				if (fileSelector.cancelled) {
					command = "";
					commandPath = "";
					yield break;
				}

				//Got a non-cancelled response from the user
				path = fileSelector.confirmedText;
			}

			fileSelector.Hide();

			yield return null;
			
			command = Path.GetFileName(path);
			commandPath = Path.GetDirectoryName(path);
		}

		public void SetSuffix(string suffix) {
			this.suffix = suffix;
		}

		public void Initialise() {
			foreach ((string key, string value) in environmentVariables) {
				System.Environment.SetEnvironmentVariable(key, value);
			}
		}

		public IEnumerator Execute(
			TID taskID, 
			bool logOutput,
			bool logError,
			float waitTime,
			string inputPath=null,
			string outputPath=null
		) {

			inputPath = inputPath ?? GetInputPath();
			outputPath = outputPath ?? GetOutputPath();
				
        	NotificationBar.SetTaskProgress(taskID, 0f);
			
			result = new ProcessResult();

			IEnumerator processEnumerator = ExecuteShellCommand(
				GetCommand(inputPath, outputPath), 
				result, 
				GetWorkingPath(), 
				logOutput, 
				logError
			);
			
			float progress = 0.1f;
			while (processEnumerator.MoveNext()) {
				//Show that external command is running
				NotificationBar.SetTaskProgress(taskID, progress);
				progress = progress < 0.9f? progress + 0.01f : progress;
				yield return new WaitForSeconds(waitTime);
			}


			if (result.ExitCode != 0) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"{0} failed!", 
					command
				);
			}
			NotificationBar.ClearTask(taskID);

		}

		public IEnumerator WriteInputAndExecute(
			Geometry geometry, 
			TID taskID, 
			bool writeConnectivity,
			bool logOutput,
			bool logError,
			float waitTime,
			string inputPath=null,
			string outputPath=null
		) {	

			inputPath = inputPath ?? GetInputPath();
			outputPath = outputPath ?? GetOutputPath();

			FileWriter fileWriter;
			try {
				fileWriter = new FileWriter(geometry, inputPath, writeConnectivity);
			} catch (System.ArgumentException e) {
				CustomLogger.LogFormat(
					EL.ERROR,
					"Failed to run command '{0}'! {1}",
					command,
					e.Message
				);
				yield break;
			}
			yield return fileWriter.WriteFile();
			yield return Execute(taskID, logOutput, logError, waitTime, inputPath, outputPath);

		}
	}
}
