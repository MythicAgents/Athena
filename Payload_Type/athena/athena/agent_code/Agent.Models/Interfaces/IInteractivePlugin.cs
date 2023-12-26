using Agent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Interfaces
{
    public interface IInteractivePlugin : IPlugin
    {
        public void Interact(InteractMessage message);
    }
}
