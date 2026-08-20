using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class BootstrapPatcher
{
    private const string PatchMarker = "HeadTracking_Patched_EternalAfternoon_v1";
    private const string BootstrapTypeName = "HeadTrackingBootstrap";
    private const string InjectedNamespace = "EternalAfternoonHeadTracking";

    /// <summary>
    /// Finds a type by full name across all resolved assemblies,
    /// following type forwarders (netstandard forwards to mscorlib at runtime).
    /// </summary>
    private static TypeDefinition FindType(string fullName, List<AssemblyDefinition> resolvedAssemblies, IAssemblyResolver resolver)
    {
        foreach (var asm in resolvedAssemblies)
        {
            var t = asm.MainModule.Types.FirstOrDefault(x => x.FullName == fullName);
            if (t != null) return t;

            // Check type forwarders (netstandard -> mscorlib)
            foreach (var fwd in asm.MainModule.ExportedTypes)
            {
                if (fwd.FullName == fullName && fwd.IsForwarder)
                {
                    var scope = fwd.Scope as AssemblyNameReference;
                    if (scope != null)
                    {
                        try
                        {
                            var fwdAsm = resolver.Resolve(scope);
                            var fwdType = fwdAsm.MainModule.Types.FirstOrDefault(x => x.FullName == fullName);
                            if (fwdType != null) return fwdType;
                        }
                        catch { }
                    }
                }
            }
        }
        throw new Exception("Could not find type: " + fullName);
    }

    public static bool PatchAssembly(string assemblyPath)
    {
        string managedDir = Path.GetDirectoryName(assemblyPath);

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedDir);

        var readerParams = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadWrite = false,
            InMemory = true
        };

        byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
        using (var memStream = new MemoryStream(assemblyBytes))
        using (var assembly = AssemblyDefinition.ReadAssembly(memStream, readerParams))
        {
            if (assembly.MainModule.Types.Any(t => t.Name == PatchMarker))
            {
                Console.WriteLine("  Assembly already patched - skipping");
                return true;
            }

            // Create bootstrap class with static Initialize method that uses reflection
            var bootstrapType = new TypeDefinition(
                InjectedNamespace,
                BootstrapTypeName,
                TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract,
                assembly.MainModule.TypeSystem.Object);

            var initializedField = new FieldDefinition(
                "_initialized",
                FieldAttributes.Private | FieldAttributes.Static,
                assembly.MainModule.TypeSystem.Boolean);
            bootstrapType.Fields.Add(initializedField);

            var initMethod = new MethodDefinition(
                "Initialize",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                assembly.MainModule.TypeSystem.Void);

            var il = initMethod.Body.GetILProcessor();
            initMethod.Body.InitLocals = true;

            // Local variables
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.String)); // 0: managedDir
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.String)); // 1: dllPath
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object)); // 2: assembly
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object)); // 3: type
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object)); // 4: method
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object)); // 5: exception

            // Import required methods - resolve types across all referenced assemblies
            // (modern Unity uses netstandard, not mscorlib, so we search all refs)
            var resolvedAssemblies = new List<AssemblyDefinition>();
            foreach (var asmRef in assembly.MainModule.AssemblyReferences)
            {
                try { resolvedAssemblies.Add(resolver.Resolve(asmRef)); }
                catch { }
            }

            var assemblyType = FindType("System.Reflection.Assembly", resolvedAssemblies, resolver);
            var loadFromRef = assembly.MainModule.ImportReference(
                assemblyType.Methods.First(m => m.Name == "LoadFrom" && m.Parameters.Count == 1));
            var getTypeRef = assembly.MainModule.ImportReference(
                assemblyType.Methods.First(m => m.Name == "GetType" && m.Parameters.Count == 1));
            var getLocationRef = assembly.MainModule.ImportReference(
                assemblyType.Properties.First(p => p.Name == "Location").GetMethod);
            var getExecutingAssemblyRef = assembly.MainModule.ImportReference(
                assemblyType.Methods.First(m => m.Name == "GetExecutingAssembly"));

            var typeType = FindType("System.Type", resolvedAssemblies, resolver);
            var getMethodRef = assembly.MainModule.ImportReference(
                typeType.Methods.First(m => m.Name == "GetMethod" && m.Parameters.Count == 1));

            var methodBaseType = FindType("System.Reflection.MethodBase", resolvedAssemblies, resolver);
            var invokeRef = assembly.MainModule.ImportReference(
                methodBaseType.Methods.First(m => m.Name == "Invoke" && m.Parameters.Count == 2));

            var pathType = FindType("System.IO.Path", resolvedAssemblies, resolver);
            var getDirectoryNameRef = assembly.MainModule.ImportReference(
                pathType.Methods.First(m => m.Name == "GetDirectoryName"));
            var combineRef = assembly.MainModule.ImportReference(
                pathType.Methods.First(m => m.Name == "Combine" && m.Parameters.Count == 2));
            var getTempPathRef = assembly.MainModule.ImportReference(
                pathType.Methods.First(m => m.Name == "GetTempPath"));

            var exceptionType = FindType("System.Exception", resolvedAssemblies, resolver);
            var toStringRef = assembly.MainModule.ImportReference(
                exceptionType.Methods.First(m => m.Name == "ToString" && m.Parameters.Count == 0));

            var fileType = FindType("System.IO.File", resolvedAssemblies, resolver);
            var appendAllTextRef = assembly.MainModule.ImportReference(
                fileType.Methods.First(m => m.Name == "AppendAllText" && m.Parameters.Count == 2));
            // The first write of each boot log truncates it, so the file a user sends in
            // only ever holds the current launch. The success line below appends to it.
            var writeAllTextRef = assembly.MainModule.ImportReference(
                fileType.Methods.First(m => m.Name == "WriteAllText" && m.Parameters.Count == 2));

            var stringType = FindType("System.String", resolvedAssemblies, resolver);
            var concatRef = assembly.MainModule.ImportReference(
                stringType.Methods.First(m => m.Name == "Concat" && m.Parameters.Count == 2
                    && m.Parameters[0].ParameterType.FullName == "System.String"));

            // Build the method body
            // Single Ret instruction used as both fast-path branch target and leave target.
            // CRITICAL: this instruction MUST be appended to the method body (see il.Append below)
            // or branch targets become invalid IL causing infinite loops.
            var leaveTarget = il.Create(OpCodes.Ret);
            var tryStart = il.Create(OpCodes.Nop);
            var catchStart = il.Create(OpCodes.Nop);

            // Check if already initialized - fast path
            il.Append(il.Create(OpCodes.Ldsfld, initializedField));
            il.Append(il.Create(OpCodes.Brtrue, leaveTarget));

            // Set initialized = true
            il.Append(il.Create(OpCodes.Ldc_I4_1));
            il.Append(il.Create(OpCodes.Stsfld, initializedField));

            // try {
            il.Append(tryStart);

            // managedDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            il.Append(il.Create(OpCodes.Call, getExecutingAssemblyRef));
            il.Append(il.Create(OpCodes.Callvirt, getLocationRef));
            il.Append(il.Create(OpCodes.Call, getDirectoryNameRef));
            il.Append(il.Create(OpCodes.Stloc_0));

            // dllPath = Path.Combine(managedDir, "EternalAfternoonHeadTracking.dll")
            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldstr, "EternalAfternoonHeadTracking.dll"));
            il.Append(il.Create(OpCodes.Call, combineRef));
            il.Append(il.Create(OpCodes.Stloc_1));

            // Log to boot log
            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldstr, "\\HeadTracking_BOOT.log"));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Ldstr, "Loading EternalAfternoonHeadTracking.dll...\n"));
            il.Append(il.Create(OpCodes.Call, writeAllTextRef));

            // assembly = Assembly.LoadFrom(dllPath)
            il.Append(il.Create(OpCodes.Ldloc_1));
            il.Append(il.Create(OpCodes.Call, loadFromRef));
            il.Append(il.Create(OpCodes.Stloc_2));

            // type = assembly.GetType("EternalAfternoonHeadTracking.ModLoader")
            il.Append(il.Create(OpCodes.Ldloc_2));
            il.Append(il.Create(OpCodes.Ldstr, "EternalAfternoonHeadTracking.ModLoader"));
            il.Append(il.Create(OpCodes.Callvirt, getTypeRef));
            il.Append(il.Create(OpCodes.Stloc_3));

            // method = type.GetMethod("Initialize")
            il.Append(il.Create(OpCodes.Ldloc_3));
            il.Append(il.Create(OpCodes.Ldstr, "Initialize"));
            il.Append(il.Create(OpCodes.Callvirt, getMethodRef));
            il.Append(il.Create(OpCodes.Stloc, 4));

            // method.Invoke(null, null)
            il.Append(il.Create(OpCodes.Ldloc, 4));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Callvirt, invokeRef));
            il.Append(il.Create(OpCodes.Pop));

            // Log success
            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldstr, "\\HeadTracking_BOOT.log"));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Ldstr, "SUCCESS: ModLoader.Initialize() called\n"));
            il.Append(il.Create(OpCodes.Call, appendAllTextRef));
            il.Append(il.Create(OpCodes.Leave, leaveTarget));

            // } catch (Exception ex) {
            il.Append(catchStart);
            il.Append(il.Create(OpCodes.Stloc, 5));

            // Log error to temp path
            il.Append(il.Create(OpCodes.Call, getTempPathRef));
            il.Append(il.Create(OpCodes.Ldstr, "HeadTracking_BOOT_ERROR.log"));
            il.Append(il.Create(OpCodes.Call, combineRef));

            il.Append(il.Create(OpCodes.Ldstr, "ERROR: "));
            il.Append(il.Create(OpCodes.Ldloc, 5));
            il.Append(il.Create(OpCodes.Callvirt, toStringRef));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Ldstr, "\n"));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Call, writeAllTextRef));

            il.Append(il.Create(OpCodes.Leave, leaveTarget));
            // }

            il.Append(leaveTarget);

            // Add exception handler
            var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                TryStart = tryStart,
                TryEnd = catchStart,
                HandlerStart = catchStart,
                HandlerEnd = leaveTarget,
                CatchType = assembly.MainModule.ImportReference(exceptionType)
            };
            initMethod.Body.ExceptionHandlers.Add(handler);

            bootstrapType.Methods.Add(initMethod);
            assembly.MainModule.Types.Add(bootstrapType);

            // Find target type to inject into
            // Eternal Afternoon game types - inject into PlayerScript or UserInput Update()
            string[] targetTypeNames = new string[] { "PlayerScript", "UserInput", "GameplaySceneReferencesManager" };
            TypeDefinition targetType = null;
            string targetTypeName = null;

            foreach (var typeName in targetTypeNames)
            {
                targetType = assembly.MainModule.Types.FirstOrDefault(t => t.Name == typeName);
                if (targetType == null)
                {
                    // Search by full name suffix for namespaced types
                    foreach (var moduleType in assembly.MainModule.Types)
                    {
                        if (moduleType.Name == typeName || moduleType.FullName.EndsWith("." + typeName))
                        {
                            targetType = moduleType;
                            break;
                        }
                    }
                }
                if (targetType != null)
                {
                    targetTypeName = typeName;
                    break;
                }
            }

            if (targetType == null)
            {
                Console.WriteLine("  ERROR: Could not find any target type to patch");
                return false;
            }

            Console.WriteLine("  Found target type: " + targetTypeName);

            // Inject into Update() (preferred for per-frame recreation) or Start()/Awake()
            var targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == "Update" && !m.IsStatic && m.HasBody);
            if (targetMethod == null)
                targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == "Start" && !m.IsStatic && m.HasBody);
            if (targetMethod == null)
                targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == "Awake" && !m.IsStatic && m.HasBody);

            if (targetMethod == null)
            {
                Console.WriteLine("  ERROR: Could not find Update, Start, or Awake method in " + targetTypeName);
                return false;
            }

            // Inject call to bootstrap at start of method
            var targetIL = targetMethod.Body.GetILProcessor();
            var firstInstruction = targetMethod.Body.Instructions.First();
            targetIL.InsertBefore(firstInstruction, targetIL.Create(OpCodes.Call, initMethod));
            Console.WriteLine("  Injected HeadTrackingBootstrap.Initialize() into " + targetTypeName + "." + targetMethod.Name);

            // Add marker type to prevent double-patching
            var markerType = new TypeDefinition(
                InjectedNamespace,
                PatchMarker,
                TypeAttributes.NotPublic | TypeAttributes.Class,
                assembly.MainModule.TypeSystem.Object);
            assembly.MainModule.Types.Add(markerType);

            assembly.Write(assemblyPath);
            Console.WriteLine("  Successfully patched " + Path.GetFileName(assemblyPath));
            return true;
        }
    }

    public static bool UnpatchAssembly(string assemblyPath)
    {
        string managedDir = Path.GetDirectoryName(assemblyPath);

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedDir);

        var readerParams = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadWrite = false,
            InMemory = true
        };

        byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
        using (var memStream = new MemoryStream(assemblyBytes))
        using (var assembly = AssemblyDefinition.ReadAssembly(memStream, readerParams))
        {
            var module = assembly.MainModule;

            bool hasMarker = module.Types.Any(t => t.Name == PatchMarker);
            bool hasBootstrap = module.Types.Any(t => t.Namespace == InjectedNamespace && t.Name == BootstrapTypeName);
            if (!hasMarker && !hasBootstrap)
            {
                Console.WriteLine("  Assembly is not patched - nothing to unpatch");
                return true;
            }

            int removedCalls = 0;
            foreach (var type in module.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    var il = method.Body.GetILProcessor();
                    var toRemove = method.Body.Instructions
                        .Where(instr => (instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt)
                            && instr.Operand is MethodReference
                            && ((MethodReference)instr.Operand).DeclaringType != null
                            && ((MethodReference)instr.Operand).DeclaringType.Name == BootstrapTypeName)
                        .ToList();
                    foreach (var instr in toRemove)
                    {
                        il.Remove(instr);
                        removedCalls++;
                    }
                }
            }

            var removeTypes = module.Types
                .Where(t => t.Name == PatchMarker
                    || (t.Namespace == InjectedNamespace && t.Name == BootstrapTypeName))
                .ToList();
            foreach (var t in removeTypes)
                module.Types.Remove(t);

            assembly.Write(assemblyPath);
            Console.WriteLine("  Unpatched " + Path.GetFileName(assemblyPath)
                + " (removed " + removedCalls + " bootstrap call(s), " + removeTypes.Count + " type(s))");
            return true;
        }
    }
}
