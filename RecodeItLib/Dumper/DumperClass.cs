using System.Collections;
using System.Runtime.CompilerServices;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using ReCodeIt.Utils;

namespace ReCodeItLib.Dumper;

public class DumperClass
{
    private ModuleDefMD? _gameModule { get; set; }
    private ModuleDefMD? _checkerModule { get; set; }
    private string _assemblyPath { get; set; }
    private string _fileCheckerPath { get; set; }
    private string _managedPath { get; set; }
    private List<TypeDef>? _gameTypes { get; set; }
    private List<TypeDef>? _checkerTypes { get; set; }

    public DumperClass(string managedPath)
    {
        _managedPath = managedPath;
        _assemblyPath = Path.Combine(managedPath, "Assembly-Csharp-cleaned.dll"); // TODO: must be the cleaned one
        _fileCheckerPath = Path.Combine(managedPath, "FilesChecker.dll");

        if (!File.Exists(_assemblyPath))
        {
            Logger.Log($"File Assembly-CSharp-cleaned.dll does not exist at {_assemblyPath}", ConsoleColor.Red);
        }

        if (!File.Exists(_fileCheckerPath))
        {
            Logger.Log($"File FilesChecker.dll does not exist at {_fileCheckerPath}", ConsoleColor.Red);
        }
        
        // will explode if they are not there?
        // TODO: [CWX] TRIED OVERRDING 
        _gameModule = DataProvider.LoadModule(_assemblyPath, _managedPath);
        _checkerModule = DataProvider.LoadModule(_fileCheckerPath, _managedPath);
        _gameTypes = _gameModule.GetTypes().ToList();
        _checkerTypes = _checkerModule.GetTypes().ToList();
    }
    
    
    public void CreateDumper()
    {
        if (_gameModule == null || _gameTypes == null)
        {
            Logger.Log($"_gameModule or _gameTypes in DumpyRemake was null", ConsoleColor.Red);
            return;
        }

        if (_checkerModule == null || _checkerTypes == null)
        {
            Logger.Log($"_checkerModule or _checkerTypes in DumpyRemake was null", ConsoleColor.Red);
            return;
        }
        
        // get required types
        var backRequestType = _gameTypes.Where(DumpyTypeHelper.GetBackRequestType).ToList();
        var validateCertType = _gameTypes.Where(DumpyTypeHelper.GetValidateCertType).ToList();
        var runValidationType = _gameTypes.Where(DumpyTypeHelper.GetRunValidationType).ToList();
        var dumpyTaskType = _gameTypes.Where(DumpyTypeHelper.GetMenuscreenType).ToList();
        
        // check types
        CheckNullOrMulti(backRequestType, "BackRequest");
        CheckNullOrMulti(validateCertType, "ValidateCertificate");
        CheckNullOrMulti(runValidationType, "RunValidation");
        CheckNullOrMulti(dumpyTaskType, "DumpyTask");
        
        // apply code changes
        SetBackRequestCode(backRequestType[0]);
        SetValidateCertCode(validateCertType[0]);
        SetRunValidationCode(runValidationType[0]);
        SetDumpyTaskCode(dumpyTaskType[0]);

        // TODO: Write game assembly to file
        
        _gameModule.Write(Path.Combine(_managedPath, "Assembly-CSharp-dumper.dll"));
        
        // get types
        var ensureConsistencyTypes = _checkerTypes.Where(DumpyTypeHelper.GetEnsureConsistencyType).ToList();
        
        // check types
        CheckNullOrMulti(ensureConsistencyTypes, "EnsureConsistency");
        
        // apply code changes
        SetEnsureConsistencyCode(ensureConsistencyTypes[0]);
        SetEnsureConsistencySingleCode(ensureConsistencyTypes[0]);
        
        // TODO: Write fileChecker assembly to file
        _checkerModule.Write(Path.Combine(_managedPath, "FilesChecker-dumper.dll"));
    }
    
    public void CreateDumpFolders()
    {
        // create dumper folders
    }

