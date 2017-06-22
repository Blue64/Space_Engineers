using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GyroUpgrade
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Gyro))]
    public class MyGyroUpgradeLogic : MyGameLogicComponent
    {
        private IMyGyro m_gyro;
        private Sandbox.ModAPI.IMyCubeBlock m_parent;
        private MyObjectBuilder_EntityBase m_objectBuilder = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            m_gyro = Entity as IMyGyro;
            m_parent = Entity as Sandbox.ModAPI.IMyCubeBlock;

            m_parent.AddUpgradeValue("GyroBoost", 0f);

            m_objectBuilder = objectBuilder;

            m_parent.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return m_objectBuilder;
        }

        private void OnUpgradeValuesChanged()
        {
            m_gyro.GyroStrengthMultiplier = m_parent.UpgradeValues["GyroBoost"] + 1f;
            m_gyro.PowerConsumptionMultiplier = m_parent.UpgradeValues["GyroBoost"] + 1f;
        }
    }
}
