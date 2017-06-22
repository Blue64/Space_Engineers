using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Game;

using VRage.Game.Components;
using VRage.ObjectBuilders;
//using Sandbox.ModAPI.Ingame;


using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using Sandbox.ModAPI.Ingame;
//using Sandbox.ModAPI.Ingame;
/**/


namespace GeoThermal_Script
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Reactor), false, new string[] { "PHX_GeoTherm_React_Large" })]
    public class GeoTherm_Gen : MyGameLogicComponent
    {
        Sandbox.ModAPI.IMyReactor georeactor;
        Sandbox.ModAPI.IMyTerminalBlock terminalBlock;
        MyParticleEffect effect = null;
        MyParticleEffect effect2 = null;
        AtmosphereDetector atmoDet = new AtmosphereDetector();
        private float density = 0.0f;
        int counter = 0;
        int max = 10;
        float UpgradeAmount = 0f;
        List<GeoTherm_Turbine> turbines = new List<GeoTherm_Turbine>();

        private Color ColorOn = new Color(0, 255, 0);
        private Color ColorOff = new Color(255, 0, 0);
        private Color ColorEmpty = new Color(255, 255, 0);
        private Color ColorAtmo = new Color(0, 192, 233);
        private Color ColorVoxel = new Color(205, 173, 133);
        private Color ColorStation = new Color(255, 148, 0);
        /*
                private Color ColorOn = new Color(0.08f, 0.44f, 0.44f);
                private Color ColorOff = new Color(0.0f, 0.0f, 0.0f);
                private Color ColorEmpty = new Color(0.75f, 0.2f, 0.2f);
                private Color ColorAtmo = new Color(0.5f, 0.5f, 0.0f);
        */
        private float INTENSITYON = 1f;
        private float INTENSITYOFF = 0f;
        bool lightson = true;
        bool empty = true;
        bool inAtmos = false;


        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Container.Entity.GetObjectBuilder(copy);
        }

        public override void Close()
        {
            Stop();
            base.Close();
        }
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            //            base.Init(objectBuilder);
            if (MyAPIGateway.Session != null)
            {
                SetSession();
            }
            Container.Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
            georeactor = (Sandbox.ModAPI.IMyReactor)Entity;
            terminalBlock = Entity as Sandbox.ModAPI.IMyTerminalBlock;
            georeactor.AddUpgradeValue("MaxPowerOutput", 1f);
            this.BeforeRemovedFromContainer += GeoTherm_Gen_BeforeRemovedFromContainer;
        }

        public override void OnRemovedFromScene()
        {
            PreExit();
            base.OnRemovedFromScene();
        }
        public override void OnBeforeRemovedFromContainer()
        {
            PreExit();
            base.OnBeforeRemovedFromContainer();
        }
        private void GeoTherm_Gen_BeforeRemovedFromContainer(MyEntityComponentBase obj)
        {
            PreExit();
        }
        private void PreExit()
        {
            this.Stop();
            for (int x = 0; x < turbines.Count; x++)
                turbines[x] = null;
        }
        private void Upgrades()
        {
            if (MarkedForClose)
                return;

            int ct = 0;
            float amt = 0f;
            float inc = 0.2f;
            /*
            List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
            try
            {
                georeactor.CubeGrid.GetBlocks(blocks);
            }
            catch { }

            for (int x = 0; x < blocks.Count; x++)
            {
                try
                {
                    if ((blocks[x].FatBlock.BlockDefinition.SubtypeId == "PHX_GeoTherm_Turbine_Large") &&
                        (blocks[x].BlockDefinition.Enabled))
                    {
                        ct++;
                    }
                }
                catch { }
            }
            */

            List<int> removing = new List<int>();
            foreach (GeoTherm_Turbine turb in turbines)
            {
                if (turb == null)
                {
                    turbines.Remove(turb);
                    continue;
                }

                if (!turb.GeoPlantCheck())
                {
                    try
                    { turb.Unregister(); }
                    catch { }

                    turbines.Remove(turb);
                }
            }
            /*
            for (int x = 0; x < turbines.Count; x++)
            {
                try
                {
                    if (turbines[x] != null)
                    {
                        if (turbines[x].GeoPlantCheck())
                            ct++;
                    }
                    else
                    {
                        removing.Add(x);
                    }
                }
                catch
                {
                    removing.Add(x);
                }
            }
            */
            turbines.TrimExcess();
            ct = turbines.Count;
            if (ct > 0)
            {
                for (int x = 1; x <= ct; x++)
                {
                    amt += inc;
                    inc *= 0.8f;
                }
            }
            // TODO: REMOVE THIS:
/*            georeactor.CustomData =
                "Output Modifier: " + amt.ToString() + Environment.NewLine +
                "Turbines: " + turbines.Count.ToString() + Environment.NewLine;
                */
            georeactor.PowerOutputMultiplier = 1.0f + amt;
            // Cleanup:
            /*
            if (removing.Count > 0)
            {
                foreach (int x in removing)
                {
                    turbines.RemoveAt(x);
                }
            }*/
            UpgradeAmount = amt;
        }
        public bool AddTurbine(GeoTherm_Turbine turbine)
        {
            if (turbines.Count < max)
            {
                foreach (GeoTherm_Turbine tbine in turbines)
                {
                    // Check for already registered:
//                    if (tbine == turbine)
//                        return true;
                }
                turbines.Add(turbine);
                return true;
            }
            return false;
        }
        private void SetSession()
        {
            MyAPIGateway.Session.OnSessionLoading += Session_OnSessionLoading;
            MyAPIGateway.Session.OnSessionReady += Session_OnSessionReady;
        }
        private void Session_OnSessionReady()
        {
        }
        private void Session_OnSessionLoading()
        {
        }
        public override void MarkForClose()
        {
            PreExit();
            base.MarkForClose();
        }
        private bool IsInVoxel(Sandbox.ModAPI.IMyTerminalBlock block)
        {
            BoundingBoxD worldAABB = block.PositionComp.WorldAABB;
            List<Sandbox.Game.Entities.MyVoxelBase> voxelList = new List<Sandbox.Game.Entities.MyVoxelBase>();
            Sandbox.Game.Entities.MyGamePruningStructure.GetAllVoxelMapsInBox(ref worldAABB, voxelList);
            var cubeSize = block.CubeGrid.GridSize;
            BoundingBoxD localAAABB = new BoundingBoxD(cubeSize * ((Vector3D)block.Min - 1), cubeSize * ((Vector3D)block.Max + 1));
            var matrix = block.CubeGrid.WorldMatrix;
            foreach (var map in voxelList)
            {
                if (map.IsAnyAabbCornerInside(ref matrix, localAAABB))
                {
                    return true;
                }
            }

            return false;
        }
        bool IsFunctional
        {
            get
            {
                return georeactor.IsFunctional && georeactor.IsWorking;
            }
        }
        bool IsMoving
        {
            get
            {
                return georeactor.CubeGrid.Physics.IsMoving;
            }
        }
        bool IsStation
        {
            get
            {
                return georeactor.CubeGrid.IsStatic;
            }
        }
        // Used for TURBINES to check GeoThermal plant and deregister if needed
        public bool TurbineCheck()
        {
            return IsFunctional && !IsMoving && IsStation && IsInVoxel(georeactor);
        }
        private void UnregisterAll()
        {
            foreach (GeoTherm_Turbine turb in turbines)
            {
                turb.Unregister();
                turbines.Remove(turb);
            }
        }
        public override void UpdateAfterSimulation()
        {
            //            base.UpdateAfterSimulation();
            bool unreg = false;
            try
            {
                if (MarkedForClose)
                {
                    PreExit();
                    return;
                }

                if (MyAPIGateway.Session == null)
                    return;

                #region Station Check
                if (!IsStation)
                {
                    if (georeactor.Enabled)
                        georeactor.CustomData = "AUTO-OFF";
                    georeactor.ApplyAction("OnOff_Off");
                    georeactor.SetEmissiveParts("EmissiveAtmosphere", ColorStation, INTENSITYON);
                    unreg = true;
                }
                #endregion
                #region Voxel Check
                else if (!IsInVoxel(georeactor))
                {
                    if (georeactor.Enabled)
                        georeactor.CustomData = "AUTO-OFF";
                    georeactor.ApplyAction("OnOff_Off");
                    georeactor.SetEmissiveParts("EmissiveAtmosphere", ColorVoxel, INTENSITYON);
                    unreg = true;
                }
                #endregion
                #region Atmosphere Check
                else if (!inAtmos)
                {
                    if (georeactor.Enabled)
                        georeactor.CustomData = "AUTO-OFF";
                    georeactor.ApplyAction("OnOff_Off");
                    georeactor.SetEmissiveParts("EmissiveAtmosphere", ColorAtmo, INTENSITYON);
                    unreg = true;
                }
                #endregion
                #region No Errors
                else
                {
                    if (georeactor.CustomData.Contains("AUTO-OFF"))
                    {
                        georeactor.ApplyAction("OnOff_On");
                    }
                    georeactor.CustomData = "Turbines: " + turbines.Count.ToString();
                    georeactor.SetEmissiveParts("EmissiveAtmosphere", ColorOn, INTENSITYON);
                }
                #endregion

                if (unreg)
                    UnregisterAll();

                lightson = IsFunctional;

                VRage.Game.ModAPI.Ingame.IMyInventory inv = georeactor.GetInventory(0);
                List<VRage.Game.ModAPI.Ingame.IMyInventoryItem> items = inv.GetItems();
                if (items.Count > 0)
                    empty = false;
                else
                    empty = true;

                if (empty)
                    georeactor.SetEmissiveParts("EmissiveLights", ColorEmpty, INTENSITYON);
                else
                    georeactor.SetEmissiveParts("EmissiveLights", lightson ? ColorOn : ColorOff, INTENSITYON);
                //                georeactor.SetEmissiveParts("EmissiveLights", lightson ? ColorOn : ColorOff, lightson ? INTENSITYON : INTENSITYOFF);
                //            georeactor.RefreshCustomInfo();

                UpdateEffect();
                Upgrades();
            }
            catch { }
        }
        public override void UpdateAfterSimulation10()
        {
            base.UpdateAfterSimulation10();
        }
        public override void UpdateAfterSimulation100()
        {
            if (MyAPIGateway.Session == null)
                return;

            base.UpdateAfterSimulation100();
        }
        public override void UpdateBeforeSimulation()
        {
            try
            {
                density = atmoDet.AtmosphereDetection(this.Entity);

                if (density > 0f)
                    inAtmos = true;
                else
                    inAtmos = false;

                terminalBlock.RefreshCustomInfo();
                base.UpdateBeforeSimulation();
            }
            catch { }
        }
        public override void UpdateBeforeSimulation10()
        {
            base.UpdateBeforeSimulation10();
        }
        public override void UpdateBeforeSimulation100()
        {
            density = atmoDet.AtmosphereDetection(this.Entity);

            if (density > 0f)
                inAtmos = true;
            else
                inAtmos = false;

            terminalBlock.RefreshCustomInfo();
            base.UpdateBeforeSimulation100();
        }
        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
        }
        void UpdateEffect()
        {
            if (!IsFunctional || empty || MarkedForClose)
            {
                Stop();
                return;
            }

            Start();
        }
        void Stop()
        {
            if (effect != null)
            {
                effect.Stop();
                effect = null;

                effect2.Stop();
                effect2 = null;
            }
        }
        void Start()
        {
            MatrixD m = georeactor.WorldMatrix;
            Vector3 f = georeactor.WorldMatrix.Forward;
            Effect(m, f, 2.5f, -3f, 1.2f, -4f);
            Effect2(m, f, 2.5f, -3f, 4f, -4f);
        }
        void Effect(MatrixD worldMatrix, Vector3 forward,
            float scale,
            float offset_down,
            float offset_back,
            float offset_left)
        {
            if (effect == null)
            {
                //                MyParticlesManager.TryCreateParticleEffect("SmokeBlack_GeoGen", out effect, false);
                MyParticlesManager.TryCreateParticleEffect(127808, out effect, false);
            }

            if (effect != null)
            {
                MatrixD world = worldMatrix;
                world.Forward = forward;

                // offset
                if (offset_down > 0f)
                    world.Translation += worldMatrix.Down * offset_down;
                if (offset_down < 0f)
                    world.Translation += worldMatrix.Up * (offset_down * -1);

                if (offset_back > 0f)
                    world.Translation += worldMatrix.Backward * offset_back;
                if (offset_back < 0f)
                    world.Translation += worldMatrix.Forward * (offset_back * -1);

                if (offset_left > 0f)
                    world.Translation += worldMatrix.Left * offset_left;
                if (offset_left < 0f)
                    world.Translation += worldMatrix.Right * (offset_left * -1);

                effect.WorldMatrix = world;
                effect.UserScale = scale;
            }
        }
        void Effect2(MatrixD worldMatrix, Vector3 forward,
            float scale,
            float offset_down,
            float offset_back,
            float offset_left)
        {
            if (effect2 == null)
            {
                //                MyParticlesManager.TryCreateParticleEffect("SmokeBlack_GeoGen", out effect, false);
                MyParticlesManager.TryCreateParticleEffect(127808, out effect2, false);
            }

            if (effect2 != null)
            {
                MatrixD world = worldMatrix;
                world.Forward = forward;

                // offset
                if (offset_down > 0f)
                    world.Translation += worldMatrix.Down * offset_down;
                if (offset_down < 0f)
                    world.Translation += worldMatrix.Up * (offset_down * -1);

                if (offset_back > 0f)
                    world.Translation += worldMatrix.Backward * offset_back;
                if (offset_back < 0f)
                    world.Translation += worldMatrix.Forward * (offset_back * -1);

                if (offset_left > 0f)
                    world.Translation += worldMatrix.Left * offset_left;
                if (offset_left < 0f)
                    world.Translation += worldMatrix.Right * (offset_left * -1);

                effect2.WorldMatrix = world;
                effect2.UserScale = scale;
            }
        }
    }


    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, new string[] { "PHX_GeoTherm_Turbine_Large" })]
    public class GeoTherm_Turbine : MyGameLogicComponent
    {
        Sandbox.ModAPI.IMyBatteryBlock turbine;
        Sandbox.ModAPI.IMyTerminalBlock terminalBlock;
        bool inAtmos = false;
        AtmosphereDetector atmoDet = new AtmosphereDetector();
        private float density = 0.0f;
        bool registered = false;
        String regName = "";
        String regError = "";
        bool status = false;
        bool messaged = false;
        bool destroyed = false;

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Container.Entity.GetObjectBuilder(copy);
        }
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            //            base.Init(objectBuilder);
            Container.Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
            this.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
            turbine = (Sandbox.ModAPI.IMyBatteryBlock)Entity;
            terminalBlock = Entity as Sandbox.ModAPI.IMyTerminalBlock;
        }
        public override void Close()
        {
            base.Close();
        }
        public override void MarkForClose()
        {
            destroyed = true;
            base.MarkForClose();
        }
        private bool IsInVoxel(Sandbox.ModAPI.IMyTerminalBlock block)
        {
            BoundingBoxD worldAABB = block.PositionComp.WorldAABB;
            List<Sandbox.Game.Entities.MyVoxelBase> voxelList = new List<Sandbox.Game.Entities.MyVoxelBase>();
            Sandbox.Game.Entities.MyGamePruningStructure.GetAllVoxelMapsInBox(ref worldAABB, voxelList);
            var cubeSize = block.CubeGrid.GridSize;
            BoundingBoxD localAAABB = new BoundingBoxD(cubeSize * ((Vector3D)block.Min - 1), cubeSize * ((Vector3D)block.Max + 1));
            var matrix = block.CubeGrid.WorldMatrix;
            foreach (var map in voxelList)
            {
                if (map.IsAnyAabbCornerInside(ref matrix, localAAABB))
                {
                    return true;
                }
            }

            return false;
        }
        bool IsFunctional
        {
            get
            {
                return turbine.IsFunctional && turbine.IsWorking && turbine.Enabled;
            }
        }
        bool IsMoving
        {
            get
            {
                return turbine.CubeGrid.Physics.IsMoving;
            }
        }
        bool IsStation
        {
            get
            {
                return turbine.CubeGrid.IsStatic;
            }
        }
        public override void UpdateAfterSimulation()
        {
            //            base.UpdateAfterSimulation();
            if (MyAPIGateway.Session == null)
                return;

            RegisterPlant();

            if (!registered)
            {
                AutoPower(true, "Not Registered to GeoThermal Plant");
                return;
            }

            if (registered)
            {
                if (!IsStation)
                {
                    AutoPower(true, "Grid is SHIP");
                    return;
                }

                /*
                if (!IsFunctional)
                {
                    AutoPower(true, "Turbine not functional");
                    return;
                }*/

                if (!inAtmos)
                {
                    AutoPower(true, "Not in atmosphere");
                    return;
                }

                AutoPower(false);
                return;
            }
            #region OLD ON-OFF CHECKS
            /*
            #region Station Check
            if (!IsStation)
            {
                if (turbine.Enabled)
                    turbine.CustomData = "AUTO-OFF";
                turbine.ApplyAction("OnOff_Off");
                status = false;
                return;
            }
            #endregion
            #region Voxel Check
            else if (!IsInVoxel(turbine))
            {
                if (turbine.Enabled)
                    turbine.CustomData = "AUTO-OFF";
                turbine.ApplyAction("OnOff_Off");
                status = false;
                return;
            }
            #endregion
            #region Atmosphere Check
            else if (!inAtmos)
            {
                if (turbine.Enabled)
                    turbine.CustomData = "AUTO-OFF";
                turbine.ApplyAction("OnOff_Off");
                status = false;
                return;
            }
            #endregion
            #region Functional Check
            else if (!IsFunctional)
            {
                if (turbine.Enabled)
                    turbine.CustomData = "AUTO-OFF";
                turbine.ApplyAction("OnOff_Off");
                status = false;
                return;
            }
            #endregion
            #region No Errors
            else
            {
                if (turbine.CustomData == "AUTO-OFF")
                {
                    turbine.ApplyAction("OnOff_On");
                    turbine.CustomData = "";
                }
                status = true;
                RegisterPlant();
            }
            #endregion
            */
            #endregion

        }
        public override void UpdateAfterSimulation10()
        {
            base.UpdateAfterSimulation10();
        }
        public override void UpdateAfterSimulation100()
        {
            if (MyAPIGateway.Session == null)
                return;

            base.UpdateAfterSimulation100();
        }
        public override void UpdateBeforeSimulation()
        {
            density = atmoDet.AtmosphereDetection(this.Entity);

            if (density > 0f)
                inAtmos = true;
            else
                inAtmos = false;

            terminalBlock.RefreshCustomInfo();
            base.UpdateBeforeSimulation();
        }
        public override void UpdateBeforeSimulation10()
        {
            base.UpdateBeforeSimulation10();
        }
        public override void UpdateBeforeSimulation100()
        {
            density = atmoDet.AtmosphereDetection(this.Entity);

            if (density > 0f)
                inAtmos = true;
            else
                inAtmos = false;

            terminalBlock.RefreshCustomInfo();
            base.UpdateBeforeSimulation100();
        }
        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
        }
        public void Unregister()
        {
            registered = false;
            regName = "";
        }
        public bool GeoPlantCheck()
        {
            return IsFunctional && !IsMoving && IsStation;
        }
        private void AutoPower(bool off, String reason = "")
        {
            String rsn = "AUTO-OFF";
            if (reason.Length > 0)
                rsn += ":" + Environment.NewLine + reason;

            if (off)
            {
                turbine.ApplyAction("OnOff_Off");
                turbine.CustomData = rsn;
            }
            else
            {
                if (turbine.CustomData.Contains("AUTO-OFF"))
                {
                    turbine.ApplyAction("OnOff_On");
                    turbine.CustomData = "";
                }
            }
        }

        private void RegisterPlant()
        {
            if (registered)
            {
//                turbine.CustomData = Environment.NewLine + "Registered to: " + regName;
                return;
            }

            List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
            turbine.CubeGrid.GetBlocks(blocks);
            int ct = 0;

            foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
            {
                ct++;
                try
                {
                    if (block.FatBlock.BlockDefinition.SubtypeId == "PHX_GeoTherm_React_Large")
                    {
/*                        msg += "Block #: " + ct.ToString() + Environment.NewLine +
                            "FatName: " + block.FatBlock.Name + Environment.NewLine +
                            "Type: " + block.FatBlock.GetType().ToString() + Environment.NewLine +
                            "GeoType: " + block.FatBlock.BlockDefinition.SubtypeId + Environment.NewLine;
                        //                            "Builder: " + block.FatBlock.GetObjectBuilder().Name + Environment.NewLine +
                        //                            "Builder2: " + block.FatBlock.GetObjectBuilderCubeBlock().Name + Environment.NewLine;
*/
                        regName = block.FatBlock.DisplayName;

                        registered = ((GeoTherm_Gen)block.FatBlock.GameLogic).AddTurbine(this);
                        if (registered)
                        {
                            try
                            {
                                turbine.CustomData = "Registered to:" + Environment.NewLine +
                                    ((GeoTherm_Gen)block.FatBlock.GameLogic).Entity.Name;
                            }
                            catch { }
                        }
                        break;
                    }
                }
                catch
                {
//                    msg += "Outter exception" + Environment.NewLine;
                }
            }

            while (blocks.Count > 0)
                blocks.RemoveAt(0);
            blocks = null;
//            turbine.CustomData = "Registration failed, didnt find GeoThermal Plant";
        }
    }
}
