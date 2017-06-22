using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using AnimaScript;
using AnimaData;
using VRage.Game.Entity;

namespace /*(Your personal namespace)*/ Sensor.SC_Radar_Draygo
{
    using BlockModAPIType = /*( Mod API Block Type )*/ IMySensorBlock;
	[MyEntityComponentDescriptor(typeof(/*( Object Builder Type )*/ MyObjectBuilder_SensorBlock), /*( Block name to link with gamelogic )*/ "SC_Radar_DS_RADAR", "SC_Small_Radar_DS_RADAR")]
    public class /*( Name of the gamelogic class )*/ Block_Logic : MyGameLogicComponent
    {
        // Main anima and parts
        private Anima m_anima;
        private AnimaPart m_part_1;
        private AnimaPart m_part_2;
        private AnimaPart m_part_3;

        // This holds the last status so we can discover when status toggle
        public bool lastStatus = false;

        // This is a helper to know the sequence playing without comparing strings
        enum BlockMode
        {
            POWER_ON, POWER_OFF, ACTIVE, INACTIVE,
        };
        private BlockMode blockMode = BlockMode.POWER_ON;

        // Your block initialization
        public void BlockInit()
        {
            // No point to run this script if is a dedicated server because there's no graphics
            if (Anima.DedicatedServer) return;

            // Create the main Anima class
            m_anima = new Anima();

            // Initialize Anima
            if (!m_anima.Init(Entity as MyEntity, "Holographic Radar", "Holo")) throw new ArgumentException("Anima failed to initialize!");

            // Add parts
            m_part_1 = m_anima.AddPart(null, @"Radar\Radar_Part1");
            m_part_2 = m_anima.AddPart(m_part_1, @"Radar\Radar_Part2");
            m_part_3 = m_anima.AddPart(m_part_2, @"Radar\Radar_Part3");

            // Assign sequences
            coreFunctionality(m_part_1);
            m_part_1.OnComplete = coreFunctionality;

            // Play sequences
            m_part_1.Play(Anima.Playback.LOOP);
            m_part_2.Play(Anima.Playback.LOOP);
            m_part_3.Play(Anima.Playback.LOOP);

            // Update each frame, note this may not work for all object's types!
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        // Your block update (each frame after simulation ... if works ...)
        public void BlockUpdate()
        {
			if (m_anima == null) return;
            // Enable Anima based of player distance and if object is functional
            // It will only enable if it's under 500m AND block is functional
            m_anima.Enable = m_anima.TestPlayerDistance(500.0) && block.IsFunctional;

            // Only update if is enabled!
            if (m_anima.Enable)
            {
                m_anima.Update(m_anima.GetElapsed());

                // This is only for animating the "Emissive" material!
                float corePower = 0.0f;
                switch (blockMode)
                {
                    case BlockMode.POWER_ON:
                        corePower = m_part_3.CursorNormal;
                        break;
                    case BlockMode.POWER_OFF:
                        corePower = 1.0f - m_part_3.CursorNormal;
                        break;
                    case BlockMode.ACTIVE:
                        corePower = 1.0f;
                        break;
                }
                m_part_3.SetEmissive(corePower, Color.Lerp(Color.DarkRed, Color.Green, corePower));
            }
        }

        // Our callback to change sequences
        public void coreFunctionality(AnimaPart part)
        {
            bool status = block.IsWorking;
            if (!lastStatus && status)
            {
                // Powering on
                m_part_1.Sequence = Seq_SC_Radar_Part1_powerOn.Adquire();
                m_part_2.Sequence = Seq_SC_Radar_Part2_powerOn.Adquire();
                m_part_3.Sequence = Seq_SC_Radar_Part3_powerOn.Adquire();
                blockMode = BlockMode.POWER_ON;
            }
            else if (lastStatus && !status)
            {
                // Powering off
                m_part_1.Sequence = Seq_SC_Radar_Part1_powerOff.Adquire();
                m_part_2.Sequence = Seq_SC_Radar_Part2_powerOff.Adquire();
                m_part_3.Sequence = Seq_SC_Radar_Part3_powerOff.Adquire();
                blockMode = BlockMode.POWER_OFF;
            }
            else if (status)
            {
                // While active
                m_part_1.Sequence = Seq_SC_Radar_Part1_active.Adquire();
                m_part_2.Sequence = Seq_SC_Radar_Part2_active.Adquire();
                m_part_3.Sequence = Seq_SC_Radar_Part3_active.Adquire();
                blockMode = BlockMode.ACTIVE;
            }
            else
            {
                // While inactive
                m_part_1.Sequence = Seq_SC_Radar_Part1_inactive.Adquire();
                m_part_2.Sequence = Seq_SC_Radar_Part2_inactive.Adquire();
                m_part_3.Sequence = Seq_SC_Radar_Part3_inactive.Adquire();
                blockMode = BlockMode.INACTIVE;
            }
            lastStatus = status;
        }

        // There's no reason to change code below unless you know what you're doing!

        private BlockModAPIType block;
        private bool active = false;

        // Gamelogic initialization
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            // Update each frame, note this may not work for all object's types!
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

            block = Entity as BlockModAPIType;
            if (block == null || MyAPIGateway.Session == null) return;

            BlockInit();

            active = true;
        }

        // Gamelogic update (each frame after simulation)
        public override void UpdateAfterSimulation()
        {
            if (!active) Init(null);
            if (!active || block == null || block.MarkedForClose || block.Closed) return;
            BlockUpdate();
        }

        // Gamelogic close when the block gets deleted
        public override void Close()
        {
            block = null;
        }

        // Gamelogic object builder, leave it alone ;)
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
    }
}