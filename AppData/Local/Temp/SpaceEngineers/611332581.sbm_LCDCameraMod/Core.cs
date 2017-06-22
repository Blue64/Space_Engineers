using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;
using LCDCameraMod.DataMessage;

namespace LCDCameraMod
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Core : MySessionComponentBase
    {
        private bool m_initialized = false;
        private List<DataHandlerBase> m_dataHandlers = new List<DataHandlerBase>();

        /// <summary>
        /// Initializer
        /// </summary>
        private void Initialize()
        {
            AddMessageHandler();

            m_dataHandlers.Add(new DataLCDAddSelection());
            m_dataHandlers.Add(new DataLCDRemoveSelection());
            m_dataHandlers.Add(new DataLCDRequest());
        }

        private void HandleClientDAta(byte[] data)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            Logging.Instance.WriteLine(string.Format("Received data: {0}", data.Length));

            foreach (DataHandlerBase handler in m_dataHandlers)
            {
                if (handler.CanHandle(data))
                {
                    try
                    {
                        Logging.Instance.WriteLine(string.Format("Processing: {0}", handler.GetType().Name));

                        byte[] newData;
                        ulong steamId;
                        handler.ProcessCommand(data, out newData, out steamId);
                        handler.HandleCommand(steamId, newData);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logging.Instance.WriteLine(string.Format("HandleCommand(): {0}", ex.ToString()));
                    }
                }
            }
        }

        private void AddMessageHandler()
        {
            MyAPIGateway.Multiplayer.RegisterMessageHandler(9020, HandleClientDAta);
        }

        private void RemoveMessageHandler()
        {
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(9020, HandleClientDAta);
        }

        /// <summary>
        /// MySessionComponentBase Implementation
        /// </summary>


        public override void UpdateBeforeSimulation()
        {
            try
            {
                if (MyAPIGateway.Session == null)
                    return;

                if (!m_initialized)
                {
                    m_initialized = true;
                    Initialize();
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("UpdateBeforeSimulation(): {0}", ex.ToString()));
            }
        }

        protected override void UnloadData()
        {
            try
            {
                RemoveMessageHandler();

                if (Logging.Instance != null)
                {
                    Logging.Instance.Close();
                }
            }
            catch(Exception ex)
            {
                Logging.Instance.WriteLine(string.Format("UnloadData(): {0}", ex.ToString()));
            }
        }
    }
}
