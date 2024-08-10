using Mono.Cecil;
using Mono.Cecil.Cil;
using ReCodeIt.Utils;

namespace ReCodeItLib.Dumper;

public class Dumper
{
    public static void CreateDumper(string managedPath)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedPath);

        // remove these dirs so it resolves to managed folder only
        resolver.RemoveSearchDirectory(".");
        resolver.RemoveSearchDirectory("bin");

        var readerParameters = new ReaderParameters { AssemblyResolver = resolver };

        // Assembly-CSharp section
        var assemblyPath = Path.Combine(managedPath, "Assembly-Csharp.dll");
        var dumperOutputPath = Path.Combine(managedPath, "dumper");
        var dumperBackupPath = Path.Combine(dumperOutputPath, "backup");
        var dumperDataFolder = Path.Combine(dumperOutputPath, "DUMPDATA");
        var newAssemblyPath = Path.Combine(dumperOutputPath, "Assembly-CSharp.dll");

        Directory.CreateDirectory(dumperOutputPath);
        Directory.CreateDirectory(dumperBackupPath);
        Directory.CreateDirectory(dumperDataFolder);

        File.Copy(Path.Combine(managedPath, "Assembly-CSharp.dll"), Path.Combine(dumperBackupPath, "Assembly-CSharp.dll"), true);
        File.Copy(Path.Combine(managedPath, "FilesChecker.dll"), Path.Combine(dumperBackupPath, "FilesChecker.dll"), true);
        File.Copy(".\\Assets\\Dumper\\DumpLib.dll", Path.Combine(dumperOutputPath, "DumpLib.dll"), true);
        File.Copy(".\\Assets\\Dumper\\DUMPDATA\\botReqData.json", dumperDataFolder + "\\botreqData.json", true);
        File.Copy(".\\Assets\\Dumper\\DUMPDATA\\config.json", dumperDataFolder + "\\config.json", true);
        File.Copy(".\\Assets\\Dumper\\DUMPDATA\\raidSettings.json", dumperDataFolder + "\\raidSettings.json", true);

        // loads assembly
        var oldAssembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);

        // gets all types
        var types = oldAssembly.MainModule.GetTypes().ToList();

        // finds and checks for type with backRequest
        var backRequestType = types.Where(DumpyTypeHelper.GetBackRequestType).ToList();
        CheckNullOrMulti(backRequestType, "BackRequest");

        // finds and checks for type with ValidateCertificate
        var validateCertificateType = types.Where(DumpyTypeHelper.GetValidateCertificateType).ToList();
        CheckNullOrMulti(validateCertificateType, "ValidateCertificate");

        // finds and checks for type with RunValidation
        var runValidationType = types.Where(DumpyTypeHelper.GetRunValidationType).ToList();
        CheckNullOrMulti(runValidationType, "RunValidation");

        var dumpyTaskType = types.Where(DumpyTypeHelper.GetMenuScreenType).ToList();
        CheckNullOrMulti(dumpyTaskType, "DumpyTask");

        // apply code changes
        SetBackRequestCode(oldAssembly, backRequestType[0]);
        SetValidateCertificateCode(validateCertificateType[0]);
        SetRunValidationCode(oldAssembly, runValidationType[0]);
        SetDumpyTaskCode(oldAssembly, dumpyTaskType[0]);

        // write modified assembly to file
        oldAssembly.Write(newAssemblyPath);

        // FilesChecker section
        var oldFilesCheckerPath = Path.Combine(managedPath, "FilesChecker.dll");
        var oldFilesChecker = AssemblyDefinition.ReadAssembly(oldFilesCheckerPath, readerParameters);
        var newFilesCheckerPath = Path.Combine(dumperOutputPath, Path.GetFileName(oldFilesCheckerPath));

        // gets all types
        types = oldFilesChecker.MainModule.GetTypes().ToList();

        // finds and checks for type called EnsureConsistency
        var ensureConsistencyType = types.Where(DumpyTypeHelper.GetEnsureConsistencyType).ToList();
        CheckNullOrMulti(ensureConsistencyType, "EnsureConsistency");

        // apply code changes
        SetEnsureConsistencyCode(oldFilesChecker, ensureConsistencyType[0]);
        SetEnsureConsistencySingleCode(oldFilesChecker, ensureConsistencyType[0]);

        // Write modified assembly to file
        oldFilesChecker.Write(newFilesCheckerPath);
    }

    /// <summary>
    /// <para>Finds the method with backRequest and bResponse as params.</para>
    /// <para>Checks the method instructions before modification has a count of 269,</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldAssembly"></param>
    /// <param name="type"></param>
    private static void SetBackRequestCode(AssemblyDefinition oldAssembly, TypeDefinition type)
    {
        // find method
        var method = type.Methods.First(x =>
            x.Parameters.Any(p => p.Name is "backRequest") && x.Parameters.Any(p => p.Name == "bResponse"));

        if (method == null || method.Body.Instructions.Count != 269)
        {
            Logger.Log($"BackRequest Instructions count has changed from 269 to {method.Body.Instructions.Count}", ConsoleColor.Red);
        }

        // where we insert the new instructions
        var startOfInstructions = 252;

        var processor = method.Body.GetILProcessor();

        var liList = DumpyInstructionsHelper.GetBackRequestInstructions(oldAssembly, method);

        var index = method.Body.Instructions[startOfInstructions];

        foreach (var item in liList)
        {
            processor.InsertBefore(index, item);
        }

        var ins = Instruction.Create(OpCodes.Brfalse_S, method.Body.Instructions[startOfInstructions]); // create instruction to jump to index 252

        method.Body.Instructions[220] = ins; // instruction to jump from 202 to 252
    }

    /// <summary>
    /// <para>Finds the method called ValidateCertificate.</para>
    /// <para>Checks that we found two of these methods,</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldAssembly"></param>
    /// <param name="type"></param>
    private static void SetValidateCertificateCode(TypeDefinition type)
    {
        var methods = type.Methods.Where(x =>
            x.Name == "ValidateCertificate"); // should be 2

        // check make sure nothing has changed
        var firstMethod = methods.FirstOrDefault(m => m.Parameters.Any(p => p.Name == "certificate"));
        var secondMethod = methods.FirstOrDefault(m => m.Parameters.Any(p => p.Name == "certificateData"));

        if (firstMethod?.Body.Instructions.Count != 55 || secondMethod?.Body.Instructions.Count != 14)
        {
            var errorMessage =
                $"Instruction count has changed, method with 'certificate' as a param - before: 51, now: {firstMethod.Body.Instructions.Count}, " +
                $"method with 'certificateData' as a param - before: 14, now: {secondMethod.Body.Instructions.Count}";
            Logger.Log(errorMessage, ConsoleColor.Red);
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
    private static void SetRunValidationCode(AssemblyDefinition oldAssembly, TypeDefinition type)
    {
        var method = type.Methods.First(x => x.Name == "RunValidation");
        var method2 = type.NestedTypes[0].Methods.First(x => x.Name == "MoveNext");

        if (method == null || method.Body.Instructions.Count != 25)
        {
            Logger.Log($"RunValidation Instructions count has changed from 25 to {method.Body.Instructions.Count}", ConsoleColor.Red);
        }

        if (method2 == null || method2.Body.Instructions.Count != 171)
        {
            Logger.Log($"RunValidation's MoveNext Instructions count has changed from 171 to {method2.Body.Instructions.Count}", ConsoleColor.Red);
        }

        // Clear these from the body of each method respectively
        method.Body.Instructions.Clear();
        method2.Body.Instructions.Clear();
        method2.Body.Variables.Clear();
        method2.Body.ExceptionHandlers.Clear();

        var processor = method.Body.GetILProcessor();
        var processor2 = method2.Body.GetILProcessor();

        var liList = DumpyInstructionsHelper.GetRunValidationInstructions(oldAssembly, method);
        var liList2 = DumpyInstructionsHelper.GetRunValidationInstructionsMoveNext(oldAssembly, method2);

        foreach (var instruction in liList)
        {
            processor.Append(instruction);
        }

        foreach (var instruction in liList2)
        {
            processor2.Append(instruction);
        }

        var ins = Instruction.Create(OpCodes.Leave_S, method2.Body.Instructions[14]); // Create instruction to jump to index 14
        var ins1 = Instruction.Create(OpCodes.Leave_S, method2.Body.Instructions[method2.Body.Instructions.IndexOf(method2.Body.Instructions.Last())]); // Create instruction to jump to last index

        processor2.InsertAfter(method2.Body.Instructions[5], ins); // Instruction to jump from 5 to 14
        processor2.InsertAfter(method2.Body.Instructions[14], ins1); // Instruction to jump from 14 to last index

        // Create exception handler with defined indexes
        var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            TryStart = method2.Body.Instructions[3],
            TryEnd = method2.Body.Instructions[7],
            HandlerStart = method2.Body.Instructions[7],
            HandlerEnd = method2.Body.Instructions[16],
            CatchType = method2.Module.ImportReference(typeof(Exception)),
        };

        // Add exception handler to method body
        method2.Body.ExceptionHandlers.Add(handler);
    }

    /// <summary>
    /// <para>Finds the method called EnsureConsistency.</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldFileChecker"></param>
    /// <param name="type"></param>
    private static void SetEnsureConsistencyCode(AssemblyDefinition oldFileChecker, TypeDefinition type)
    {
        var method = type.Methods.First(x => x.Name == "EnsureConsistency");

        if (method == null || method.Body.Instructions.Count != 152)
        {
            Logger.Log($"EnsureConsistency is null or Instructions count has changed from 152 to {method.Body.Instructions.Count}", ConsoleColor.Red);
        }

        var processor = method.Body.GetILProcessor();

        // clear these from the method body
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();
        method.Body.ExceptionHandlers.Clear();

        var liList = DumpyInstructionsHelper.GetEnsureConsistencyInstructions(oldFileChecker, method);

        foreach (var li in liList)
        {
            processor.Append(li);
        }
    }

    /// <summary>
    /// <para>Finds the method called EnsureConsistencySingle.</para>
    /// <para>if this is not the case, this needs to be checked.</para>
    /// <para>This type passed in is the only type with this method.</para>
    /// </summary>
    /// <param name="oldFileChecker"></param>
    /// <param name="type"></param>
    private static void SetEnsureConsistencySingleCode(AssemblyDefinition oldFileChecker, TypeDefinition type)
    {
        var method = type.Methods.First(x => x.Name == "EnsureConsistencySingle");

        if (method == null || method.Body.Instructions.Count != 101)
        {
            Logger.Log($"EnsureConsistencySingle is null or Instructions count has changed from 101 to {method.Body.Instructions.Count}", ConsoleColor.Red);
        }

        // clear these from the method body
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();
        method.Body.ExceptionHandlers.Clear();

        var processor = method.Body.GetILProcessor();

        var liList = DumpyInstructionsHelper.GetEnsureConsistencyInstructions(oldFileChecker, method);

        foreach (var li in liList)
        {
            processor.Append(li);
        }
    }

    private static void SetDumpyTaskCode(AssemblyDefinition oldAssembly, TypeDefinition type)
    {
        var method = type.Methods.First(x => x.Name == "Awake");

        if (method == null || method.Body.Instructions.Count != 62)
        {
            Logger.Log($"MainMenu is null or instructions have changed from 62 to {method.Body.Instructions.Count}", ConsoleColor.Red);
        }

        var processor = method.Body.GetILProcessor();

        var liList = DumpyInstructionsHelper.GetDumpyTaskInstructions(oldAssembly, method);

        var index = method.Body.Instructions.First(x => x.OpCode == OpCodes.Ret);

        foreach (var item in liList)
        {
            processor.InsertBefore(index, item);
        }
    }

    /// <summary>
    /// Checks for null or multiple types
    /// </summary>
    /// <param name="types">ICollection</param>
    /// <param name="name">string</param>
    public static void CheckNullOrMulti(List<TypeDefinition> types, string name = "")
    {
        if (types == null)
        {
            Logger.Log($"{name} was null", ConsoleColor.Red);
        }

        if (types.Count > 1)
        {
            Logger.Log($"{name} count was more than 1", ConsoleColor.Red);
        }
    }
}