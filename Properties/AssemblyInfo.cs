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
[assembly: Guid("203dfbf1-3599-43fd-8487-e1c79c2b788f")]

[assembly: ModDependency("game")]
[assembly: ModInfo("RedirectLogs", "redirectlogs", Version = "1.0.0", Authors = new string[] { "Tyron" },
        Website = "https://github.com/anegostudios/vsmodtemplate", Description = "Redirecting logs to VS", RequiredOnClient = false)]
