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

            var containerBuilder = new Autofac.ContainerBuilder();
            //containerBuilder.RegisterType<ContainerProvider>().SingleInstance();
            //Register the Agent Configuration, which is needed by all services.
            containerBuilder.RegisterType<DiagnosticService>().As<ILogger>().SingleInstance();
            containerBuilder.RegisterType<ServiceConfig>().As<IServiceConfig>().SingleInstance();
            containerBuilder.RegisterType<RuntimeExecutor>().As<IRuntimeExecutor>();
            //ICrypto is required by the IChannel to be able to encrypt and decrypt messages.
            containerBuilder.RegisterType<SecurityProvider>().As<ISecurityProvider>().SingleInstance();

            //DataBroker is in use by Plugins & Other Managers so should be early in the chain.
            containerBuilder.RegisterType<DataBroker>().As<IDataBroker>().SingleInstance();

            containerBuilder.RegisterType<CredentialProvider>().As<ICredentialProvider>().SingleInstance();
            containerBuilder.RegisterType<ComponentProvider>().As<IComponentProvider>().SingleInstance();
            containerBuilder.RegisterType<RequestDispatcher>().As<IRequestDispatcher>().SingleInstance();
            containerBuilder.RegisterType<ScriptEngine>().As<IScriptEngine>().SingleInstance();
            TryLoadProfiles(containerBuilder);
            //Finally register the Agent
            containerBuilder.RegisterType<ServiceHost>().As<IService>().SingleInstance();
            return containerBuilder;
        }
        private static void TryLoadProfiles(Autofac.ContainerBuilder containerBuilder)
        {
            List<string> potentialProfiles = new List<string> { "DebugProfile", "Http", "Websocket", "Slack", "Discord", "Smb", "GitHub" };

            foreach(var profile in potentialProfiles)
            {
                try
                {
                    Assembly _tasksAsm = Assembly.Load($"Workflow.Channels.{profile}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
                    containerBuilder.RegisterAssemblyTypes(_tasksAsm).As<IChannel>().SingleInstance();
                }
                catch 
                {
                }
            }
        }
    }
}
