using VRageMath;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Entity;
using Sandbox.ModAPI;
using System.IO;
using Sandbox.Game.Entities;

namespace Eikester.ContainerStatus
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CargoContainer), false, 
        new string[] 
        {
            "LargeBlockSmallContainer",
            "LargeBlockLargeContainer",
            "SmallBlockSmallContainer",
            "SmallBlockMediumContainer",
            "SmallBlockLargeContainer"
        }
    )]
    public class Container : MyGameLogicComponent
    {
        IMyCubeBlock m_block;

        private Color COLOROFF = new Color(1, 1, 1);
        private Color GREEN = new Color(0, 200, 0);
        private Color ORANGE = new Color(200, 100, 0);
        private Color RED = new Color(200, 0, 0);

        long currentVolume = 0;
        long maxVolume = 0;

        MyEntity m_display;
        
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Container.Entity.GetObjectBuilder(copy);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            
            m_block = (IMyCubeBlock)Entity;
            m_block.NeedsWorldMatrix = true;
        }

        MyEntity LoadDisplay()
        {
            if (m_block == null)
                return null;

            string model = "Display_" + m_block.BlockDefinition.SubtypeId + ".mwm";
            
            MyEntity entity;
            entity = new MyEntity();
            entity.Init(null, GetModelsFolder() + model, (MyEntity)m_block, null, null);
            entity.Render.EnableColorMaskHsv = true;
            entity.Render.ColorMaskHsv = m_block.Render.ColorMaskHsv;
            entity.Render.PersistentFlags = MyPersistentEntityFlags2.CastShadows;

            entity.PositionComp.LocalMatrix = 
                Matrix.CreateFromTransformScale(
                    Quaternion.Identity,
                    Vector3.Zero,
                    Vector3.One);

            entity.Flags = EntityFlags.NeedsWorldMatrix | EntityFlags.Visible | EntityFlags.NeedsDraw | EntityFlags.NeedsDrawFromParent | EntityFlags.InvalidateOnMove;
            entity.OnAddedToScene(m_block);

            return entity;
        }

        private string GetModelsFolder()
        {
            ulong publishID = 0;
            var mods = MyAPIGateway.Session.GetCheckpoint("null").Mods;
            foreach (var mod in mods)
            {
                if (mod.PublishedFileId == 677790017)
                    publishID = mod.PublishedFileId;
            }

            if (publishID != 0)
                return Path.GetFullPath(string.Format(@"{0}\{1}.sbm\Models\", MyAPIGateway.Utilities.GamePaths.ModsPath, publishID.ToString()));
            else
                return Path.GetFullPath(string.Format(@"{0}\{1}\Models\", MyAPIGateway.Utilities.GamePaths.ModsPath, "ContainerStatus"));
        }

        public long GetCurrentVolumeInPercent()
        {
            currentVolume = m_block.GetInventory(0).CurrentVolume.RawValue;
            maxVolume = m_block.GetInventory(0).MaxVolume.RawValue;
            
            if (maxVolume > 0)
                return (currentVolume * 100 / maxVolume);
            
            return 0;
        }

        public override void UpdateAfterSimulation()
        {
            // no need to display in Creative Mode
            //if (MyAPIGateway.Session.CreativeMode)
                //return;

            try
            {
                if(m_display == null)
                    m_display = LoadDisplay();

                UpdateEmissive();
            }
            catch
            {
            }
        }

        void UpdateEmissive()
        {
            long fill = GetCurrentVolumeInPercent();

            try
            {
                if (m_block.IsWorking && m_block.IsFunctional)
                {
                    for (int i = 1; i <= 100; i++)
                    {
                        if (i <= fill)
                        {
                            Color c = GREEN;
                            if (fill >= 75 && fill < 90)
                                c = ORANGE;
                            else if (fill >= 90)
                                c = RED;

                            m_display.SetEmissiveParts("Em_" + i, c, 1f);
                        }
                        else
                        {
                            m_display.SetEmissiveParts("Em_" + i, Color.White, 0.2f);
                        }
                    }
                }
                else
                {
                    for (int i = 1; i <= 100; i++)
                    {
                        m_display.SetEmissiveParts("Em_" + i, COLOROFF, 0f);
                    }
                }
            }
            catch
            {

            }
        }
    }
}