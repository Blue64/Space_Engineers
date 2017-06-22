using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using System.IO;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace SGCam_HelmetAT
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]  //defines update location (before, after, never)
    public class RUN : MySessionComponentBase  //this is where everything starts
    {
        private int runcount;
        private bool initialized;
        private bool processing;
        private bool autotoggleON;
        public override void UpdateBeforeSimulation()  //this is where things actually start each update (60 times per second)
        {
            try
            {
                if (MyAPIGateway.Session == null) //if game is not running, dont run
                {
                    return;
                }
                if (!initialized)  //dont run if disabled by user
                {
                    Initialize();
                    return;
                }
                if (!autotoggleON)  //dont run if disabled by user
                {
                    return;
                }
                if (++runcount % (5) != 0)    //runs every 5th frame
                {
                    return;
                }
                LOG.Log.WriteLine("starting run");
                if (processing)  //dont double run
                {
                    LOG.Log.WriteLine("still processing?");
                    return;
                }
                MyAPIGateway.Parallel.Start(() =>
                {
                    try
                    {
                        LOG.Log.WriteLine("trying parallel");
                        processing = true;
                        runHauto();
                        processing = false;
                        LOG.Log.WriteLine("closing parallel");
                    }
                    catch (Exception ex)  //thrown if parallel string fails (ie complicated part of mod)
                    {
                        processing = false;
                        LOG.Log.WriteLine("exception in parallel: " + ex.ToString());
                    }
                });
            }
            catch (Exception ex)  //thrown if entire update for mod fails
            {
                LOG.Log.WriteLine("exception in main: " + ex.ToString());
            }
        }

        private void Initialize()
        {
            MyAPIGateway.Utilities.MessageEntered += HandleMessageEntered;
            autotoggleON = true;  //default on/off setting for mod
            initialized = true;
            LOG.Log.WriteLine("initialized");
            return;
        }
        private void runHauto()     //core function of mod
        {
            LOG.Log.WriteLine("running main function");
            bool helmetEnabled = MyAPIGateway.Session.Player.Character.EnabledHelmet;
            float oxyLevel = MyAPIGateway.Session.Player.Character.EnvironmentOxygenLevel;  //float from 0.0 to 1.0 *potentially out of range?
            LOG.Log.WriteLine("oxyLevel: " + oxyLevel.ToString());
            if (helmetEnabled)  //if the helmet is on
            {
                LOG.Log.WriteLine("helmet on");
                if (oxyLevel > 0.55)    //if oxygen, take helmet off
                {
                    LOG.Log.WriteLine("oxygen high, removing helmet");
                    MyAPIGateway.Session.Player.Character.SwitchHelmet();
                    return;
                }
                else if (oxyLevel < 0.56 && oxyLevel > -0.01)  //if no oxygen, keep helmet on
                {
                    LOG.Log.WriteLine("oxygen low, keeping helmet on");
                    return;
                }
                else
                {
                    LOG.Log.WriteLine("helmet on, oxyLevel out of range " + oxyLevel.ToString());
                    return;
                }
            }
            if (!helmetEnabled)  //if the helmet is off
            {
                LOG.Log.WriteLine("helmet off");
                if (oxyLevel > 0.55)  //if oxygen keep helmet off
                {
                    LOG.Log.WriteLine("oxygen high, keeping helmet off");
                    return;
                }
                else if (oxyLevel < 0.56 && oxyLevel > -0.01)  //if no oxygen, put helmet on
                {
                    LOG.Log.WriteLine("oxygen low, putting helmet on");
                    MyAPIGateway.Session.Player.Character.SwitchHelmet();
                    return;
                }
                else
                {
                    LOG.Log.WriteLine("helmet off, oxyLevel out of range " + oxyLevel.ToString());
                    return;
                }
            }
            LOG.Log.WriteLine("helmet enable is null " + helmetEnabled.ToString());
            return;
        }
        protected override void UnloadData()  //closes chat reader
        {
            LOG.Log.WriteLine("unloading data");
            if (LOG.debug)
            {
                LOG.Log.Close();
            }
            MyAPIGateway.Utilities.MessageEntered -= HandleMessageEntered;
        }
        private void HandleMessageEntered(string messageText, ref bool sendToOthers)  //filters chat inputs and sends to server
        {
            LOG.Log.WriteLine("handling message");
            if (messageText[0] != '/')  //quick check to see if command
            {
                LOG.Log.WriteLine("parse: not a command");
                return;
            }
            string messageLower = messageText.ToLower();
            if (!messageLower.StartsWith("/hat "))          //makes sure command pertains to the mod
            {
                LOG.Log.WriteLine("parse: not this mod");
                return;
            }

            string[] splits = messageLower.Split(' ');  //rejects if not a one word command
            LOG.Log.WriteLine("split0 = " + splits[0].ToString());
            LOG.Log.WriteLine("split1 = " + splits[1].ToString());
            if (splits.Length > 2)
            {
                LOG.Log.WriteLine("parse: too many words");
                MyAPIGateway.Utilities.ShowMessage("HelmetAutoToggle ", "Invalid command. Try /hat help");
                return;
            }

            switch (splits[1])  //parses incoming text and changes settings
            {
                case "on":
                    if (true)
                    {
                        LOG.Log.WriteLine("user set autotoggle to ON");
                        autotoggleON = true;
                        MyAPIGateway.Utilities.ShowMessage("HelmetAutoToggle ", "Mod Activated");

                        break;
                    }

                case "off":
                    if (true)
                    {
                        LOG.Log.WriteLine("user set autotoggle to OFF");
                        autotoggleON = false;
                        MyAPIGateway.Utilities.ShowMessage("HelmetAutoToggle ", "Mod Deactivated");
                        break;
                    }
                case "help":
                    {
                        LOG.Log.WriteLine("user asked for help");
                        string helpMessage = "This mod automatically takes your helmet on and off when leaving or entering high-oxygen environments.\r\n"
                            + "Currently enabled?  " + autotoggleON.ToString() + "\r\n"
                            + "Use \"/hat on\" to enable\r\n"
                            + "Use \"/hat off\" to disable";
                        MyAPIGateway.Utilities.ShowMessage("HelmetAutoToggle ", helpMessage);
                        break;
                    }

                default:
                    {
                        LOG.Log.WriteLine("invalid command");
                        MyAPIGateway.Utilities.ShowMessage("HelmetAutoToggle ", "Invalid command. Try /hat help");
                    }
                    return;
            }
        }
    }
}
