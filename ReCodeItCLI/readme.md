# RecodeIt Command Line

This is the command line interface for ReCodeIt. It offers a streamlined way to operate the application without the
use of the graphical user interface (GUI). It can be used to de-obfuscate, re-map, and other smaller utilities.
It can be placed on the system path and accessed from anywhere. 

## Commands

- `deobfuscate` - This command will run the de4dot de-obfuscater over the assembly provided. It supports both the
primary game assembly and the launcher assembly.
  - Param `AssemblyPath` - The absolute path to your obfuscated assembly or exe file, the folder must contain all 
references needed to be resolved.
  - Param `IsLauncher` - Is the target the EFT launcher?

---

- `remap` - Generates a re-mapped dll when provided with a mapping file and de-obfuscated dll.
  - Param `MappingJsonPath` - The absolute path to the `mapping.json` file supports both `json` and `jsonc`.
  - Param `AssemblyPath` - The absolute path to the *de-obfuscated* dll generated from the `deobfuscate` command.
  - Param `Publicize` - if true, the re-mapper will publicize all types, methods, and properties in the assembly.
  - Param `Rename` - If true, the re-mapper will rename all changed types associated variable names to be the same as 
the declaring type

---

- `GenRefCountList` - Generates a print out of the most used classes. Useful to prioritize remap targets.
  - Param `AssemblyPath` - The absolute path to your de-obfuscated and remapped dll.

---

- `Dumper` - Generates a dumper zip.
  - Param `ManagedDirectory` - The absolute path to your Managed folder for EFT, folder must contain all references to 
be resolved. Assembly-CSharp-cleaned.dll, mscorlib.dll, FilesChecker.dll