
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;

using VRageMath;

using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using VRage.Game;

namespace Eikester.CryoChamber
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CryoChamber), true,
        new string[] 
		{ 	
			"Eikester_CryoChamber"
		}
    )]
    public class CryoChamber : MyGameLogicComponent
    {
        IMyCryoChamber m_block;
		MyParticleEffect effect = null;

        private Color COLORON = new Color(0.08f, 0.44f, 0.44f);
        private Color COLOROFF = new Color(0.0f, 0.0f, 0.0f);
        
        private float INTENSITYON = 1f;
        private float INTENSITYOFF = 0f;

        bool lightson = true;

        bool IsMoving
        {
            get
            {
                return m_block.CubeGrid.Physics.IsMoving;
            }
        }

        bool IsFunctional
        {
            get
            {
                return m_block.IsFunctional && m_block.IsWorking;
            }
        }

        bool IsPEVisible
        {
            get
            {
                try
                {
                    double distance;
                    Vector3D pPos = MyAPIGateway.Session.Player.GetPosition();
                    Vector3D bPos = m_block.WorldMatrix.Translation;
                    Vector3D.Distance(ref pPos, ref bPos, out distance);

                    return distance < 100;
                }
                catch
                {
                    return true;
                }
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Container.Entity.GetObjectBuilder(copy);
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;

            m_block = (IMyCryoChamber)Entity;
        }

        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            UpdateEmissivity();
            UpdateEffect();
        }

        void UpdateEffect()
        {
            if (!IsPEVisible || IsMoving || !IsFunctional)
            {
                Stop();
                return;
            }

            Start();
        }

        void UpdateEmissivity()
        {
            lightson = !IsMoving && IsFunctional && IsPEVisible;

            m_block.SetEmissiveParts("EmissiveLights", lightson ? COLORON : COLOROFF, lightson ? INTENSITYON : INTENSITYOFF);
        }

        public override void Close()
        {
			Stop();
        }
		
		void Stop()
		{
            if (effect != null)
            {
                effect.Stop();
                effect = null;
            }
		}
		
		void Start()
		{
            MatrixD m = m_block.WorldMatrix;
            Vector3 f = m_block.WorldMatrix.Forward;
            Effect(m, f, 1f, 1.1f, 0.2f);
		}

        void Effect(MatrixD worldMatrix, Vector3 forward,
            float scale,
            float offset_down,
            float offset_back)
        {
            if (effect == null)
            {
                MyParticlesManager.TryCreateParticleEffect("Bubbles", out effect, false);
            }

            if(effect != null)
            {
                MatrixD world = worldMatrix;
                world.Forward = forward;

                // offset
                if (offset_down > 0f)
                    world.Translation += worldMatrix.Down * offset_down;

                if (offset_back > 0f)
                    world.Translation += worldMatrix.Backward * offset_back;

                effect.WorldMatrix = world;
                effect.UserScale = scale;
            }
        }
    }
}