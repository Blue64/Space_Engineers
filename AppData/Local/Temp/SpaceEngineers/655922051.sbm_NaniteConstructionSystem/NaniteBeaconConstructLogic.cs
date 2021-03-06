using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Game;
using System.Collections.Generic;
using VRageMath;
using VRage.Utils;
using System;


namespace NaniteConstructionSystem.Entities.Beacons
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenFarm), true, "LargeNaniteBeaconConstruct", "SmallNaniteBeaconConstruct")]
    public class NaniteBeaconConstructLogic : MyGameLogicComponent
    {
        private NaniteBeacon m_beacon = null;
        private static FastResourceLock m_lock = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (m_lock == null)
                m_lock = new FastResourceLock();

            base.Init(objectBuilder);

            using (m_lock.AcquireExclusiveUsing())
            {
                Logging.Instance.WriteLine(string.Format("ADDING Repair Beacon: {0}", Entity.EntityId));
                m_beacon = new NaniteBeaconConstruct(Entity as IMyTerminalBlock);
                NaniteConstructionManager.BeaconList.Add(m_beacon);
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return null;
        }
    }
}
