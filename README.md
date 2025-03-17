# Building

- Install [.net9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Clone the project to a local folder
- Open the project in rider or viual studio and run the builder project
  - The project uses [Cake Build](https://cakebuild.net/) to manage the build process
- The application will build the assembly tool and output it to `solutionDir\Build`

---

# Usage

This tool is used to deobfuscate and remap the tarkov assembly to be more user friendly to interact with. The primary command is the `remap` command. It will take the entire process from start to finish.

- Make a copy of live tarkovs managed folder, found at `gameRoot\EscapeFromTarkov_Data\Managed\`
- The mapping file can be found in the `Assets` directory of the repository.
- It will not mutate the original input files, instead it will output new copys in the `Managed` folder it resides in.
- If you provide the previous spt versions assembly, metadata in the form of attributes will be applied to remapped classes. This contains data such as the original class name, and if the class has changed since the previous version. This is optional.

---

# Commands

- `addmissingproperties` - This command will add missing properties to the provided mapping.json.
  
- Note: This is a development command and should not be used unless you know what you're doing. Always make backups when using this command.

---

- `automatch` - This command will Automatically try to generate a mapping object given old type and new type names.
  - Param `AssemblyPath` - The absolute path to your assembly, folder must contain all references to be resolved.
  - Param `OldTypeName` - Full old type name including namespace `Foo.Bar` for nested classes `Foo.Bar/FooBar`
  - Param `NewTypeName` - The name you want the type to be renamed to.
  - This command will prompt you to append your created mapping to the mapping file.
  - It will then prompt you to run the remap process.

---

- `deobfuscate` - This command will run the de4dot de-obfuscater over the assembly provided. It supports both the
primary game assembly and the launcher assembly.
  - Param `AssemblyPath` - The absolute path to your obfuscated assembly or exe file, the folder must contain all
references needed to be resolved.
  - Param `IsLauncher` - Is the target the EFT launcher?

---

- `Dumper` - Generates a dumper zip.
  - Param `ManagedDirectory` - The absolute path to your Managed folder for EFT, folder must contain all references to
    be resolved. Assembly-CSharp-cleaned.dll, mscorlib.dll, FilesChecker.dll

---

- `GenRefCountList` - Generates a print out of the most used classes. Useful to prioritize remap targets.
  - Param `AssemblyPath` - The absolute path to your de-obfuscated and remapped dll.

---

- `regensig` - regenerates the signature of a mapping if it is failing
  - Param `AssemblyPath` - The absolute path to the assembly you want to regenerate the signature for.
  - Param `NewTypeName` - The new type name as listed in the mapping file.
  - Param `OldTypeName` - Full old type name including namespace `Foo.Bar` for nested classes `Foo.Bar/FooBar`

---

- `remap` - Generates a re-mapped dll provided a mapping file and dll. If the dll is obfuscated, it will automatically de-obfuscate.
  - Param `AssemblyPath` - The absolute path to the dll generated from the `deobfuscate` command.
  - Param `OldAssemblyPath` - The absolute path to the previous spt versions remapped assembly. Can be left empty.
