using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Kage.HackingComputer
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), new string[] { "LargeFirewallBlock" })]
    public class FirewallBlock : MyGameLogicComponent
    {
        private IMyFunctionalBlock m_firewall = null;

        private MyEntity3DSoundEmitter m_soundEmitter = null;
        private MySoundPair m_soundPair = MySoundPair.Empty;

        public const string Emissive1 = "EM_Firewall";
        public const string Emissive2 = "EM_Firewall2";
        private MyResourceSinkComponent m_sink = null;

        public int BlockedAttempts = 0;
        private int blockIndicatorCountdown = 0;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            m_firewall = Entity as IMyFunctionalBlock;
            m_firewall.SetEmissiveParts(Emissive1, Color.Red, 1f);
            m_firewall.SetEmissiveParts(Emissive2, Color.Red, 1f);

            m_sink = new MyResourceSinkComponent(1);
            m_sink.Init(MyStringHash.GetOrCompute("Utility"), 0.05f, delegate {
                if (!UsePower())
                {
                    return 0f;
                }
                return m_sink.MaxRequiredInput;
            });
            if (m_firewall.Components.Contains(typeof(MyResourceSinkComponent)))
            {
                LogManager.WriteLine("WARNING: Firewall Already Has A Resource Sink");
                m_firewall.Components.Remove<MyResourceSinkComponent>();
            }
            m_firewall.Components.Add(m_sink);
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return null;
        }

        public bool UsePower()
        {
            return m_firewall.Enabled && m_firewall.IsFunctional;
        }

        public bool IsWorking()
        {
            return m_sink.IsPowered && UsePower();
        }

        public override void UpdateBeforeSimulation10()
        {
            m_sink.Update();
            if (IsWorking())
            {
                m_firewall.SetEmissiveParts(Emissive1, Color.Green, 1f);
                if (blockIndicatorCountdown > 0)
                {
                    blockIndicatorCountdown--;
                    m_firewall.SetEmissiveParts(Emissive2, Color.Cyan, 1f);
                }
                else
                {
                    if (blockIndicatorCountdown > 0)
                        blockIndicatorCountdown--;

                    if (BlockedAttempts > 0 && blockIndicatorCountdown <= 0)
                    {
                        m_firewall.SetEmissiveParts(Emissive2, Color.OrangeRed, 1f);
                        if (BlockedAttempts > 5)
                            BlockedAttempts = 5;
                        blockIndicatorCountdown = Math.Max(1, (int)Math.Ceiling(10f / (2f * BlockedAttempts)));
                        BlockedAttempts--;
                    }
                    else
                    {
                        m_firewall.SetEmissiveParts(Emissive2, Color.Cyan, 1f);
                    }
                }
            }
            else
            {
                m_firewall.SetEmissiveParts(Emissive1, Color.Red, 1f);
                m_firewall.SetEmissiveParts(Emissive2, Color.Red, 1f);
            }
        }
    }
}