using System;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace Kage.HackingComputer
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Main : MySessionComponentBase
    {
        private bool m_init = false;
        public override void UpdateBeforeSimulation()
        {
            if (!m_init || MyAPIGateway.Session != null)
            {
                m_init = true;
                HackingComputerSync.Initialize();
            }
        }

        protected override void UnloadData()
        {
            HackingComputerSync.Unload();
            LogManager.Unload();
        }
    }
}