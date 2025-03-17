using System.Collections;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.IO.Compression;
using AssemblyLib.ReMapper;
using AssemblyLib.Utils;

namespace AssemblyLib.Dumper;

public class DumperClass
{
    private ModuleDefMD? _gameModule { get; set; }
    private ModuleDefMD? _checkerModule { get; set; }
    private ModuleDefMD? _msModule { get; set; }
    private ModuleDefMD? _dumpModule { get; set; }
    private string _assemblyPath { get; set; }
    private string _fileCheckerPath { get; set; }
    private string _mscorlibPath { get; set; }
    private string _dumpLibPath { get; set; }
    private string _managedPath { get; set; }
    private List<TypeDef>? _gameTypes { get; set; }
    private List<TypeDef>? _checkerTypes { get; set; }
    private Importer? _gameImporter { get; set; }
    private Importer? _checkImporter { get; set; }

    public DumperClass(string managedPath)
    {
        _managedPath = managedPath;
        _assemblyPath = Path.Combine(managedPath, "Assembly-Csharp.dll");
        _fileCheckerPath = Path.Combine(managedPath, "FilesChecker.dll");
        _mscorlibPath = Path.Combine(managedPath, "mscorlib.dll");
        _dumpLibPath = "./DumpLib.dll";

        if (!File.Exists(_assemblyPath))
        {
            Logger.Log($"File Assembly-CSharp-cleaned.dll does not exist at {_assemblyPath}", ConsoleColor.Red);
        }

        if (!File.Exists(_fileCheckerPath))
        {
            Logger.Log($"File FilesChecker.dll does not exist at {_fileCheckerPath}", ConsoleColor.Red);
        }
        
        if (!File.Exists(_mscorlibPath))
        {
            Logger.Log($"File FilesChecker.dll does not exist at {_mscorlibPath}", ConsoleColor.Red);
        }
        
        if (!File.Exists(_dumpLibPath))
        {
            Logger.Log($"File DumpLib.dll does not exist at {_dumpLibPath}", ConsoleColor.Red);
        }
        
        _assemblyPath = AssemblyUtils.TryDeObfuscate(
            DataProvider.LoadModule(_assemblyPath), 
            _assemblyPath, 
            out var gameModule);
        
        _gameModule = gameModule;
        _checkerModule = DataProvider.LoadModule(_fileCheckerPath);
        _msModule = DataProvider.LoadModule(_mscorlibPath);
        _dumpModule = DataProvider.LoadModule(_dumpLibPath);
        _gameTypes = _gameModule.GetTypes().ToList();
        _checkerTypes = _checkerModule.GetTypes().ToList();
        _gameImporter = new Importer(_gameModule);
        _checkImporter = new Importer(_checkerModule);
    }
    
    public void CreateDumper()
    {
        if (_gameModule == null || _gameTypes == null)
        {
            Logger.Log($"_gameModule or _gameTypes in CreateDumper() was null", ConsoleColor.Red);
            return;
        }

        if (_checkerModule == null || _checkerTypes == null)
        {
            Logger.Log($"_checkerModule or _checkerTypes in CreateDumper() was null", ConsoleColor.Red);
            return;
        }
        
        if (_msModule == null)
        {
            Logger.Log($"_msModule in CreateDumper() was null", ConsoleColor.Red);
            return;
        }
        
        if (_dumpModule == null)
        {
            Logger.Log($"_dumpModule in CreateDumper() was null", ConsoleColor.Red);
            return;
        }
        
        // get types
        var backRequestType = _gameTypes.Where(DumpyReflectionHelper.GetBackRequestType).ToList();
        var validateCertType = _gameTypes.Where(DumpyReflectionHelper.GetValidateCertType).ToList();
        var runValidationType = _gameTypes.Where(DumpyReflectionHelper.GetRunValidationType).ToList();
        var dumpyTaskType = _gameTypes.Where(DumpyReflectionHelper.GetMenuscreenType).ToList();
        
        // check types
        CheckNullOrMulti(backRequestType, "BackRequest");
        CheckNullOrMulti(validateCertType, "ValidateCertificate");
        CheckNullOrMulti(runValidationType, "RunValidation");
        CheckNullOrMulti(dumpyTaskType, "DumpyTask");
        
        // apply code changes
        SetBackRequestCode(backRequestType.First());
        SetValidateCertCode(validateCertType.First());
        SetRunValidationCode(runValidationType.First());
        SetDumpyTaskCode(dumpyTaskType.First());

        // write assembly to disk
        _gameModule.Write(Path.Combine(_managedPath, "Assembly-CSharp-dumper.dll"));
        
        // get types
        var ensureConsistencyTypes = _checkerTypes.Where(DumpyReflectionHelper.GetEnsureConsistencyType).ToList();
        
        // check types
        CheckNullOrMulti(ensureConsistencyTypes, "EnsureConsistency");
        
        // apply code changes
        SetEnsureConsistencyCode(ensureConsistencyTypes.First());
        SetEnsureConsistencySingleCode(ensureConsistencyTypes.First());
        
        // write assembly to disk
        _checkerModule.Write(Path.Combine(_managedPath, "FilesChecker-dumper.dll"));
    }
    