    /// <summary>
    /// Checks for null or multiple types
    /// </summary>
    /// <param name="types">ICollection</param>
    /// <param name="name">string</param>
    private void CheckNullOrMulti(ICollection types, string name = "")
    {
        if (types == null)
        {
            Logger.Log($"{name} was null");
        }

        if (types.Count > 1)
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
        var method = type.Methods.First(x => x.Parameters.Any(p => p.Name is "backRequest" && x.Parameters.Any(p => p.Name == "bResponse")));

        if (method == null || method.Body.Instructions.Count != 269)
        {
            Logger.Log($"BackRequest Instructions count has changed from 269 to {method.Body.Instructions.Count}", ConsoleColor.Red);
        }

        var startOfInstructions = 252;
        var liList = DumpyInstructionsHelper.GetBackRequestInstructions(_gameModule, method);
        var index = method.Body.Instructions[startOfInstructions];

        foreach (var li in liList)
        {
            // something along these lines, this needs to be tested
            method.Body.Instructions.InsertBefore(index, li);
        }

        // create instruction
        var ins = Instruction.Create(OpCodes.Brfalse_S, method.Body.Instructions[startOfInstructions]);

        // replace instruction at 220 with this
        method.Body.Instructions[220] = ins;
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
        var methods = type.Methods.Where(x =>
            x.Name == "ValidateCertificate"); // should be 2

        // check make sure nothing has changed
        var firstMethod = methods.FirstOrDefault(m => m.Parameters.Any(p => p.Name == "certificate"));
        var secondMethod = methods.FirstOrDefault(m => m.Parameters.Any(p => p.Name == "certificateData"));

        if (firstMethod?.Body.Instructions.Count != 55 || secondMethod?.Body.Instructions.Count != 14)
        {
            Logger.Log($"Instruction count has changed, method with 'certificate' as a param - before: 51, now: {firstMethod.Body.Instructions.Count}, " +
                       $"method with 'certificateData' as a param - before: 14, now: {secondMethod.Body.Instructions.Count}", ConsoleColor.Red);
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
        var importer = new Importer(_gameModule);
        
        var method = type.Methods.First(x => x.Name == "RunValidation");
        var method2 = type.NestedTypes[0].Methods.First(x => x.Name == "MoveNext");

        if (method == null || method.Body.Instructions.Count != 25)
        {
            Logger.Log($"RunValidation Instructions count has changed from 25 to {method.Body.Instructions.Count}");
        }

        if (method2 == null || method2.Body.Instructions.Count != 171)
        {
            Logger.Log($"RunValidation's MoveNext Instructions count has changed from 171 to {method2.Body.Instructions.Count}");
        }

        // Clear these from the body of each method respectively
        method.Body.Instructions.Clear();
        method2.Body.Instructions.Clear();
        method2.Body.Variables.Clear();
        method2.Body.ExceptionHandlers.Clear();

        var liList = DumpyInstructionsHelper.GetRunValidationInstructions(_gameModule, method);
        var liList2 = DumpyInstructionsHelper.GetRunValidationInstructionsMoveNext(_gameModule, method2);

        foreach (var instruction in liList)
        {
            method.Body.Instructions.Add(instruction);
        }
        
        foreach (var instruction in liList2)
        {
            method2.Body.Instructions.Add(instruction);
        }

        var ins = Instruction.Create(OpCodes.Leave_S, method2.Body.Instructions[14]); // Create instruction to jump to index 14
        var ins1 = Instruction.Create(OpCodes.Leave_S, method2.Body.Instructions[method2.Body.Instructions.IndexOf(method2.Body.Instructions.Last())]); // Create instruction to jump to last index

        method2.Body.Instructions.InsertAfter(method2.Body.Instructions[5], ins); // Instruction to jump from 5 to 14
        method2.Body.Instructions.InsertAfter(method2.Body.Instructions[14], ins1); // Instruction to jump from 14 to last index

        // Create exception handler with defined indexes
        var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            TryStart = method2.Body.Instructions[3],
            TryEnd = method2.Body.Instructions[7],
            HandlerStart = method2.Body.Instructions[7],
            HandlerEnd = method2.Body.Instructions[16],
            CatchType = importer.Import(typeof(Exception)),
        };

        // Add exception handler to method body
        method2.Body.ExceptionHandlers.Add(handler);
    }

    private void SetDumpyTaskCode(TypeDef type)
    {
        var method = type.Methods.First(x => x.Name == "Awake");

        if (method == null || method.Body.Instructions.Count != 62)
        {
            Logger.Log($"MainMenu is null or isnt 62 instructions, SOMETHING HAD CHANGED!", ConsoleColor.Red);
        }

        var liList = DumpyInstructionsHelper.GetDumpyTaskInstructions(_gameModule, method);

        var index = method.Body.Instructions.First(x => x.OpCode == OpCodes.Ret);

        foreach (var item in liList)
        {
            method.Body.Instructions.InsertBefore(index, item);
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
        var method = type.Methods.First(x => x.Name == "EnsureConsistency");

        if (method == null || method.Body.Instructions.Count != 152)
        {
            Logger.Log($"EnsureConsistency Instructions count has changed from 152 to {method.Body.Instructions.Count}", ConsoleColor.Red);
        }

        // clear these from the method body
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();
        method.Body.ExceptionHandlers.Clear();

        var liList = DumpyInstructionsHelper.GetEnsureConsistencyInstructions(_gameModule, _checkerModule, method);
        
        foreach (var li in liList)
        {
            method.Body.Instructions.Append(li);
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
        var method = type.Methods.First(x => x.Name == "EnsureConsistencySingle");

        if (method == null || method.Body.Instructions.Count != 101)
        {
            Logger.Log($"EnsureConsistencySingle Instructions count has changed from 101 to {method.Body.Instructions.Count}", ConsoleColor.Red);
        }

        // clear these from the method body
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();
        method.Body.ExceptionHandlers.Clear();

        var liList = DumpyInstructionsHelper.GetEnsureConsistencyInstructions(_gameModule, _checkerModule, method);
        
        foreach (var li in liList)
        {
            method.Body.Instructions.Append(li);
        }
    }
}