using Workflow.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workflow.Contracts
{
    public interface IInteractiveModule : IModule
    {
        public void Interact(InteractMessage message);
    }
}
