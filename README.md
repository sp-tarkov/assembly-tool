# Commands

- `addmissingproperties` - This command will add missing properties to the provided mapping.json.
  - Param `MappingsPath` - Path to the mapping.json file to be fixed
  
- Note: This is a development command and should not be used unless you know what you're doing. Always make backups when using this command.

---

- `automatch` - This command will Automatically try to generate a mapping object given old type and new type names.
  - Param `AssemblyPath` - The absolute path to your assembly, folder must contain all references to be resolved.
  - Param `MappingsPath` - Path to your mapping file so it can be updated if a match is found.
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
  - Param `MappingPath` - The absolute path to mapping.json.
  - Param `AssemblyPath` - The absolute path to the assembly you want to regenerate the signature for.
  - Param `NewTypeName` - The new type name as listed in the mapping file.
  - Param `OldTypeName` - Full old type name including namespace `Foo.Bar` for nested classes `Foo.Bar/FooBar`

---

- `remap` - Generates a re-mapped dll provided a mapping file and dll. If the dll is obfuscated, it will automatically de-obfuscate.
  - Param `MappingJsonPath` - The absolute path to the `mapping.json` file supports both `json` and `jsonc`.
  - Param `AssemblyPath` - The absolute path to the dll generated from the `deobfuscate` command.

