using System;
using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRageMath;

using Draygo.API;

using Jawastew;
namespace Jawastew.HudLcd
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), false)]
    sealed class HudLcd : MyGameLogicComponent
    {
        //Workshop & Debug
        const long WORKSHOP_ID = 911144486;
        const string MOD = "HudLcd";
        static bool logging = Logger.logging(false);

        //Static
        public static bool IsInit => textAPI != null;
        public static bool IsAPIAlive => textAPI != null && textAPI.Heartbeat;
        static bool isServer = false;

        static HUDTextAPI textAPI = null;

        //config
        const int ttl = 10;
        const string matchstring = "hudlcd";
        const char delim = ':';
        const double textPosX = -0.98;
        const double textPosY = -0.2;
        const double textScale = 0.8;

        // Textpanel
        IMyTextPanel thisLcd = null;
        HUDTextAPI.HUDMessage thisHUDMessage;
        Vector2D thistextPosition = new Vector2D(-0.98, -0.2);
        double thistextScale = 0.8;
        string thisconfigcolour = Color.White.ToString();

        IMyTerminalBlock ControlledEntity => MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity as IMyTerminalBlock;
        bool isControlled => ControlledEntity != null && ControlledEntity.CubeGrid == thisLcd.CubeGrid;
        bool hasHudLcd = false;



        //Initialises textAPI once for clients, checks if is Server
        static void Initialize()
        {
            if (IsInit) return;
            if ((MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated))
            {
                isServer = true;
                return;
            }
            textAPI = new HUDTextAPI(WORKSHOP_ID);
        }


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            thisLcd = Entity as IMyTextPanel;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (!IsInit) Initialize();
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME; //If you change the update interval, also change the int ttl accordingly for the HUDMessage
        }

        //for Performance every 10th frame
        public override void UpdateBeforeSimulation10()
        {
            if (isServer) return;
            if (!IsAPIAlive) return;
            UpdateValues();
            if (isControlled && hasHudLcd)
                UpdateLCD();
        }

        private void UpdateValues()
        {
            hasHudLcd = false;
            if (thisLcd.GetPublicTitle().ToLower().Contains(matchstring))
            {
                hasHudLcd = true;
                ParseAndUpdateConfig(thisLcd.GetPublicTitle());
            }
            else if (thisLcd.CustomData.ToLower().Contains(matchstring)) {
                hasHudLcd = true;
                ParseAndUpdateConfig(thisLcd.CustomData);
            }
        }

        private void ParseAndUpdateConfig(string data)
        {
            String[] lines = data.Split('\n');
            foreach (String line in lines)
            {
                if (line.ToLower().Contains(matchstring))
                {
                    String[] rawconf = line.Split(delim);
                    for (int i = 0; i < 5; i++)
                    {
                        if (rawconf.Length > i) // Set values from Config Line
                        {
                            switch (i)
                            {
                                case 0:
                                    break;
                                case 1:
                                    thistextPosition.X = trygetdouble(rawconf[i], textPosX);
                                    break;
                                case 2:
                                    thistextPosition.Y = trygetdouble(rawconf[i], textPosY);
                                    break;
                                case 3:
                                    thistextScale = trygetdouble(rawconf[i], textScale);
                                    break;
                                case 4:
                                    thisconfigcolour = "<color=" + rawconf[i].Trim() + ">";
                                    break;
                                default:
                                    break;
                            }
                        }
                        else // Set Default Values
                        {
                            switch (i)
                            {
                                case 0:
                                    break;
                                case 1:
                                    thistextPosition.X = textPosX;
                                    break;
                                case 2:
                                    thistextPosition.Y = textPosY;
                                    break;
                                case 3:
                                    thistextScale = textScale;
                                    break;
                                case 4:
                                    var fontColor = thisLcd.GetValueColor("FontColor");
                                    thisconfigcolour = $"<color={fontColor.R},{fontColor.G},{fontColor.B}>";
                                    break;
                                default:
                                    break;
                            }
                        }

                    }
                    break;
                }

            }
            
        }

        private double trygetdouble(string v, double defaultval)
        {
            try
            {
                return double.Parse(v);
            }
            catch (Exception)
            {
                return defaultval;
            }
        }

        private void UpdateLCD()
        {
            thisHUDMessage = new HUDTextAPI.HUDMessage(thisLcd.EntityId,
                                                        ttl,
                                                        thistextPosition,
                                                        thistextScale,
                                                        true, false, Color.Black,
                                                        thisconfigcolour + thisLcd.GetPublicText()
                                                        );
            textAPI.Send(thisHUDMessage);
        }

        

        //public override void Close ()
        //{
        //}

    }
}
