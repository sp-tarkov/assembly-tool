using System.Collections;
using System.IO.Compression;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.ReMapper;
using AssemblyLib.Utils;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Dumper;

[Injectable(InjectionType.Singleton)]
public class DumperClass(
    AssemblyUtils assemblyUtils,
    DataProvider dataProvider,
    DumpyILHelper dumpyIlHelper,
    DumpyReflectionHelper dumpyReflectionHelper
    )
{
    private ModuleDefinition? _gameModule { get; set; }
    private ModuleDefinition? _checkerModule { get; set; }
    private ModuleDefinition? _msModule { get; set; }
    private ModuleDefinition? _dumpModule { get; set; }
    private string _assemblyPath { get; set; }
    private string _fileCheckerPath { get; set; }
    private string _mscorlibPath { get; set; }
    private string _dumpLibPath { get; set; }
    private string _managedPath { get; set; }
    private List<TypeDefinition>? _gameTypes { get; set; }
    private List<TypeDefinition>? _checkerTypes { get; set; }
    private ReferenceImporter? _gameImporter { get; set; }
    private ReferenceImporter? _checkImporter { get; set; }
    
    public void LoadModule(
        string managedPath
        )
    {
        _managedPath = managedPath;
        _assemblyPath = Path.Combine(managedPath, "Assembly-CSharp.dll");
        _fileCheckerPath = Path.Combine(managedPath, "FilesChecker.dll");
        _mscorlibPath = Path.Combine(managedPath, "mscorlib.dll");
        _dumpLibPath = "./DumpLib.dll";

        if (!File.Exists(_assemblyPath))
        {
            Log.Error("File Assembly-CSharp-cleaned.dll does not exist at {AssemblyPath}", _assemblyPath);
        }

        if (!File.Exists(_fileCheckerPath))
        {
            Log.Error("File FilesChecker.dll does not exist at {FileCheckerPath}", _fileCheckerPath);
        }

        if (!File.Exists(_mscorlibPath))
        {
            Log.Error("File FilesChecker.dll does not exist at {MscorlibPath}", _mscorlibPath);
        }

        if (!File.Exists(_dumpLibPath))
        {
            Log.Error("File DumpLib.dll does not exist at {DumpLibPath}", _dumpLibPath);
        }
        
        var kvp = assemblyUtils.TryDeObfuscate(dataProvider.LoadModule(_assemblyPath), _assemblyPath);
        _assemblyPath = kvp.Item1;
        _gameModule = kvp.Item2;
        _checkerModule = dataProvider.LoadModule(_fileCheckerPath);
        _msModule = dataProvider.LoadModule(_mscorlibPath);
        _dumpModule = dataProvider.LoadModule(_dumpLibPath, false);
        _gameTypes = _gameModule.GetAllTypes().ToList();
        _checkerTypes = _checkerModule.GetAllTypes().ToList();
        _gameImporter = new ReferenceImporter(_gameModule);
        _checkImporter = new ReferenceImporter(_checkerModule);
    }

    public void CreateDumper()
    {
        if (_gameModule == null || _gameTypes == null)
        {
            Log.Error("_gameModule or _gameTypes in CreateDumper() was null");
            return;
        }

        if (_checkerModule == null || _checkerTypes == null)
        {
            Log.Error("_checkerModule or _checkerTypes in CreateDumper() was null");
            return;
        }

        if (_msModule == null)
        {
            Log.Error("_msModule in CreateDumper() was null");
            return;
        }

        if (_dumpModule == null)
        {
            Log.Error("_dumpModule in CreateDumper() was null");
            return;
        }

        // get types
        var backRequestType = _gameTypes.Where(dumpyReflectionHelper.GetBackRequestType).ToList();
        var validateCertType = _gameTypes.Where(dumpyReflectionHelper.GetValidateCertType).ToList();
        var runValidationType = _gameTypes.Where(dumpyReflectionHelper.GetRunValidationType).ToList();
        var dumpyTaskType = _gameTypes.Where(dumpyReflectionHelper.GetMenuscreenType).ToList();

        // check types
        CheckNullOrMulti(backRequestType, "BackRequest");
        CheckNullOrMulti(validateCertType, "ValidateCertificate");
        CheckNullOrMulti(runValidationType, "RunValidation");
        CheckNullOrMulti(dumpyTaskType, "DumpyTask");

        // apply code changes
        SetBackRequestCode(backRequestType.FirstOrDefault());
        SetValidateCertCode(validateCertType.FirstOrDefault());
        SetRunValidationCode(runValidationType.FirstOrDefault());
        SetDumpyTaskCode(dumpyTaskType.FirstOrDefault());

        // write assembly to disk
        _gameModule.Write(Path.Combine(_managedPath, "Assembly-CSharp-dumper.dll"));

        // get types
        var ensureConsistencyTypes = _checkerTypes.Where(dumpyReflectionHelper.GetEnsureConsistencyType).ToList();

        // check types
        CheckNullOrMulti(ensureConsistencyTypes, "EnsureConsistency");

        // apply code changes
        SetEnsureConsistencyCode(ensureConsistencyTypes.FirstOrDefault());
        SetEnsureConsistencySingleCode(ensureConsistencyTypes.FirstOrDefault());

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
            Log.Error("{Name} was null", name);
        }

        if (types?.Count > 1)
        {
            Log.Error("{Name} count was more than 1", name);
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
    private void SetBackRequestCode(TypeDefinition type)
    {
        // find method
        var method = type.Methods.First(dumpyReflectionHelper.GetBackRequestMethod);

        if (method == null || method.CilMethodBody.Instructions.Count != 269)
        {
            Log.Error(
                "BackRequest Instructions count has changed from 269 to {InstructionsCount}", 
                method.CilMethodBody?.Instructions.Count
                );
        }

        var startOfInstructions = 252;
        var liList = dumpyIlHelper.GetBackRequestInstructions(method, _gameImporter);
        var index = method.CilMethodBody.Instructions[startOfInstructions];

        foreach (var li in liList)
        {
            // something along these lines, this needs to be tested
            method.CilMethodBody.Instructions.InsertBefore(index, li);
        }

        // create instruction
        var ins = new CilInstruction(CilOpCodes.Brfalse_S, method.CilMethodBody.Instructions[startOfInstructions].CreateLabel());

        // replace instruction at 220 with this
        method.CilMethodBody.Instructions[220] = ins;
        method.CilMethodBody.VerifyLabels(true);
    }

    /// <summary>
    /// <para>Finds the method called ValidateCertificate.</para>
    /// <para>Checks that we found two of these methods,</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldAssembly"></param>
    /// <param name="type"></param>
    private void SetValidateCertCode(TypeDefinition type)
    {
        var methods = type.Methods.Where(dumpyReflectionHelper.GetValidateCertMethods); // should be 2

        // check make sure nothing has changed
        var firstMethod = methods.FirstOrDefault(m => m.Parameters.Any(p => p.Name == "certificate"));
        var secondMethod = methods.FirstOrDefault(m => m.Parameters.Any(p => p.Name == "certificateData"));

        // as of 01/11/24 firstMethod returns true, so its now only 2 instructions, was 55 (this may change, BSG have byppassed their own SSL checks atm)
        if (firstMethod.CilMethodBody.Instructions.Count != 2 || secondMethod.CilMethodBody.Instructions.Count != 14)
        {
            Log.Error($@"Instruction count has changed, method with 'certificate' as a param - before: 51, now: {firstMethod.CilMethodBody.Instructions.Count}, " +
                      $"method with 'certificateData' as a param - before: 14, now: {secondMethod.CilMethodBody.Instructions.Count}");
        }

        if (methods.Count() != 2)
        {
            Log.Error($"ValidateCertificate should be found twice, count was: {methods.Count()}");
        }

        // TODO: redo to be a foreach
        firstMethod.CilMethodBody.Instructions.Clear();
        firstMethod.CilMethodBody.LocalVariables.Clear();
        firstMethod.CilMethodBody.ExceptionHandlers.Clear();

        // return true;
        var ins = new CilInstruction(CilOpCodes.Ldc_I4_1);
        var ins1 = new CilInstruction(CilOpCodes.Ret);

        // add instructions
        firstMethod.CilMethodBody.Instructions.Add(ins);
        firstMethod.CilMethodBody.Instructions.Add(ins1);

        secondMethod.CilMethodBody.Instructions.Clear();
        secondMethod.CilMethodBody.LocalVariables.Clear();
        secondMethod.CilMethodBody.ExceptionHandlers.Clear();

        // return true;
        var ins2 = new CilInstruction(CilOpCodes.Ldc_I4_1);
        var ins3 = new CilInstruction(CilOpCodes.Ret);

        // add instructions
        secondMethod.CilMethodBody.Instructions.Add(ins2);
        secondMethod.CilMethodBody.Instructions.Add(ins3);
        firstMethod.CilMethodBody.VerifyLabels(true);
        secondMethod.CilMethodBody.VerifyLabels(true);
    }

    /// <summary>
    /// <para>Finds the method called RunValidation and MoveNext.</para>
    /// <para>Checks that we found two of these methods,</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldAssembly"></param>
    /// <param name="type"></param>
    private void SetRunValidationCode(TypeDefinition type)
    {
        var method = type.Methods.First(dumpyReflectionHelper.GetRunValidationMethod);
        var method2 = type.NestedTypes[0].Methods.First(dumpyReflectionHelper.GetRunValidationNextMethod);

        if (method == null || method.CilMethodBody.Instructions.Count != 23)
        {
            Log.Error("RunValidation Instructions count has changed from 23 to {InstructionsCount}", method.CilMethodBody.Instructions.Count);
        }

        if (method2 == null || method2.CilMethodBody.Instructions.Count != 171)
        {
            Log.Error("RunValidation's MoveNext Instructions count has changed from 171 to {InstructionsCount}", method2.CilMethodBody.Instructions.Count);
        }

        // Clear these from the body of each method respectively
        method.CilMethodBody.Instructions.Clear();
        method2.CilMethodBody.Instructions.Clear();
        method2.CilMethodBody.LocalVariables.Clear();
        method2.CilMethodBody.ExceptionHandlers.Clear();

        var liList = dumpyIlHelper.GetRunValidationInstructions(method, _gameModule, _msModule, _gameImporter);
        var liList2 = dumpyIlHelper.GetRunValidationInstructionsMoveNext(method2, _gameModule, _msModule, _gameImporter);

        foreach (var instruction in liList)
        {
            method.CilMethodBody.Instructions.Add(instruction);
        }

        foreach (var instruction in liList2)
        {
            method2.CilMethodBody.Instructions.Add(instruction);
        }

        var ins = new CilInstruction(CilOpCodes.Leave_S, method2.CilMethodBody.Instructions[14].CreateLabel()); // Create instruction to jump to index 14
        var ins1 = new CilInstruction(CilOpCodes.Leave_S, method2.CilMethodBody.Instructions.Last().CreateLabel()); // Create instruction to jump to last index

        method2.CilMethodBody.Instructions.InsertAfter(method2.CilMethodBody.Instructions[5], ins); // Instruction to jump from 5 to 14
        method2.CilMethodBody.Instructions.InsertAfter(method2.CilMethodBody.Instructions[14], ins1); // Instruction to jump from 14 to last index

        // Add exception handler to method body
        method2.CilMethodBody.ExceptionHandlers.Add(dumpyIlHelper.GetExceptionHandler(method2, _gameImporter, _msModule));
        method.CilMethodBody.VerifyLabels(true);
        method2.CilMethodBody.VerifyLabels(true);
    }

    private void SetDumpyTaskCode(TypeDefinition type)
    {
        var method = type.Methods.First(dumpyReflectionHelper.GetMenuscreenMethod);

        if (method == null || method.CilMethodBody.Instructions.Count != 62)
        {
            Log.Error($"MainMenu is null or isnt 62 instructions, SOMETHING HAD CHANGED!");
        }

        var liList = dumpyIlHelper.GetDumpyTaskInstructions(method,_dumpModule, _gameImporter);

        var index = method.CilMethodBody.Instructions.First(x => x.OpCode == CilOpCodes.Ret);

        foreach (var item in liList)
        {
            method.CilMethodBody.Instructions.InsertBefore(index, item);
        }
        method.CilMethodBody.VerifyLabels(true);
    }

    /// <summary>
    /// <para>Finds the method called EnsureConsistency.</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldFileChecker"></param>
    /// <param name="type"></param>
    private void SetEnsureConsistencyCode(TypeDefinition type)
    {
        var method = type.Methods.First(dumpyReflectionHelper.GetEnsureConMethod);

        if (method == null || method.CilMethodBody.Instructions.Count != 152)
        {
            Log.Error(
                "EnsureConsistency Instructions count has changed from 152 to {InstructionsCount}", 
                method.CilMethodBody.Instructions.Count
                );
        }

        // clear these from the method body
        method.CilMethodBody.Instructions.Clear();
        method.CilMethodBody.LocalVariables.Clear();
        method.CilMethodBody.ExceptionHandlers.Clear();

        var liList = dumpyIlHelper.GetEnsureConsistencyInstructions(method, _checkerModule, _msModule, _checkImporter);

        foreach (var li in liList)
        {
            method.CilMethodBody.Instructions.Add(li);
        }
        method.CilMethodBody.VerifyLabels(true);
    }

    /// <summary>
    /// <para>Finds the method called EnsureConsistencySingle.</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldFileChecker"></param>
    /// <param name="type"></param>
    private void SetEnsureConsistencySingleCode(TypeDefinition type)
    {
        var method = type.Methods.First(dumpyReflectionHelper.GetEnsureConSingleMethod);

        if (method == null || method.CilMethodBody.Instructions.Count != 101)
        {
            Log.Error(
                "EnsureConsistencySingle Instructions count has changed from 101 to {InstructionsCount}", 
                method.CilMethodBody.Instructions.Count
                );
        }

        // clear these from the method body
        method.CilMethodBody.Instructions.Clear();
        method.CilMethodBody.LocalVariables.Clear();
        method.CilMethodBody.ExceptionHandlers.Clear();

        var liList = dumpyIlHelper.GetEnsureConsistencyInstructions(method, _checkerModule, _msModule, _checkImporter);

        foreach (var li in liList)
        {
            method.CilMethodBody.Instructions.Add(li);
        }
        method.CilMethodBody.VerifyLabels(true);
    }
}