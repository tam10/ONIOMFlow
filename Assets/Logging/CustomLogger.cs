using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using EL = Constants.ErrorLevel;

public static class CustomLogger {
    
    public static EL logErrorLevel = EL.INFO;
    
    private static StreamWriter logStream;
    private static string _logPath;
    public static string logPath {
        get {return _logPath;}
        set {
            _logPath = value;
            File.Delete(_logPath);
            logStream = new StreamWriter(File.Open(_logPath, System.IO.FileMode.Create));
            logStream.Write(string.Format("Initialised at [{0}]{1}", DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss"), FileIO.newLine));
            logStream.Flush();
        }
    }

    static CustomLogger() {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += PlayModeChanged;
        #else
            AppDomain.CurrentDomain.ProcessExit += Close;
        #endif
    }

    #if UNITY_EDITOR
    static void PlayModeChanged(UnityEditor.PlayModeStateChange stateChange) {
        if (stateChange == UnityEditor.PlayModeStateChange.ExitingPlayMode) {
            try {
                logStream.Write(string.Format("Process closed{0}", FileIO.newLine));
            } catch {}
            logStream.Flush();
            logStream.Close();
        }
    }
    #endif

    static void Close(object sender, EventArgs e) {
        Debug.Log("STOP");
        try {
            logStream.Write(string.Format("Process closed{0}", FileIO.newLine));
        } catch {}
        logStream.Flush();
        logStream.Close();
    }

    private static Dictionary<EL, string> prefixDict = new Dictionary<EL, string> {
        {EL.NULL, "NULL"},
        {EL.FATAL, "FATAL"},
        {EL.ERROR, "ERROR"},
        {EL.WARNING, "WARN "},
        {EL.NOTIFICATION, "NOTIF"},
        {EL.INFO, "INFO "},
        {EL.VERBOSE, "VRBOS"},
        {EL.DEBUG, "DEBUG"}
    };

    public static void Log(EL errorLevel, string message) {
        if (errorLevel <= logErrorLevel) {
            logStream.Write(
                "[{0}]: [{1}] {2}{3}",
                TimeSpan.FromSeconds(Time.realtimeSinceStartup).ToString(@"hh\:mm\:ss\:fff"),
                prefixDict[errorLevel],
                message, 
                FileIO.newLine
            );
            logStream.Flush();
        }
        if (errorLevel <= EL.ERROR && logErrorLevel >= EL.DEBUG) {
            System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(true);
            logStream.Write(
                "Stack Trace: {0}{1}",
                trace.ToString(), 
                FileIO.newLine
            );
            logStream.Flush();
        }
        switch (errorLevel) {
            case EL.ERROR:
                NotificationBar.AddError(message);
                break;
            case EL.WARNING:
                NotificationBar.AddWarning(message);
                break;
            case EL.NOTIFICATION:
                NotificationBar.AddInfo(message);
                break;
        }
    }

    //Use the messageDelegate version of Log if evaluation is required.
    //This means it only evaluates if the error level is low enough
    public static void Log(EL errorLevel, Func<string> messageDelegate) {
        if (errorLevel <= logErrorLevel) {
            Log(errorLevel, messageDelegate());
        }
    }

    public static void LogFormat(EL errorLevel, string format, params object[] args) {

        string message = string.Format(format, args);
        if (errorLevel <= logErrorLevel) {
            logStream.Write(
               "[{0}]: [{1}] {2}{3}",
                TimeSpan.FromSeconds(Time.realtimeSinceStartup).ToString(@"hh\:mm\:ss\:fff"),
                prefixDict[errorLevel],
                message, 
                FileIO.newLine
            );
            logStream.Flush();
        }
        if (errorLevel <= EL.ERROR && logErrorLevel >= EL.DEBUG) {
            System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(true);
            logStream.Write(
                "Stack Trace: {0}{1}",
                trace.ToString(), 
                FileIO.newLine
            );
            logStream.Flush();
        }

        switch (errorLevel) {
            case EL.ERROR:
                NotificationBar.AddError(message);
                break;
            case EL.WARNING:
                NotificationBar.AddWarning(message);
                break;
            case EL.NOTIFICATION:
                NotificationBar.AddInfo(message);
                break;
        }
    }

    public static void LogOutput(string message) {
        logStream.Write(message + FileIO.newLine);
        logStream.Flush();
    }
    
    public static void LogOutput(string format, params object[] args) {
        logStream.Write(string.Format(format, args) + FileIO.newLine);
        logStream.Flush();
    }         

    public static void LogFormat(EL errorLevel, string format, Func<object[]> argsDelegate) {
        if (errorLevel <= logErrorLevel) {
            LogFormat(errorLevel, format, argsDelegate());
        }
    }

}