    public void CreateDumpFolders()
    {
        Directory.CreateDirectory(Path.Combine(_managedPath, "DumperZip"));
        Directory.CreateDirectory(Path.Combine(_managedPath, "Dumper/DUMPDATA"));
        Directory.CreateDirectory(Path.Combine(_managedPath, "Dumper/EscapeFromTarkov_Data/Managed/backup"));
    }
    
    public void CopyFiles()
    {
        File.Copy(Path.Combine(_managedPath, "Assembly-CSharp.dll"), Path.Combine(_managedPath, "Dumper/EscapeFromTarkov_Data/Managed/backup/Assembly-CSharp.dll"), true);
        File.Copy(Path.Combine(_managedPath, "FilesChecker.dll"), Path.Combine(_managedPath, "Dumper/EscapeFromTarkov_Data/Managed/backup/FilesChecker.dll"), true);
        File.Copy(Path.Combine(_managedPath, "Assembly-CSharp-dumper.dll"), Path.Combine(_managedPath, "Dumper/EscapeFromTarkov_Data/Managed/Assembly-CSharp.dll"), true);
        File.Copy(Path.Combine(_managedPath, "FilesChecker-dumper.dll"), Path.Combine(_managedPath, "Dumper/EscapeFromTarkov_Data/Managed/FilesChecker.dll"), true);
        File.Copy("./DumpLib.dll", Path.Combine(_managedPath, "Dumper/EscapeFromTarkov_Data/Managed/DumpLib.dll"), true);
        File.Copy("./DUMPDATA/botReqData.json", Path.Combine(_managedPath, "Dumper/DUMPDATA/botReqData.json"), true);
        File.Copy("./DUMPDATA/config.json", Path.Combine(_managedPath, "Dumper/DUMPDATA/config.json"), true);
        File.Copy("./DUMPDATA/raidSettings.json", Path.Combine(_managedPath, "Dumper/DUMPDATA/raidSettings.json"), true);
        File.Copy("./DUMPDATA/raidConfig.json", Path.Combine(_managedPath, "Dumper/DUMPDATA/raidConfig.json"), true);
        File.Copy("./DUMPDATA/endRaid.json", Path.Combine(_managedPath, "Dumper/DUMPDATA/endRaid.json"), true);
    }

    public void ZipFiles()
    {
        if (File.Exists(Path.Combine(_managedPath, "DumperZip/Dumper.zip")))
        {
            File.Delete(Path.Combine(_managedPath, "DumperZip/Dumper.zip"));
        }
        
        ZipFile.CreateFromDirectory(Path.Combine(_managedPath, "Dumper"), Path.Combine(_managedPath, "DumperZip/Dumper.zip"), CompressionLevel.Optimal, false);
    }

