using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Kage.HackingComputer
{
    public class HackingComputerSync
    {
        private static bool m_init = false;
        public static bool HasBeenInitialized
        {
            get
            {
                return m_init;
            }
        }

        public static ushort HackingStateMessageId
        {
            get
            {
                return 23889;
            }
        }

        public static void Initialize()
        {
            if (MyAPIGateway.Session.Player != null && m_init == false)
            {
                LogManager.WriteLine("Initializing HackingComputerSync");
                MyAPIGateway.Multiplayer.RegisterMessageHandler(HackingStateMessageId, handleHackingBlockStates);
            }
            m_init = true;
        }

        public static void Unload()
        {
            m_init = false;
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(HackingStateMessageId, handleHackingBlockStates);
        }

        private static void handleHackingBlockStates(byte[] m)
        {
            try
            {
                long entityId = BitConverter.ToInt64(m, 0);
                IMyEntity entity;
                if (MyAPIGateway.Entities.TryGetEntityById(entityId, out entity))
                {
                    HackingBlock.States state = (HackingBlock.States)BitConverter.ToInt32(m, 8);
                    long targetId = BitConverter.ToInt64(m, 12);
                    int chance = BitConverter.ToInt32(m, 20);

                    var logic = entity.GameLogic.GetAs<HackingBlock>();
                    if (logic != null)
                    {
                        logic.CurrentState = state;
                        logic.TargetId = targetId;
                        logic.Chance = chance;
                        logic.UpdateClient();

                        IMyEntity target = null;
                        MyAPIGateway.Entities.TryGetEntityById(targetId, out target);
                        if (target != null)
                        {
                            FirewallBlock firewall = target.GameLogic.GetAs<FirewallBlock>();
                            if (firewall != null)
                            {
                                if (state == HackingBlock.States.Hacking)
                                {
                                    firewall.BlockedAttempts++;
                                }
                                else if (state == HackingBlock.States.Success)
                                {
                                    firewall.BlockedAttempts = 0;
                                }
                            }
                                
                        }
                    }
                    else
                    {
                        LogManager.WriteLine("handleHackingBlockStates: Unable to get GameLogic Component");
                    }
                }
            }
            catch (Exception e)
            {
                LogManager.WriteException(e);
            }
        }

        public static void SendHackingBlockStates(HackingBlock b)
        {
            try
            {
                byte[] message = new byte[24];
                Array.Copy(BitConverter.GetBytes(b.Entity.EntityId), 0, message, 0, 8);
                Array.Copy(BitConverter.GetBytes((int)b.CurrentState), 0, message, 8, 4);
                Array.Copy(BitConverter.GetBytes(b.TargetId), 0, message, 12, 8);
                Array.Copy(BitConverter.GetBytes(b.Chance), 0, message, 20, 4);

                List<IMyPlayer> players = new List<IMyPlayer>();
                MyAPIGateway.Multiplayer.Players.GetPlayers(players);
                foreach (var player in players)
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(HackingStateMessageId, message, player.SteamUserId);
                }
            }
            catch (Exception e)
            {
                LogManager.WriteException(e);
            }
        }
    }
}