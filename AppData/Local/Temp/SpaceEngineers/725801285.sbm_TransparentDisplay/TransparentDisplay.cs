using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.TransparentDisplay
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class TransparentDisplayMod : MySessionComponentBase
    {
        private bool init = false;

        public override void LoadData()
        {
            Log.SetUp("Transparent Display", 725801285, "TransparentDisplay");
        }

        public void Init()
        {
            init = true;
            Log.Init();

            MyAPIGateway.Utilities.InvokeOnGameThread(() => SetUpdateOrder(MyUpdateOrder.NoUpdate));
        }

        protected override void UnloadData()
        {
            try
            {
                if(init)
                {
                    init = false;
                    Log.Info("Mod unloaded.");
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            Log.Close();
        }

        public override void UpdateAfterSimulation()
        {
            if(!init)
            {
                if(MyAPIGateway.Session == null)
                    return;

                Init();
            }
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), false, "LargeTransparentDisplay", "MediumTransparentDisplay")]
    public class TransparentDisplay : MyGameLogicComponent
    {
        private bool prevFuntional = true;
        private float transparency = DISPLAY_TRANSPARENCY;

        private const float DISPLAY_TRANSPARENCY = 0.5f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(Entity.Render == null || !Entity.InScene || Entity.MarkedForClose)
                    return;

                var panel = Entity as IMyTextPanel;

                if(prevFuntional != panel.IsFunctional) // when functional state changes
                {
                    var panelSlim = panel.CubeGrid.GetCubeBlock(panel.Position);
                    var panelDef = ((MyCubeBlock)panel).BlockDefinition;

                    prevFuntional = panel.IsFunctional;
                    transparency = (panelSlim.BuildLevelRatio >= panelDef.CriticalIntegrityRatio ? DISPLAY_TRANSPARENCY : 0); // make it opaque if build model is used
                }

                if(Math.Abs(Entity.Render.Transparency - transparency) > 0.001f)
                {
                    Entity.Render.Transparency = transparency;
                    Entity.Render.RemoveRenderObjects();
                    Entity.Render.AddRenderObjects();

                    if(panel.IsFunctional)
                        Entity.SetEmissiveParts("ScreenArea", Color.White, 1); // fixes the screen being non-emissive
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}