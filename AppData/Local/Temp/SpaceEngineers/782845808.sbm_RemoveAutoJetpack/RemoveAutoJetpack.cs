using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Lights;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRageMath;
using VRage;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using Sandbox.ModAPI.Interfaces; // needed for TerminalPropertyExtensions

using IMyControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;
using Sandbox.Game.Entities.Character.Components;
using VRage.Library.Utils;

namespace vorgonian.DisableAllAutomaticJetpackActivation
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class AllJetpackActivationRemoved : MySessionComponentBase
    {
        public static int activationskippingdelay = 20;
        private short tick;
        public static int skipNextActivation = 0;
        public static bool handlerAdded = false;
        public bool init { get; private set; }
        public static IMyCharacter characterEntity = null;
        bool isDedicatedHost;

        public void Init()
        {
            isDedicatedHost = (MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer);
            init = true;
        }
        protected override void UnloadData()
        {
            try
            {
                if (handlerAdded)
                {
                    characterEntity.MovementStateChanged -= characterEntity_MovementStateChanged;
                    handlerAdded = false;
                }
            }
            catch (Exception e)
            {
                //Log.Error(e); //yes, i am lazy - i should log. i really should
            }
        }

        private void characterEntity_MovementStateChanged(IMyCharacter currentcharacter, MyCharacterMovementEnum oldState, MyCharacterMovementEnum newState)
        {
            if (skipNextActivation > 0) skipNextActivation--;
            if (oldState == MyCharacterMovementEnum.Sitting)
            {
            }
            if (newState == MyCharacterMovementEnum.Died)
            {
                characterEntity.MovementStateChanged -= characterEntity_MovementStateChanged;
                handlerAdded = false;
                skipNextActivation = 0;
            }

            if ((oldState == MyCharacterMovementEnum.Flying) || (newState == MyCharacterMovementEnum.Flying))
            {
                MyEntityComponentContainer playercontainer = characterEntity.Components;
                MyCharacterJetpackComponent JetpackComp = playercontainer.Get<MyCharacterJetpackComponent>();
                if (JetpackComp != null)
                {
                    if ((oldState == MyCharacterMovementEnum.Flying) && (newState == MyCharacterMovementEnum.Sitting))
                    {
                        skipNextActivation = activationskippingdelay;
                    }
                    else
                    {
                        if (!MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.THRUSTS))
                        {
                            if (JetpackComp.TurnedOn)
                            {
                                if (skipNextActivation>0)
                                {
                                    skipNextActivation = 0;
                                }
                                else
                                {
                                    JetpackComp.TurnOnJetpack(false); //turn the jetpack off
                                }
                            }
                        }
                    }
                }
            }
          //  if ((newState != MyCharacterMovementEnum.Sitting) || (oldState != MyCharacterMovementEnum.Sitting)) skipNextActivation = false;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                tick++;
                if (MyAPIGateway.Session == null) return;
                if (!init) //reinit just to be save
                    Init();
                if (isDedicatedHost)
                    return; //there is no jetpack or even a player on the dedicated host itself
                if (tick % 3 == 0) //better performance if the code is executed less - only do it every 3 ticks will work until you play below 4 FPS
                {
                    if ((skipNextActivation > 0) && (skipNextActivation < activationskippingdelay)) skipNextActivation--;
                    var camera = MyAPIGateway.Session.CameraController;
                    if (camera == null) return;
                    if (characterEntity != null && (characterEntity.MarkedForClose || characterEntity.Closed))
                    {
                        characterEntity = null; //remove the stored character
                    }
                    if (camera is IMyCharacter)
                    {
                        if (camera as IMyCharacter != characterEntity)
                        {
                            if (characterEntity != null) characterEntity.MovementStateChanged -= characterEntity_MovementStateChanged;
                            handlerAdded = false;
                        }
                        characterEntity = ((IMyCharacter)camera);
                        MyEntityComponentContainer playercontainer = ((IMyCharacter)camera).Components;
                        MyCharacterJetpackComponent JetpackComp = playercontainer.Get<MyCharacterJetpackComponent>();
                        if (JetpackComp != null)
                        {
                            if (JetpackComp.CurrentAutoEnableDelay > 0)  //The coundown is running so deactivate it
                                JetpackComp.CurrentAutoEnableDelay = -1; //now it is deactivated again
                        }
                    }
                }
                if (!handlerAdded) //only once
                {
                    var camera = MyAPIGateway.Session.CameraController;
                    if (camera == null) return;
                    if (camera is IMyCharacter)
                    {
                        characterEntity = ((IMyCharacter)camera); //store the character Entity
                        characterEntity.MovementStateChanged += characterEntity_MovementStateChanged; //add the handler
                        handlerAdded = true;
                        skipNextActivation = activationskippingdelay;
                    }
                }
            }
            catch (Exception e)
            {
                //Lazy lazy... still no logging... 
            }
        }
    }
}
