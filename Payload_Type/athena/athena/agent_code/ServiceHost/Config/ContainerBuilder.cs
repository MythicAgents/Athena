using Autofac;
using Workflow.Contracts;
using Workflow.Providers;
using System.Reflection;
using Workflow.Security;
using Workflow.Models;
using Workflow.Utilities;

namespace Workflow.Config
{
    public static class ContainerBuilder
    {
        /// The order of the registrations is important. If a registration relies, directly or indirectly, on another registration, 
        /// then the dependent registration must be added after the registration it depends on. 
        /// 
        /// AutoFac will inject the necessary registration into the constructor of the class
        /// so be sure to add it there so that it can be injected. Example: We have a class that wants to use the ServiceConfig and RequestDispatcher
        /// The constructor should be public MyClass(IServiceConfig config, IRequestDispatcher manager) 
        /// {
        ///     this.config = config;
        ///     this.manager = manager;
        /// }
        /// 
        /// AutoFac will inject the necessary dependencies into this constructor.
        public static Autofac.ContainerBuilder Build()
        {
            DebugLog.Log("ContainerBuilder.Build() starting");

            var containerBuilder = new Autofac.ContainerBuilder();
            DebugLog.Log("Registering DiagnosticService as ILogger");
            containerBuilder.RegisterType<DiagnosticService>().As<ILogger>().SingleInstance();
            DebugLog.Log("Registering ServiceConfig as IServiceConfig");
            containerBuilder.RegisterType<ServiceConfig>().As<IServiceConfig>().SingleInstance();
            DebugLog.Log("Registering RuntimeExecutor as IRuntimeExecutor");
            containerBuilder.RegisterType<RuntimeExecutor>().As<IRuntimeExecutor>();
            DebugLog.Log("Registering SecurityProvider as ISecurityProvider");
            containerBuilder.RegisterType<SecurityProvider>().As<ISecurityProvider>().SingleInstance();

            DebugLog.Log("Registering DataBroker as IDataBroker");
            containerBuilder.RegisterType<DataBroker>().As<IDataBroker>().SingleInstance();

            DebugLog.Log("Registering CredentialProvider as ICredentialProvider");
            containerBuilder.RegisterType<CredentialProvider>().As<ICredentialProvider>().SingleInstance();
            DebugLog.Log("Registering ComponentProvider as IComponentProvider");
            containerBuilder.RegisterType<ComponentProvider>().As<IComponentProvider>().SingleInstance();
            DebugLog.Log("Registering RequestDispatcher as IRequestDispatcher");
            containerBuilder.RegisterType<RequestDispatcher>().As<IRequestDispatcher>().SingleInstance();
            DebugLog.Log("Registering ScriptEngine as IScriptEngine");
            containerBuilder.RegisterType<ScriptEngine>().As<IScriptEngine>().SingleInstance();
            DebugLog.Log("Registering PluginContext");
            containerBuilder.Register(c => new PluginContext(
                c.Resolve<IDataBroker>(),
                c.Resolve<IServiceConfig>(),
                c.Resolve<ILogger>(),
                c.Resolve<ICredentialProvider>(),
                c.Resolve<IRuntimeExecutor>(),
                c.Resolve<IScriptEngine>()
            )).SingleInstance();
            TryLoadProfiles(containerBuilder);
            DebugLog.Log("Registering ServiceHost as IService");
            containerBuilder.RegisterType<ServiceHost>().As<IService>().SingleInstance();
            DebugLog.Log("ContainerBuilder.Build() complete");
            return containerBuilder;
        }
        private static void TryLoadProfiles(
            Autofac.ContainerBuilder containerBuilder)
        {
            var entryAsm = Assembly.GetEntryAssembly();
            if (entryAsm is null) return;

            var scannedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            scannedNames.Add(entryAsm.GetName().Name ?? "");

            foreach (var refName
                in entryAsm.GetReferencedAssemblies())
            {
                if (refName.Name is null) continue;
                if (refName.Name.StartsWith("System.")
                    || refName.Name.StartsWith("Microsoft."))
                    continue;

                scannedNames.Add(refName.Name);
                try
                {
                    DebugLog.Log(
                        "TryLoadProfiles: scanning "
                        + refName.Name);
                    Assembly asm = Assembly.Load(refName.FullName);
                    containerBuilder
                        .RegisterAssemblyTypes(asm)
                        .Where(t => typeof(IChannel)
                            .IsAssignableFrom(t))
                        .As<IChannel>().SingleInstance();
                }
                catch (FileNotFoundException)
                {
                    DebugLog.Log(
                        "TryLoadProfiles: "
                        + refName.Name + " not found");
                }
                catch (Exception ex)
                {
                    DebugLog.Log(
                        "TryLoadProfiles: failed "
                        + refName.Name + ": " + ex.Message);
                }
            }

            // Also scan DLLs present in the application directory that were not
            // reachable via GetReferencedAssemblies() (e.g. channel DLLs
            // deployed by build targets as loose files).
            foreach (var dllPath in Directory.GetFiles(
                AppContext.BaseDirectory, "*.dll"))
            {
                var baseName = Path.GetFileNameWithoutExtension(dllPath);
                if (scannedNames.Contains(baseName)) continue;
                if (baseName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                    || baseName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    DebugLog.Log("TryLoadProfiles: scanning (dir) " + baseName);
                    var asm = Assembly.LoadFrom(dllPath);
                    containerBuilder
                        .RegisterAssemblyTypes(asm)
                        .Where(t => typeof(IChannel).IsAssignableFrom(t))
                        .As<IChannel>().SingleInstance();
                }
                catch (Exception ex)
                {
                    DebugLog.Log(
                        "TryLoadProfiles: failed (dir) "
                        + baseName + ": " + ex.Message);
                }
            }
        }
    }
}
