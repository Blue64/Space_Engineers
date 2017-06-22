using System;
using System.Collections.Generic;
using System.Text;

using Sandbox.ModAPI;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.Components;
using Sandbox.Definitions;
using Sandbox.ModAPI.Interfaces;
using System.Reflection;

namespace AirlockRKR
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock))]
    // [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon))]
    public class AirlockRKR : MyGameLogicComponent
    {

        static private bool Debugging = false;
        private Logger LOG = null;
        Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase m_objectBuilder = null;

        Control.Status progress = Control.Status.Standby;
        Dictionary<Sandbox.ModAPI.Ingame.IMySensorBlock, Boolean> sensorStatus = new Dictionary<Sandbox.ModAPI.Ingame.IMySensorBlock, bool>();
        private long abortStartTimeStamp = 0L;

        public override void Close()
        {
            // MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;
        }

        public override void Init(Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            m_objectBuilder = objectBuilder;

            if (Entity as IMyCubeBlock != null && Debugging)
            {

                IMyCubeBlock block = Entity as IMyCubeBlock;
                if (block.BlockDefinition.SubtypeName.Contains("AirlockRKR"))
                {
                    LOG = new Logger("textPanel", Debugging);
                }
            }
            //if (Entity as Sandbox.ModAPI.IMyDoor == null) {
            //    return;
            //}

            // IMyProgrammableBlock progBlock = Entity as IMyProgrammableBlock;
            //if (objectBuilder.SubtypeId.Equals("SmallProgrammableBlockRKR") || objectBuilder.SubtypeId.Equals("LargeProgrammableBlockRKR")) 
            //{
                
            //}
            // MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
        }

        public override void MarkForClose()
        {
        }

        public override void UpdateAfterSimulation()
        {
        }

        public override void UpdateAfterSimulation10()
        {
        }

        public override void UpdateAfterSimulation100()
        {
        }

        public override void UpdateBeforeSimulation()
        {
        }

        public override void UpdateBeforeSimulation10()
        {
            if (Entity as IMyCubeBlock != null && Entity as Sandbox.ModAPI.Ingame.IMyTerminalBlock != null)
            {
                IMyCubeBlock block = Entity as IMyCubeBlock;
                Sandbox.ModAPI.Ingame.IMyGridTerminalSystem gridTerminal = getGridTerminal(Entity);
                if (block.BlockDefinition.SubtypeName.Contains("AirlockRKR") && gridTerminal != null && (Entity as IMyCubeBlock).IsWorking)
                {
                    String name = (Entity as IMyTerminalBlock).CustomName;
                    String[] nameSplit = name.Split('#');
                    if (nameSplit.Length == 2 && nameSplit[1] != null)
                    {
                        String configLCDName = nameSplit[1];
                        Config config = new ConfigReader(gridTerminal, Entity as Sandbox.ModAPI.Ingame.IMyTerminalBlock, configLCDName).readConfig();
                        if (config.isStatusPanelValid() && !Debugging)
                        {
                            LOG = new Logger(config.statusPanel);
                        }
                        //MyAPIGateway.Utilities.ShowNotification(config.isStatusPanelValid() ? "status Panel gefunden" : "kein status Panel", 10000, MyFontEnum.Red);
                        if (LOG != null && !Debugging && config.isStatusPanelValid())
                        {
                            LOG.Clear(config.statusPanel);
                            LOG.log(config.statusPanel, "Uhrzeit: " + DateTime.Now.ToLongTimeString(), ErrorSeverity.Notice);
                            LOG.log(config.statusPanel, "Konfigurationspanel: " + configLCDName, ErrorSeverity.Notice);
                        }
                        else if (LOG != null && Debugging && gridTerminal != null)
                        {
                            LOG.Clear(gridTerminal);
                            LOG.log(gridTerminal, "Uhrzeit: " + DateTime.Now.ToLongTimeString(), ErrorSeverity.Notice);
                            LOG.log(gridTerminal, "Konfigurationspanel: " + configLCDName, ErrorSeverity.Notice);
                            LOG.log(gridTerminal, "Progress: " + this.progress.ToString() + "(" + this.progress.Equals(Control.Status.Standby) + ")", ErrorSeverity.Notice);
                        }
                        if (config.isValid())
                        {
                            if (LOG != null && config.isStatusPanelValid())
                            {
                                LOG.log(config.statusPanel, "Schleusendruck: " + Utils.getPressure(config.sluiceVents[0]) + "%", ErrorSeverity.Notice);
                            }
                            List<Control.Event> currentEvents = new List<Control.Event>();
                            if (this.abortStartTimeStamp != 0L && DateTime.Now.Ticks >= new DateTime(this.abortStartTimeStamp).AddMilliseconds(config.abortMilliseconds).Ticks)
                            {
                                currentEvents.Add(Control.Event.AbortTimerDown);
                            }
                            currentEvents.AddList(Utils.getSensorStatusChanges(this.sensorStatus, config));
                            if (LOG != null && Debugging && gridTerminal != null)
                            {
                                foreach (Control.Event currentEvent in currentEvents)
                                {
                                    LOG.log(gridTerminal, currentEvent.ToString(), ErrorSeverity.Error);
                                }
                                LOG.log(gridTerminal, "Doors1 " + Utils.isOneDoorOpen(config.doors1), ErrorSeverity.Notice);
                                LOG.log(gridTerminal, "Doors2 " + Utils.isOneDoorOpen(config.doors2), ErrorSeverity.Notice);
                            }
                            this.progress = Control.run(currentEvents, this.progress, config);
                            if ((this.abortStartTimeStamp == 0L) && (this.progress.Equals(Control.Status.leaveProgress) || this.progress.Equals(Control.Status.leaveSchleuse) || this.progress.Equals(Control.Status.enterProgress) || this.progress.Equals(Control.Status.enterSchleuse)))
                            {
                                this.abortStartTimeStamp = DateTime.Now.Ticks;
                            }
                            else if (this.progress.Equals(Control.Status.leavePressurize) || this.progress.Equals(Control.Status.Standby) || this.progress.Equals(Control.Status.enterPressurize))
                            {
                                this.abortStartTimeStamp = 0L;
                            }
                        }
                        else
                        {
                            if (LOG != null && config.isStatusPanelValid())
                            {
                                LOG.log(config.statusPanel, "Konfiguration ist " + (config.isValid() ? "" : "nicht ") + "valide", ErrorSeverity.Notice);
                                LOG.log(config.statusPanel, "Doors1 " + (config.isDoors1Valid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(config.statusPanel, "Sensors1 " + (config.isSensors1Valid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(config.statusPanel, "SluiceVents " + (config.isSluiceVentsValid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(config.statusPanel, "SluiceSensors " + (config.isSluiceSensorsValid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(config.statusPanel, "Doors2 " + (config.isDoors2Valid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(config.statusPanel, "Sensors2 " + (config.isSensors2Valid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(config.statusPanel, "Vents " + (config.isVentsValid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(config.statusPanel, "Lights " + (config.lightsFound() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                            } else if (LOG != null && Debugging && gridTerminal != null)
                            {
                                LOG.log(gridTerminal, "Konfiguration ist " + (config.isValid() ? "" : "nicht ") + "valide", ErrorSeverity.Notice);
                                LOG.log(gridTerminal, "Doors1 " + (config.isDoors1Valid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(gridTerminal, "Doors2 " + (config.isDoors2Valid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(gridTerminal, "Vents " + (config.isVentsValid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(gridTerminal, "SluiceVents  " + (config.isSluiceVentsValid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(gridTerminal, "Sensors1  " + (config.isSensors1Valid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(gridTerminal, "SluiceSensors  " + (config.isSluiceSensorsValid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                                LOG.log(gridTerminal, "Sensors2  " + (config.isSensors2Valid() ? "ok" : "nicht gefunden"), ErrorSeverity.Notice);
                            }
                            this.progress = Control.Status.Standby;
                            this.abortStartTimeStamp = 0L;
                            this.sensorStatus.Clear();
                        }
                    }
                }
                else
                {
                    this.progress = Control.Status.Standby;
                    this.abortStartTimeStamp = 0L;
                    this.sensorStatus.Clear();
                }
            }
        }

        public override void UpdateBeforeSimulation100()
        {
        }

        public override void UpdateOnceBeforeFrame()
        {
        }

        public override Sandbox.Common.ObjectBuilders.MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return m_objectBuilder;
        }

        public void listActions(Sandbox.ModAPI.Ingame.IMyTerminalBlock block, Logger log, Config config)
        {
            List<ITerminalAction> actions = new List<ITerminalAction>();
            block.GetActions(actions);
            for (int i = 0; i < actions.Count; i++)
            {
                log.log(config.statusPanel, actions[i].Name.ToString() + "(" + actions[i].Id + ")", ErrorSeverity.Notice);
            }
        }

        public void listProperties(Sandbox.ModAPI.Ingame.IMyTerminalBlock block, Logger log, Config config)
        {
            List<ITerminalProperty> properties = new List<ITerminalProperty>();
            block.GetProperties(properties);
            for (int i = 0; i < properties.Count; i++)
            {
                log.log(config.statusPanel, properties[i].Id, ErrorSeverity.Notice);
            }
        }

        public static Sandbox.ModAPI.Ingame.IMyGridTerminalSystem getGridTerminal(VRage.ModAPI.IMyEntity Entity)
        {
            return Sandbox.ModAPI.MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid((Entity as IMyCubeBlock).CubeGrid);
        }

    }
}
