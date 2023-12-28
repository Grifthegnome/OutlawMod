using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vintagestory.API.Common;

[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("fbddd71f-ae07-4e29-be3a-6ec7d9f60dab")]

[assembly: ModInfo( "expandedaitasksloader", "expandedaitasksloader",
    Version = "1.0.0",
    Description = "Loads the contents of ExpandedAiTasks.dll for other mods",
    Authors = new[] { "Grifthegnome" })]

 [assembly: ModDependency("game")]