    /// <summary>
    /// Checks for null or multiple types
    /// </summary>
    /// <param name="types">ICollection</param>
    /// <param name="name">string</param>
    private void CheckNullOrMulti(ICollection? types, string name = "")
    {
        if (types == null)
        {
            Logger.Log($"{name} was null");
        }

        if (types?.Count > 1)
        {
            Logger.Log($"{name} count was more than 1");
        }
    }

    /// <summary>
    /// <para>Finds the method with backRequest and bResponse as params.</para>
    /// <para>Checks the method instructions before modification has a count of 269,</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldAssembly"></param>
    /// <param name="type"></param>
    private void SetBackRequestCode(TypeDef type)
    {
        // find method
        var method = type.Methods.First(DumpyReflectionHelper.GetBackRequestMethod);

        if (method == null || method.Body.Instructions.Count != 269)
        {
            Logger.Log($"BackRequest Instructions count has changed from 269 to {method?.Body.Instructions.Count}", ConsoleColor.Red);
        }

        var startOfInstructions = 252;
        var liList = DumpyILHelper.GetBackRequestInstructions(method!, _gameImporter);
        var index = method?.Body.Instructions[startOfInstructions];

        foreach (var li in liList)
        {
            // something along these lines, this needs to be tested
            method?.Body.Instructions.InsertBefore(index!, li);
        }

        // create instruction
        var ins = Instruction.Create(OpCodes.Brfalse_S, method?.Body.Instructions[startOfInstructions]);

        // replace instruction at 220 with this
        method!.Body.Instructions[220] = ins;
    }

    /// <summary>
    /// <para>Finds the method called ValidateCertificate.</para>
    /// <para>Checks that we found two of these methods,</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldAssembly"></param>
    /// <param name="type"></param>
    private void SetValidateCertCode(TypeDef type)
    {
        var methods = type.Methods.Where(DumpyReflectionHelper.GetValidateCertMethods); // should be 2

        // check make sure nothing has changed
        var firstMethod = methods.FirstOrDefault(m => m.Parameters.Any(p => p.Name == "certificate"));
        var secondMethod = methods.FirstOrDefault(m => m.Parameters.Any(p => p.Name == "certificateData"));

        // as of 01/11/24 firstMethod returns true, so its now only 2 instructions, was 55 (this may change, BSG have byppassed their own SSL checks atm)
        if (firstMethod?.Body.Instructions.Count != 2 || secondMethod?.Body.Instructions.Count != 14) 
        {
            Logger.Log($"Instruction count has changed, method with 'certificate' as a param - before: 51, now: {firstMethod?.Body.Instructions.Count}, " +
                       $"method with 'certificateData' as a param - before: 14, now: {secondMethod?.Body.Instructions.Count}", ConsoleColor.Red);
        }

        if (methods.Count() != 2)
        {
            Logger.Log($"ValidateCertificate should be found twice, count was: {methods.Count()}", ConsoleColor.Red);
        }

        foreach (var method in methods)
        {
            // clear these from the body.
            method.Body.Instructions.Clear();
            method.Body.Variables.Clear();
            method.Body.ExceptionHandlers.Clear();

            // return true;
            var ins = Instruction.Create(OpCodes.Ldc_I4_1);
            var ins1 = Instruction.Create(OpCodes.Ret);

            // add instructions
            method.Body.Instructions.Add(ins);
            method.Body.Instructions.Add(ins1);
        }
    }

