# Assembly Tool Command Line

This is the command line interface for assembly tool. It can be used to de-obfuscate, re-map, and other smaller utilities.
It can be placed on the system path and accessed from anywhere. 

## Commands

- `deobfuscate` - This command will run the de4dot de-obfuscater over the assembly provided. It supports both the
primary game assembly and the launcher assembly.
  - Param `AssemblyPath` - The absolute path to your obfuscated assembly or exe file, the folder must contain all 
references needed to be resolved.
  - Param `IsLauncher` - Is the target the EFT launcher?

---

- `automatch` - This command will Automatically try to generate a mapping object given old type and new type names.
  - `AssemblyPath` - The absolute path to your assembly, folder must contain all references to be resolved.
  - `MappingsPath` - Path to your mapping file so it can be updated if a match is found.
  - `OldTypeName` - Full old type name including namespace.
  - `NewTypeName` - The name you want the type to be renamed to.

- This command will prompt you to append your created mapping to the mapping file. 
- It will then prompt you to run the remap process.

---

- `remap` - Generates a re-mapped dll provided a mapping file and dll. If the dll is obfuscated, it will automatically de-obfuscate.
  - Param `MappingJsonPath` - The absolute path to the `mapping.json` file supports both `json` and `jsonc`.
  - Param `AssemblyPath` - The absolute path to the dll generated from the `deobfuscate` command.

---

- `GenRefCountList` - Generates a print out of the most used classes. Useful to prioritize remap targets.
  - Param `AssemblyPath` - The absolute path to your de-obfuscated and remapped dll.

---

- `Dumper` - Generates a dumper zip.
  - Param `ManagedDirectory` - The absolute path to your Managed folder for EFT, folder must contain all references to 
be resolved. Assembly-CSharp-cleaned.dll, mscorlib.dll, FilesChecker.dll