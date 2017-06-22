namespace IndustrialAutomaton.ShrapnelDamage
{
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Common.ObjectBuilders.Definitions;
    using Sandbox.Definitions;
    using Sandbox.Game.Entities;
    using Sandbox.Game.ParticleEffects;
    using Sandbox.ModAPI;
    using Sandbox.Game;
    using System;
    using System.Collections.Generic;
    using VRage;
    using VRage.Game;
    using VRage.Game.ModAPI;
    using VRage.ModAPI;
    using VRage.ObjectBuilders;
    using VRageMath;
    
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class IndustrialAutomaton : MySessionComponentBase
    {

        public static bool _isInit;
        
        // Initialisation

        public override void UpdateBeforeSimulation()
        {
            if (_isInit) return;
            if (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated) Init();
            else if (MyAPIGateway.Session.Player!=null) Init();
        }

        public static void Init()
        {
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, checkDamage);
            _isInit=true;
        }

        // Mod actions
    
        public static void checkDamage(object block, ref MyDamageInformation info)
        {
            if (info.Type!=MyDamageType.Bullet && info.Type!=MyDamageType.Rocket) return;
            var slimBlock = block as IMySlimBlock;				if (slimBlock==null) return;
            var dmgBlock = slimBlock as IMyDestroyableObject;   if (dmgBlock==null || dmgBlock.Integrity>info.Amount) return;       
            var grid = slimBlock.CubeGrid as IMyCubeGrid;       if (grid==null) return;
            var deltaDamage = info.Amount-dmgBlock.Integrity;
            info.Amount=dmgBlock.Integrity;
            Vector3D pos = grid.GridIntegerToWorld(slimBlock.Position);
            BoundingSphereD sphere = new BoundingSphereD(pos, deltaDamage/500f);
            MyExplosionInfo bomb = new MyExplosionInfo(deltaDamage*0.8f, deltaDamage*0.8f, sphere, MyExplosionTypeEnum.BOMB_EXPLOSION, false, true);
            bomb.CreateParticleEffect = true;
            MyExplosions.AddExplosion(ref bomb, true);
        }
       
    }
}