using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;

using System;

using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Eikester.LCDPillar
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), true,
        new string[]
        {
            "Eikester_LCDAdSign_TopMounted_1x1",
            "Eikester_LCDAdSign_TopMounted_2x1",
            "Eikester_LCDAdSign_TopMounted_1x2",
            "Eikester_LCDAdSign_WallMounted_1x1",
            "Eikester_LCDAdSign_WallMounted_2x1",
            "Eikester_LCDAdSign_WallMounted_1x2",
            "Eikester_LCDAdSign_BottomMounted_1x1",
            "Eikester_LCDAdSign_BottomMounted_2x1",
            "Eikester_LCDAdSign_BottomMounted_1x2"
        }
    )]
    public class LCDAdSigns : MyGameLogicComponent
    {
        public override void Close()
        {
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }

        void UpdateEmissive()
        {
            try
            {
                Entity.SetEmissiveParts("Emissive", Color.Turquoise, 1);
            }
            catch
            {

            }
        }

        public override void UpdateAfterSimulation()
        {
            UpdateEmissive();
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), true,
        new string[]
        {
            "Eikester_LCDPillar_Round"
        }
    )]
    public class LCDPillar : MyGameLogicComponent
    {
        float rotation = 0.25f;

        Sandbox.ModAPI.IMyTextPanel lcd;
        MyEntitySubpart subpart;
        
        public override void Close() 
        {
        } 

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        { 
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            lcd = Entity as Sandbox.ModAPI.IMyTextPanel;
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
        
        void UpdateMatrix()
        {
            try
            {
                if (!lcd.IsFunctional || !lcd.IsWorking)
                    return;

                subpart = Entity.GetSubpart("top");
                if (subpart != null)
                {
                    var pos = subpart.PositionComp.LocalMatrix;
                    var rot = MatrixD.CreateFromAxisAngle(pos.Up, MathHelper.ToRadians(-rotation));

                    var tr = pos.Translation;
                    pos = pos * rot;
                    pos.Translation = tr;

                    subpart.PositionComp.LocalMatrix = pos;
                }

                if (lcd != null)
                {
                    var pos = lcd.PositionComp.LocalMatrix;
                    var rot = MatrixD.CreateFromAxisAngle(pos.Up, MathHelper.ToRadians(rotation));

                    var tr = pos.Translation;
                    pos = pos * rot;
                    pos.Translation = tr;

                    lcd.PositionComp.LocalMatrix = pos;
                }
            }
            catch
            {

            }
        }

        void UpdateEmissive()
        {
            try
            {
                Entity.SetEmissivePartsForSubparts("Emissive", Color.Turquoise, 1);
            }
            catch
            {

            }
        }
        
        public override void UpdateAfterSimulation()
        {
            UpdateMatrix();
            UpdateEmissive();
        }
    }
}
