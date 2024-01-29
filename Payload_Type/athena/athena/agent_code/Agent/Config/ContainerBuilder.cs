using Autofac;
using Agent.Interfaces;
using Agent.Managers;
using System.Reflection;
using Agent.Crypto;
using System.Diagnostics;
using Agent.Utlities;

namespace Agent.Config
{
    public static class ContainerBuilder
    {
        /// The order of the registrations is important. If a registration relies, directly or indirectly, on another registration, 
        /// then the dependent registration must be added after the registration it depends on. 
        /// 
        /// AutoFac will inject the necessary registration into the constructor of the class
        /// so be sure to add it there so that it can be injected. Example: We have a class that wants to use the AgentConfig and TaskManager
        /// The constructor should be public MyClass(IAgentConfig config, ITaskManager manager) 
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
            containerBuilder.RegisterType<LogManager>().As<ILogger>().SingleInstance();
            containerBuilder.RegisterType<AgentConfig>().As<IAgentConfig>().SingleInstance();
            containerBuilder.RegisterType<ProcessSpawner>().As<ISpawner>();
            //ICrypto is required by the IProfile to be able to encrypt and decrypt messages.
            containerBuilder.RegisterType<AgentCrypto>().As<ICryptoManager>().SingleInstance();

            //MessageManager is in use by Plugins & Other Managers so should be early in the chain.
            containerBuilder.RegisterType<MessageManager>().As<IMessageManager>().SingleInstance();

            containerBuilder.RegisterType<TokenManager>().As<ITokenManager>().SingleInstance();
            containerBuilder.RegisterType<AssemblyManager>().As<IAssemblyManager>().SingleInstance();
            containerBuilder.RegisterType<TaskManager>().As<ITaskManager>().SingleInstance();
            TryLoadProfiles(containerBuilder);
            //Finally register the Agent
            containerBuilder.RegisterType<Agent>().As<IAgent>().SingleInstance();
            return containerBuilder;
        }
        private static void TryLoadProfiles(Autofac.ContainerBuilder containerBuilder)
        {
            List<string> potentialProfiles = new List<string> { "DebugProfile", "Http", "Websocket", "Slack", "Discord", "Smb" };

            foreach(var profile in potentialProfiles)
            {
                try
                {
                    Assembly _tasksAsm = Assembly.Load($"Agent.Profiles.{profile}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
                    containerBuilder.RegisterAssemblyTypes(_tasksAsm).As<IProfile>().SingleInstance();
                }
                catch 
                {
                }
            }
        }
    }
}