    /// <summary>
    /// <para>Finds the method called RunValidation and MoveNext.</para>
    /// <para>Checks that we found two of these methods,</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldAssembly"></param>
    /// <param name="type"></param>
    private void SetRunValidationCode(TypeDef type)
    {
        var method = type.Methods.First(DumpyReflectionHelper.GetRunValidationMethod);
        var method2 = type.NestedTypes[0].Methods.First(DumpyReflectionHelper.GetRunValidationNextMethod);

        if (method == null || method.Body.Instructions.Count != 23)
        {
            Logger.Log($"RunValidation Instructions count has changed from 23 to {method?.Body.Instructions.Count}");
        }

        if (method2 == null || method2.Body.Instructions.Count != 171)
        {
            Logger.Log($"RunValidation's MoveNext Instructions count has changed from 171 to {method2?.Body.Instructions.Count}");
        }

        // Clear these from the body of each method respectively
        method?.Body.Instructions.Clear();
        method2?.Body.Instructions.Clear();
        method2?.Body.Variables.Clear();
        method2?.Body.ExceptionHandlers.Clear();

        var liList = DumpyILHelper.GetRunValidationInstructions(method!, _gameModule!, _msModule!, _gameImporter);
        var liList2 = DumpyILHelper.GetRunValidationInstructionsMoveNext(method2!, _gameModule!, _msModule!, _gameImporter);

        foreach (var instruction in liList)
        {
            method?.Body.Instructions.Add(instruction);
        }
        
        foreach (var instruction in liList2)
        {
            method2?.Body.Instructions.Add(instruction);
        }

        var ins = Instruction.Create(OpCodes.Leave_S, method2?.Body.Instructions[14]); // Create instruction to jump to index 14
        var ins1 = Instruction.Create(OpCodes.Leave_S, method2?.Body.Instructions.Last()); // Create instruction to jump to last index

        method2?.Body.Instructions.InsertAfter(method2.Body.Instructions[5], ins); // Instruction to jump from 5 to 14
        method2?.Body.Instructions.InsertAfter(method2.Body.Instructions[14], ins1); // Instruction to jump from 14 to last index

        // Add exception handler to method body
        method2?.Body.ExceptionHandlers.Add(DumpyILHelper.GetExceptionHandler(method2, _gameImporter, _msModule!));
    }

    private void SetDumpyTaskCode(TypeDef type)
    {
        var method = type.Methods.First(DumpyReflectionHelper.GetMenuscreenMethod);

        if (method == null || method.Body.Instructions.Count != 62)
        {
            Logger.Log($"MainMenu is null or isnt 62 instructions, SOMETHING HAD CHANGED!", ConsoleColor.Red);
        }

        var liList = DumpyILHelper.GetDumpyTaskInstructions(method!,_dumpModule!, _gameImporter);

        var index = method?.Body.Instructions.First(x => x.OpCode == OpCodes.Ret);

        foreach (var item in liList)
        {
            method?.Body.Instructions.InsertBefore(index!, item);
        }
    }

    /// <summary>
    /// <para>Finds the method called EnsureConsistency.</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldFileChecker"></param>
    /// <param name="type"></param>
    private void SetEnsureConsistencyCode(TypeDef type)
    {
        var method = type.Methods.First(DumpyReflectionHelper.GetEnsureConMethod);

        if (method == null || method.Body.Instructions.Count != 152)
        {
            Logger.Log($"EnsureConsistency Instructions count has changed from 152 to {method?.Body.Instructions.Count}", ConsoleColor.Red);
        }

        // clear these from the method body
        method?.Body.Instructions.Clear();
        method?.Body.Variables.Clear();
        method?.Body.ExceptionHandlers.Clear();

        var liList = DumpyILHelper.GetEnsureConsistencyInstructions(method!, _checkerModule!, _msModule!, _checkImporter);
        
        foreach (var li in liList)
        {
            method?.Body.Instructions.Add(li);
        }
    }

    /// <summary>
    /// <para>Finds the method called EnsureConsistencySingle.</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldFileChecker"></param>
    /// <param name="type"></param>
    private void SetEnsureConsistencySingleCode(TypeDef type)
    {
        var method = type.Methods.First(DumpyReflectionHelper.GetEnsureConSingleMethod);

        if (method == null || method.Body.Instructions.Count != 101)
        {
            Logger.Log($"EnsureConsistencySingle Instructions count has changed from 101 to {method?.Body.Instructions.Count}", ConsoleColor.Red);
        }

        // clear these from the method body
        method?.Body.Instructions.Clear();
        method?.Body.Variables.Clear();
        method?.Body.ExceptionHandlers.Clear();

        var liList = DumpyILHelper.GetEnsureConsistencyInstructions(method!, _checkerModule!, _msModule!, _checkImporter);
        
        foreach (var li in liList)
        {
            method?.Body.Instructions.Add(li);
        }
    }
}