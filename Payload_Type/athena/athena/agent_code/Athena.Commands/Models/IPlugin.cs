using Athena.Models.Comms.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Commands.Models
{
    public interface IPlugin
    {
        /// <summary>
        /// The name of the plugin
        /// </summary>
        public string Name { get; }

        ///// <summary>
        ///// This controls whether the plugin is currently running or not. Interactive plugins that are running will instead have the Send method called.
        ///// </summary>
        //public bool Running { get; set; }

        /// <summary>
        /// This controls whether the plugin supports interactivity or not. You'll have the ability to freely communicate with the plugin via the interactivity hook in Mythic
        /// </summary>
        public bool Interactive { get; }

        /// <summary>
        /// Executes the plugin.
        /// </summary>
        public void Start(Dictionary<string, string> args);

        /// <summary>
        /// Send data to the executing plugin. This is only used if the plugin is interactive.
        /// </summary>
        public void Interact(InteractiveMessage message);

        /// <summary>
        /// Stop the plugin
        /// </summary>
        public void Stop(string task_id);
        /// <summary>
        /// This determines whether the plugin is currently running or not. Interactive plugins that are running will instead have the Send method called.
        /// </summary>
        public bool IsRunning();
    }
}
