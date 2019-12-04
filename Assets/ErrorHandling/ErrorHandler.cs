namespace ErrorHandler
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using System;
    using System.Runtime.Serialization;
    using System.Security.Permissions;
    using RCID = Constants.ResidueCheckerID;
    using ACID = Constants.AtomCheckerID;
    using GIID = Constants.GeometryInterfaceID;
    using GIS = Constants.GeometryInterfaceStatus;
    using AID = Constants.ArrowID;
    using TID = Constants.TaskID;
    using BT = Constants.BondType;
    using RS = Constants.ResidueState;
    using RP = Constants.ResidueProperty;


    // IO
    
    [Serializable]
    public class FileTypeNotRecognisedException : Exception
    {
        //This occurs when something tries to read the results of a checker that hasn't been
        // attached to an AtomsChecker object
        private readonly string fileType;
        public FileTypeNotRecognisedException() {}
        public FileTypeNotRecognisedException(string message) : base(message) {}
        public FileTypeNotRecognisedException(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected FileTypeNotRecognisedException(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.fileType = info.GetString("FileType");
        }
        public FileTypeNotRecognisedException(string message, string fileType) : base(message) {
            this.fileType = fileType;
        }
        public string FileType { get {return this.fileType;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("FileType", this.FileType);
            base.GetObjectData(info, context);
        }
    }
    
    [Serializable]
    public class FileParserException : Exception
    {
        //This occurs when something tries to read the results of a checker that hasn't been
        // attached to an AtomsChecker object
        private readonly string path;
        private readonly int lineNumber;
        public FileParserException() {}
        public FileParserException(string message) : base(message) {}
        public FileParserException(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected FileParserException(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.path = info.GetString("Path");
            this.lineNumber = info.GetInt32("LineNumber");
        }
        public FileParserException(string message, string path, int lineNumber) : base(message) {
            this.path = path;
            this.lineNumber = lineNumber;
        }
        public string Path { get {return this.path;}}
        public int LineNumber { get {return this.lineNumber;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("Path", this.Path);
            info.AddValue("LineNumber", this.LineNumber);
            base.GetObjectData(info, context);
        }
    }
    
    [Serializable]
    public class ExternalCommandError : Exception
    {
        //This occurs when something tries to read the results of a checker that hasn't been
        // attached to an AtomsChecker object
        private readonly string command;        
        private readonly string stderr;
        public ExternalCommandError() {}
        public ExternalCommandError(string message) : base(message) {}
        public ExternalCommandError(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected ExternalCommandError(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.command = info.GetString("Command");
            this.stderr = info.GetString("Stderr");
        }
        public ExternalCommandError(string message, string command, string stderr) : base(message) {
            this.command = command;
            this.stderr = stderr;
        }
        public string Command { get {return this.command;}}
        public string Stderr { get {return this.stderr;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("Command", this.Command);
            info.AddValue("Stderr", this.Stderr);
            base.GetObjectData(info, context);
        }
    }
    
    [Serializable]
    public class CommandNotFoundException : Exception
    {
        //This occurs when something tries to read the results of a checker that hasn't been
        // attached to an AtomsChecker object
        private readonly string command;      
        public CommandNotFoundException() {}
        public CommandNotFoundException(string message) : base(message) {}
        public CommandNotFoundException(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected CommandNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.command = info.GetString("Command");
        }
        public CommandNotFoundException(string message, string command) : base(message) {
            this.command = command;
        }
        public string Command { get {return this.command;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("Command", this.Command);
            base.GetObjectData(info, context);
        }
    }


    // RESIDUES
    [Serializable]
    public class ResidueMismatchException : Exception
    {
        //This occurs when part of a residue is modified, changing its residue name.
        //e.g. reading an inconsistent file, adding an atom improperly
        private readonly int residueNum;
        private readonly string residueName;
        private readonly string newResidueName;
        public ResidueMismatchException() {}
        public ResidueMismatchException(string message) : base(message) {}
        public ResidueMismatchException(string message, Exception innerException) : base(message, innerException) {}

        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected ResidueMismatchException(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.residueNum = info.GetInt32("ResidueNum");
            this.residueName = info.GetString("residueName");
            this.newResidueName = info.GetString("newResidueName");
        }
        public ResidueMismatchException(string message, int residueNum, string residueName, string newResidueName) : base(message) {
            this.residueNum = residueNum;
            this.residueName = residueName;
            this.newResidueName = newResidueName;
        }
        public int ResidueNum { get {return this.residueNum;}}
        public string ResidueName { get {return this.residueName;}}
        public string NewResidueName { get {return this.newResidueName;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("ResidueNum", this.ResidueNum);
            info.AddValue("ResidueName", this.ResidueName);
            info.AddValue("NewResidueName", this.NewResidueName);
            base.GetObjectData(info, context);
        }
    }
    [Serializable]
    public class InvalidResiduePropertyID : Exception
    {
        //This occurs when part of a residue is modified, changing its residue name.
        //e.g. reading an inconsistent file, adding an atom improperly
        private readonly RP residueProperty;
        public InvalidResiduePropertyID() {}
        public InvalidResiduePropertyID(string message) : base(message) {}
        public InvalidResiduePropertyID(string message, Exception innerException) : base(message, innerException) {}

        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidResiduePropertyID(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.residueProperty = (RP)info.GetInt32("ResidueProperty");
        }
        public InvalidResiduePropertyID(string message, RP residueProperty) : base(message) {
            this.residueProperty = residueProperty;
        }
        public RP ResidueProperty { get {return this.residueProperty;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("ResidueProperty", (int)this.ResidueProperty);
            base.GetObjectData(info, context);
        }
    }

    //CHECKERS
    [Serializable]
    public class ResidueCheckerNotAttachedException : Exception
    {
        //This occurs when something tries to read the results of a checker that hasn't been
        // attached to an AtomsChecker object
        private readonly RCID residueCheckerID;
        private readonly string geometryInterfaceFullName;
        public ResidueCheckerNotAttachedException() {}
        public ResidueCheckerNotAttachedException(string message) : base(message) {}
        public ResidueCheckerNotAttachedException(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected ResidueCheckerNotAttachedException(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.residueCheckerID = (RCID)info.GetInt32("ResidueCheckerID");
            this.geometryInterfaceFullName = info.GetString("GeometryInterfaceFullName");
        }
        public ResidueCheckerNotAttachedException(string message, RCID residueCheckerID, string geometryInterfaceFullName) : base(message) {
            this.residueCheckerID = residueCheckerID;
            this.geometryInterfaceFullName = geometryInterfaceFullName;
        }
        public RCID ResidueCheckerID { get {return this.residueCheckerID;}}
        public string GeometryInterfaceFullName { get {return this.geometryInterfaceFullName;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("CheckName", (int)this.ResidueCheckerID);
            info.AddValue("GeometryInterfaceFullName", this.GeometryInterfaceFullName);
            base.GetObjectData(info, context);
        }
    }
    
    [Serializable]
    public class InvalidResidueCheckerID : Exception
    {
        //This occurs when something tries to read the results of a checker that hasn't been
        // attached to an AtomsChecker object
        private readonly RCID residueCheckerID;
        public InvalidResidueCheckerID() {}
        public InvalidResidueCheckerID(string message) : base(message) {}
        public InvalidResidueCheckerID(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidResidueCheckerID(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.residueCheckerID = (RCID)info.GetInt32("ResidueCheckerID");
        }
        public InvalidResidueCheckerID(string message, RCID residueCheckerID) : base(message) {
            this.residueCheckerID = residueCheckerID;
        }
        public RCID ResidueCheckerID { get {return this.residueCheckerID;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("ResidueCheckerID", (int)this.ResidueCheckerID);
            base.GetObjectData(info, context);
        }
    }
    
    [Serializable]
    public class InvalidAtomCheckerID : Exception
    {
        //This occurs when something tries to read the results of a checker that hasn't been
        // attached to an AtomsChecker object
        private readonly ACID atomCheckerID;
        public InvalidAtomCheckerID() {}
        public InvalidAtomCheckerID(string message) : base(message) {}
        public InvalidAtomCheckerID(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidAtomCheckerID(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.atomCheckerID = (ACID)info.GetInt32("AtomCheckerID");
        }
        public InvalidAtomCheckerID(string message, ACID atomCheckerID) : base(message) {
            this.atomCheckerID = atomCheckerID;
        }
        public ACID AtomCheckerID { get {return this.atomCheckerID;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("AtomCheckerID", (int)this.AtomCheckerID);
            base.GetObjectData(info, context);
        }
    }

    //ELEMENTS
    [Serializable]
    public class InvalidResidueElement : Exception
    {
        // This occurs when a residue has an invalid element
        // eg N in Water residue
        private readonly int residueNum;
        private readonly string element;
        public InvalidResidueElement() {}
        public InvalidResidueElement(string message) : base(message) {}
        public InvalidResidueElement(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidResidueElement(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.residueNum = info.GetInt32("ResidueNum");
            this.element = info.GetString("PdbName");
        }
        public InvalidResidueElement(string message, int residueNum, string element) : base(message) {
            this.residueNum = residueNum;
            this.element = element;
        }
        public int ResidueNum { get {return this.residueNum;}}
        public string Element { get {return this.element;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("ResidueNum", this.ResidueNum);
            info.AddValue("Element", this.Element);
            base.GetObjectData(info, context);
        }
    }

    //PDBs
    [Serializable]
    public class InvalidPDBLength : Exception
    {
        //This occurs when a PDB Name doesn't have length 4
        private readonly string pdbName;
        public InvalidPDBLength() {}
        public InvalidPDBLength(string message) : base(message) {}
        public InvalidPDBLength(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidPDBLength(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.pdbName = info.GetString("PdbName");
        }
        public InvalidPDBLength(string message, string pdbName) : base(message) {
            this.pdbName = pdbName;
        }
        public string PdbName { get {return this.pdbName;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("PdbName", this.PdbName);
            base.GetObjectData(info, context);
        }
    }
    
    [Serializable]
    public class InvalidResidueState : Exception
    {
        //This occurs when a PDB Name doesn't have length 4
        private readonly RS state;
        public InvalidResidueState() {}
        public InvalidResidueState(string message) : base(message) {}
        public InvalidResidueState(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidResidueState(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.state = (RS)info.GetInt32("State");
        }
        public InvalidResidueState(string message, RS state) : base(message) {
            this.state = state;
        }
        public RS State { get {return this.state;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("State", this.State);
            base.GetObjectData(info, context);
        }
    }

    //ARROWS
    [Serializable]
    public class InvalidArrowID : Exception
    {
        private readonly AID arrowID;
        public InvalidArrowID() {}
        public InvalidArrowID(string message) : base(message) {}
        public InvalidArrowID(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidArrowID(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.arrowID = (AID)info.GetInt32("ArrowID");
        }
        public InvalidArrowID(string message, AID arrowID) : base(message) {
            this.arrowID = arrowID;
        }
        public AID ArrowID { get {return this.arrowID;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("ArrowID", (int)this.ArrowID);
            base.GetObjectData(info, context);
        }
    }

    //TASKS
    [Serializable]
    public class InvalidTask : Exception
    {
        //This occurs when an invalid Task ID is used
        private readonly TID taskID;
        public InvalidTask() {}
        public InvalidTask(string message) : base(message) {}
        public InvalidTask(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidTask(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.taskID = (TID)info.GetInt32("TaskID");
        }
        public InvalidTask(string message, TID taskID) : base(message) {
            this.taskID = taskID;
        }
        public TID TaskID { get {return this.taskID;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("TaskID", (int)this.TaskID);
            base.GetObjectData(info, context);
        }
    }

    //GEOMETRY INTERFACE
    [Serializable]
    public class InvalidGeometryInterfaceID : Exception
    {
        //This occurs when an invalid Geometry Interface ID is used
        private readonly Constants.GeometryInterfaceID geometryInterfaceID;
        public InvalidGeometryInterfaceID() {}
        public InvalidGeometryInterfaceID(string message) : base(message) {}
        public InvalidGeometryInterfaceID(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidGeometryInterfaceID(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.geometryInterfaceID = (Constants.GeometryInterfaceID)info.GetInt32("GeometryInterfaceID");
        }
        public InvalidGeometryInterfaceID(string message, Constants.GeometryInterfaceID geometryInterfaceID) : base(message) {
            this.geometryInterfaceID = geometryInterfaceID;
        }
        public Constants.GeometryInterfaceID GeometryInterfaceID { get {return this.geometryInterfaceID;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("GeometryInterfaceID", (int)this.GeometryInterfaceID);
            base.GetObjectData(info, context);
        }
    }

    [Serializable]
    public class InvalidGeometryInterfaceCallbackID : Exception
    {
        //This occurs when an invalid Geometry Interface ID is used
        private readonly Constants.GeometryInterfaceCallbackID callbackID;
        public InvalidGeometryInterfaceCallbackID() {}
        public InvalidGeometryInterfaceCallbackID(string message) : base(message) {}
        public InvalidGeometryInterfaceCallbackID(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidGeometryInterfaceCallbackID(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.callbackID = (Constants.GeometryInterfaceCallbackID)info.GetInt32("GeometryInterfaceID");
        }
        public InvalidGeometryInterfaceCallbackID(string message, Constants.GeometryInterfaceCallbackID callbackID) : base(message) {
            this.callbackID = callbackID;
        }
        public Constants.GeometryInterfaceCallbackID CallbackID { get {return this.callbackID;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("CallbackID", (int)this.CallbackID);
            base.GetObjectData(info, context);
        }
    }
    
    [Serializable]
    public class InvalidGeometryInterfaceStatus : Exception
    {
        //This occurs when an invalid Geometry Interface ID is used
        private readonly Constants.GeometryInterfaceStatus geometryInterfaceStatus;
        public InvalidGeometryInterfaceStatus() {}
        public InvalidGeometryInterfaceStatus(string message) : base(message) {}
        public InvalidGeometryInterfaceStatus(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidGeometryInterfaceStatus(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.geometryInterfaceStatus = (Constants.GeometryInterfaceStatus)info.GetInt32("GeometryInterfaceStatus");
        }
        public InvalidGeometryInterfaceStatus(string message, Constants.GeometryInterfaceStatus geometryInterfaceStatus) : base(message) {
            this.geometryInterfaceStatus = geometryInterfaceStatus;
        }
        public Constants.GeometryInterfaceStatus GeometryInterfaceStatus { get {return this.geometryInterfaceStatus;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("GeometryInterfaceStatus", (int)this.GeometryInterfaceStatus);
            base.GetObjectData(info, context);
        }
    }

    //BONDS
    [Serializable]
    public class InvalidBondType : Exception
    {
        //This occurs when an invalid bond type is used
        private readonly BT bondType;
        public InvalidBondType() {}
        public InvalidBondType(string message) : base(message) {}
        public InvalidBondType(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected InvalidBondType(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.bondType = (BT)info.GetInt32("BondType");
        }
        public InvalidBondType(string message, BT bondType) : base(message) {
            this.bondType = bondType;
        }
        public BT BondType { get {return this.bondType;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("BondType", (int)this.BondType);
            base.GetObjectData(info, context);
        }
    }

    //SETTINGS
    [Serializable]
    public class XMLParseError : Exception
    {
        //This occurs when an invalid bond type is used
        private readonly string badName;
        private readonly string fileName;
        public XMLParseError() {}
        public XMLParseError(string message) : base(message) {}
        public XMLParseError(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected XMLParseError(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.badName = info.GetString("BadName");
            this.fileName = info.GetString("FileName");
        }
        public XMLParseError(string message, string badName, string fileName) : base(message) {
            this.badName = badName;
            this.fileName = fileName;
        }
        public string BadName { get {return this.badName;}}
        public string FileName { get {return this.fileName;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("BadName", this.BadName);
            info.AddValue("FileName", this.FileName);
            base.GetObjectData(info, context);
        }
    }

    //IO
    [Serializable]
    public class PDBIDException : Exception
    {
        //This occurs when an invalid bond type is used
        private readonly string pdbString;
        public PDBIDException() {}
        public PDBIDException(string message) : base(message) {}
        public PDBIDException(string message, Exception innerException) : base(message, innerException) {}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected PDBIDException(SerializationInfo info, StreamingContext context) : base(info, context) {
            this.pdbString = info.GetString("PDBString");
        }
        public PDBIDException(string message, string pdbString) : base(message) {
            this.pdbString = pdbString;
        }
        public string PDBString { get {return this.pdbString;}}
        
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            info.AddValue("PDBString", this.PDBString);
            base.GetObjectData(info, context);
        }
    }






}

