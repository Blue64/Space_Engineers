using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using VRage.Game.ModAPI;
using Sandbox.Game.EntityComponents;
using Sandbox.Common.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using System.Text;
using VRage.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;

namespace Kage.HackingComputer
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), new string[] { "LargeHackingBlock", "SmallHackingBlock" })]
    public class HackingBlock : MyGameLogicComponent
    {
        IMyFunctionalBlock m_hackingblock;
        static Random m_random = new Random();

        public enum States
        {
            Off,
            NoEnemies,
            Hacking,
            Success
        };

        public States CurrentState = States.Off;
        public long TargetId = 0L;
        public int Chance = 0;

        private MyEntity3DSoundEmitter m_soundEmitter = null;
        private MySoundPair m_soundPair = MySoundPair.Empty;

        public const string Emissive = "Em_Hacking";
        private MyResourceSinkComponent m_sink = null;

        private int m_countdown = 5;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;

            if (!(Entity is IMyFunctionalBlock))
                LogManager.WriteLine("WARNING: Hacking Computer Is Not An IMyFunctionalBlock!");

            m_hackingblock = Entity as IMyFunctionalBlock;

            m_sink = new MyResourceSinkComponent(1);
            m_sink.Init(MyStringHash.GetOrCompute("Utility"), 0.05f, delegate {
                if (!UsePower())
                {
                    return 0f;
                }
                return m_sink.MaxRequiredInput;
            });
            if (m_hackingblock.Components.Contains(typeof(MyResourceSinkComponent)))
            {
                LogManager.WriteLine("WARNING: HackingComputer Already Has A Resource Sink");
                m_hackingblock.Components.Remove<MyResourceSinkComponent>();
            }
            m_hackingblock.Components.Add(m_sink);

            m_hackingblock.AppendingCustomInfo += updateInfo;

            // Setup Audio
            m_soundEmitter = new MyEntity3DSoundEmitter((MyEntity)Entity);
            m_soundPair = new MySoundPair("ArcBlockTimerSignalB");
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return null;
        }

        public override void Close()
        {
            m_hackingblock.AppendingCustomInfo -= updateInfo;
            if (m_soundEmitter != null)
                m_soundEmitter.StopSound(true);
            base.Close();
        }

        public bool UsePower()
        {
            return m_hackingblock.Enabled && m_hackingblock.IsFunctional;
        }

        public bool IsWorking()
        {
            return m_sink.IsPowered && UsePower();
        }

        public override void UpdateAfterSimulation10()
        {
            m_countdown -= 1;
            if (m_countdown < 0 && (CurrentState == States.Hacking || CurrentState == States.Success))
            {
                m_hackingblock.SetEmissiveParts(Emissive, Color.White, 1f);
            }
        }

        public override void UpdateAfterSimulation100()
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            try
            {
                m_sink.Update();
                if (!IsWorking() || m_hackingblock.OwnerId == 0)
                {
                    CurrentState = States.Off;
                }
                else
                {
                    List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                    (m_hackingblock.CubeGrid as IMyCubeGrid).GetBlocks(blocks, isValidHackingTarget);
                    if (blocks.Count > 0)
                    {
                        int targetIndex = m_random.Next(blocks.Count);

                        for (int i = 0; i < blocks.Count; i++)
                        {
                            if (blocks[i].GetObjectBuilder().GetId().SubtypeName == "LargeFirewallBlock")
                            {
                                FirewallBlock firewall = blocks[i].FatBlock.GameLogic.GetAs<FirewallBlock>();
                                if (firewall == null)
                                    LogManager.WriteLine("Firewall Has No Firewall Component");
                                else if (firewall.IsWorking())
                                    targetIndex = i;
                            }
                        }

                        IMySlimBlock block = blocks[targetIndex];

                        Chance = getComputerCount(block);
                        TargetId = block.FatBlock.EntityId;

                        if (m_random.Next() % Chance == 0)
                        {
                            (block.FatBlock as MyCubeBlock).ChangeOwner(0, MyOwnershipShareModeEnum.Faction);
                            (block.FatBlock as MyCubeBlock).ChangeBlockOwnerRequest(m_hackingblock.OwnerId, MyOwnershipShareModeEnum.Faction);

                            // Success
                            CurrentState = States.Success;
                        }
                        else
                        {
                            // Failure
                            CurrentState = States.Hacking;
                        }
                    }
                    else
                    {
                        // No Enemies
                        TargetId = 0L;
                        CurrentState = States.NoEnemies;
                    }
                }
            }
            catch (Exception e)
            {
                LogManager.WriteException(e);
            }

            HackingComputerSync.SendHackingBlockStates(this);
        }

        private bool isValidHackingTarget(IMySlimBlock block)
        {
            if (block == null || block.FatBlock == null)
                return false;

            if (!block.FatBlock.IsWorking || !block.FatBlock.IsFunctional)
                return false;

            if (block.FatBlock.GetUserRelationToOwner(m_hackingblock.OwnerId) == MyRelationsBetweenPlayerAndBlock.Enemies)
            {
                return true;
            }
            return false;
        }

        public void UpdateClient()
        {
            try
            {
                m_countdown = 4;
                Color emissiveColor;

                switch (CurrentState)
                {
                    case States.Success:
                        emissiveColor = Color.Green;
                        if (m_soundEmitter != null)
                            m_soundEmitter.PlaySound(m_soundPair);
                        break;
                    case States.Hacking:
                        emissiveColor = Color.OrangeRed;
                        break;
                    case States.NoEnemies:
                        emissiveColor = Color.Cyan;
                        break;
                    case States.Off:
                    default:
                        emissiveColor = Color.Red;
                        break;
                }

                m_hackingblock.SetEmissiveParts(Emissive, emissiveColor, 1f);
                m_hackingblock.RefreshCustomInfo();
            }
            catch (Exception e)
            {
                LogManager.WriteException(e);
            }
        }

        private void updateInfo(IMyTerminalBlock arg1, StringBuilder arg2)
        {
            try
            {
                if (m_hackingblock.OwnerId == 0L)
                {
                    arg2.Append("!!WARNING!!\n\nBlock Must Be Owned");
                    return;
                }

                if (CurrentState == States.NoEnemies)
                {
                    arg2.Append("No Enemy Blocks Found");
                }
                if (CurrentState == States.Hacking || CurrentState == States.Success)
                {
                    string targetName = "<<Unknown>>";
                    IMyEntity target;

                    if (MyAPIGateway.Entities.TryGetEntityById(TargetId, out target))
                    {
                        if (target is IMyTerminalBlock)
                        {
                            targetName = (target as IMyTerminalBlock).CustomName;
                        }
                    }

                    arg2.Append("Attempting to hack ");
                    arg2.Append(targetName);
                    arg2.Append("\n\nChance of success is 1 in ");
                    arg2.Append(Chance);
                    arg2.Append("...\n\n");

                    if (CurrentState == States.Hacking)
                        arg2.Append("Failed");
                    else if (CurrentState == States.Success)
                        arg2.Append("Success");
               }
            }
            catch(Exception e)
            {
                LogManager.WriteException(e);
            }
        }

        private int getComputerCount(IMySlimBlock block)
        {
            var componets = MyDefinitionManager.Static.GetCubeBlockDefinition(block.GetObjectBuilder()).Components;
            int computers = 0;
            for (var i = 0; i < componets.Length; i++)
            {
                if (componets[i].Definition.Id.SubtypeName == "Computer")
                    computers += componets[i].Count;
            }
            return computers;
        }
    }
}
