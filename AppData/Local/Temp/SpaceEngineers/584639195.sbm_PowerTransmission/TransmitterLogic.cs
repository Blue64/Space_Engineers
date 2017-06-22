using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Common.Utils;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using VRage.ModAPI;
using Sandbox.Game.EntityComponents;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;

namespace Cython.PowerTransmission
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
	public class TransmitterLogic: MySessionComponentBase
	{
		MyObjectBuilder_SessionComponent m_sessionComponent;

		bool m_init = false;

		readonly string m_saveFileName = "PowerTransmitters.sav";

		public static PTSaveFile transmittersSaveFile = new PTSaveFile();

		List<long> m_dictionaryCopy = new List<long>();

		public override MyObjectBuilder_SessionComponent GetObjectBuilder ()
		{
			return m_sessionComponent;
		}

		public override void UpdateBeforeSimulation ()
		{
			if(!m_init)
				init();
		}

		public override void UpdateAfterSimulation ()
		{

			m_dictionaryCopy.Clear ();
			
			foreach (var grid in TransmissionManager.totalPowerPerGrid) {

				m_dictionaryCopy.Add (grid.Key);
			}

			foreach (var grid in m_dictionaryCopy) {
				TransmissionManager.totalPowerPerGrid [grid] = 0f;
			}

			base.UpdateAfterSimulation ();
		}

		protected override void UnloadData ()
		{
			if(m_init) 
			{
				MyAPIGateway.Multiplayer.UnregisterMessageHandler(5910, handleMessage);
			}
		}

		void init()
		{
			MyAPIGateway.Multiplayer.RegisterMessageHandler(5910, handleMessage);

			loadPTSaveFile();

			m_init = true;
		}

		void loadPTSaveFile() 
		{
			if (MyAPIGateway.Utilities.FileExistsInLocalStorage (m_saveFileName, typeof(TransmitterLogic))) 
			{
				if (MyAPIGateway.Multiplayer.IsServer || !MyAPIGateway.Multiplayer.MultiplayerActive) 
				{
					
					try 
					{
						string buffer;

						using (TextReader file = MyAPIGateway.Utilities.ReadFileInLocalStorage (m_saveFileName, typeof(TransmitterLogic))) 
						{
							buffer = file.ReadToEnd ();
						}

						TransmitterLogic.transmittersSaveFile = MyAPIGateway.Utilities.SerializeFromXML<PTSaveFile> (buffer);

					} 
					catch (InvalidOperationException e) 
					{

					}
				}
			}

			foreach(var ptInfo in TransmitterLogic.transmittersSaveFile.Transmitters)
			{
				if(MyAPIGateway.Entities.EntityExists(ptInfo.Id))
				{
					IMyEntity transmitter = MyAPIGateway.Entities.GetEntityById(ptInfo.Id);

					if(ptInfo.Type.Equals("R"))
					{
						var RPT = transmitter.GameLogic.GetAs<RadialPowerTransmitter>();

						RPT.m_sender = ptInfo.Sender;
						RPT.m_info.sender = ptInfo.Sender;
						RPT.m_channel = ptInfo.ChannelTarget;
						RPT.m_info.channel = ptInfo.ChannelTarget;
						RPT.m_transmittedPower = ptInfo.Power;
					}
					else
					{
						var OPT = transmitter.GameLogic.GetAs<OpticalPowerTransmitter>();

						OPT.m_sender = ptInfo.Sender;
						OPT.m_info.sender = ptInfo.Sender;
						OPT.m_id = ptInfo.ChannelTarget;
						OPT.m_targetId = ptInfo.ChannelTarget;
						OPT.m_info.id = ptInfo.ChannelTarget;
						OPT.m_transmittedPower = ptInfo.Power;
					}
				}
			}
		}

		void savePTSaveFile() 
		{
			if(MyAPIGateway.Multiplayer != null)
			{
				if (MyAPIGateway.Multiplayer.IsServer || !MyAPIGateway.Multiplayer.MultiplayerActive) 
				{
					try 
					{
						using (TextWriter saveFile = MyAPIGateway.Utilities.WriteFileInLocalStorage (m_saveFileName, typeof(TransmitterLogic))) 
						{
							saveFile.Write(MyAPIGateway.Utilities.SerializeToXML<PTSaveFile> (TransmitterLogic.transmittersSaveFile));
						}
					} 
					catch (InvalidOperationException e) 
					{

					}
				}
			}

		}

		public override void SaveData ()
		{
			savePTSaveFile();
		}

		void handleMessage(byte[] message)
		{
			int id = BitConverter.ToInt32(message, 0);

			// rpt
			if(id == 0)
			{
				long entityId = BitConverter.ToInt64(message, 4);
				bool value = BitConverter.ToBoolean(message, 12);

				IMyEntity refinery;

				if(MyAPIGateway.Entities.TryGetEntityById(entityId, out refinery))
				{
					var RPT = refinery.GameLogic.GetAs<RadialPowerTransmitter>();
					RPT.m_sender = value;
					RPT.m_info.sender = value;
				}
			}
			else if(id == 1)
			{
				long entityId = BitConverter.ToInt64(message, 4);

				uint value = BitConverter.ToUInt32(message, 12);

				IMyEntity rptEntity;

				if(MyAPIGateway.Entities.TryGetEntityById(entityId, out rptEntity))
				{
					var RPT = rptEntity.GameLogic.GetAs<RadialPowerTransmitter>();
					RPT.m_channel = value;
					RPT.m_info.channel = value;
				}
			}
			else if(id == 2)
			{
				long entityId = BitConverter.ToInt64(message, 4);

				float value = BitConverter.ToSingle(message, 12);

				IMyEntity rptEntity;

				if(MyAPIGateway.Entities.TryGetEntityById(entityId, out rptEntity))
				{
					var RPT = rptEntity.GameLogic.GetAs<RadialPowerTransmitter>();
					RPT.m_transmittedPower = value;
				}
			}

			// opt
			else if(id == 3)
			{
				long entityId = BitConverter.ToInt64(message, 4);
				bool value = BitConverter.ToBoolean(message, 12);

				IMyEntity refinery;

				if(MyAPIGateway.Entities.TryGetEntityById(entityId, out refinery))
				{
					var OPT = refinery.GameLogic.GetAs<OpticalPowerTransmitter>();
					OPT.m_sender = value;
					OPT.m_info.sender = value;
				}
			}
			else if(id == 4)
			{
				long entityId = BitConverter.ToInt64(message, 4);

				uint value = BitConverter.ToUInt32(message, 12);

				IMyEntity rptEntity;

				if(MyAPIGateway.Entities.TryGetEntityById(entityId, out rptEntity))
				{
					var OPT = rptEntity.GameLogic.GetAs<OpticalPowerTransmitter>();
					if(OPT.m_sender)
					{
						OPT.m_targetId = value;
					}
					else
					{
						OPT.m_id = value;
						OPT.m_info.id = value;
					}
				}
			}
			else if(id == 5)
			{
				long entityId = BitConverter.ToInt64(message, 4);

				float value = BitConverter.ToSingle(message, 12);

				IMyEntity rptEntity;

				if(MyAPIGateway.Entities.TryGetEntityById(entityId, out rptEntity))
				{
					var OPT = rptEntity.GameLogic.GetAs<OpticalPowerTransmitter>();
					OPT.m_transmittedPower = value;
				}
			}
			else if(id == 10)
			{
				if(MyAPIGateway.Multiplayer.IsServer)
				{
					ulong senderId = BitConverter.ToUInt64(message, 4);
					long entityId = BitConverter.ToInt64(message, 12);

					IMyEntity rptEntity;

					if(MyAPIGateway.Entities.TryGetEntityById(entityId, out rptEntity))
					{
						var RPT = rptEntity.GameLogic.GetAs<RadialPowerTransmitter>();

						byte[] answer = new byte[21];

						byte[] answerId = BitConverter.GetBytes(10);
						byte[] answerEntityId = BitConverter.GetBytes(entityId);
						byte[] answerChannel = BitConverter.GetBytes(RPT.m_channel);
						byte[] answerPower = BitConverter.GetBytes(RPT.m_transmittedPower);

						for(int i = 0; i < 4; i++)
							answer[i] = answerId[i];

						for(int i = 0; i < 8; i++)
							answer[i] = answerEntityId[i];
						
						answer[4] = BitConverter.GetBytes(RPT.m_sender)[0];

						for(int i = 0; i < 4; i++)
							answer[i+13] = answerChannel[i];

						for(int i = 0; i < 4; i++)
							answer[i+17] = answerPower[i];

						MyAPIGateway.Multiplayer.SendMessageTo(5910, answer, senderId, true);
					}
				}
				else
				{
					long entityId = BitConverter.ToInt64(message, 4);

					IMyEntity rptEntity;

					if(MyAPIGateway.Entities.TryGetEntityById(entityId, out rptEntity))
					{
						var RPT = rptEntity.GameLogic.GetAs<RadialPowerTransmitter>();

						bool sender = BitConverter.ToBoolean(message, 12);
						uint channel = BitConverter.ToUInt32(message, 13);
						float power = BitConverter.ToSingle(message, 17);

						RPT.m_sender = sender;
						RPT.m_info.sender = sender;
						RPT.m_channel = channel;
						RPT.m_info.channel = channel;

						RPT.m_transmittedPower = power;
					}
				}
			}
			else if(id == 11)
			{
				if(MyAPIGateway.Multiplayer.IsServer)
				{
					ulong senderId = BitConverter.ToUInt64(message, 4);
					long entityId = BitConverter.ToInt64(message, 12);

					IMyEntity rptEntity;

					if(MyAPIGateway.Entities.TryGetEntityById(entityId, out rptEntity))
					{
						var OPT = rptEntity.GameLogic.GetAs<OpticalPowerTransmitter>();

						byte[] answer = new byte[25];

						byte[] answerId = BitConverter.GetBytes(11);
						byte[] answerEntityId = BitConverter.GetBytes(entityId);
						byte[] answerThisId = BitConverter.GetBytes(OPT.m_id);
						byte[] answerTargetId = BitConverter.GetBytes(OPT.m_targetId);
						byte[] answerPower = BitConverter.GetBytes(OPT.m_transmittedPower);

						for(int i = 0; i < 4; i++)
							answer[i] = answerId[i];

						for(int i = 0; i < 8; i++)
							answer[i] = answerEntityId[i];

						answer[4] = BitConverter.GetBytes(OPT.m_sender)[0];

						for(int i = 0; i < 4; i++)
							answer[i+13] = answerThisId[i];

						for(int i = 0; i < 4; i++)
							answer[i+17] = answerTargetId[i];

						for(int i = 0; i < 4; i++)
							answer[i+21] = answerPower[i];

						MyAPIGateway.Multiplayer.SendMessageTo(5910, answer, senderId, true);
					}
				}
				else
				{
					long entityId = BitConverter.ToInt64(message, 4);

					IMyEntity rptEntity;

					if(MyAPIGateway.Entities.TryGetEntityById(entityId, out rptEntity))
					{
						var OPT = rptEntity.GameLogic.GetAs<OpticalPowerTransmitter>();

						bool sender = BitConverter.ToBoolean(message, 12);
						uint thisId = BitConverter.ToUInt32(message, 13);
						uint targetId = BitConverter.ToUInt32(message, 17);
						float power = BitConverter.ToSingle(message, 21);

						OPT.m_sender = sender;
						OPT.m_info.sender = sender;
						OPT.m_id = thisId;
						OPT.m_info.id = thisId;
						OPT.m_targetId = targetId;

						OPT.m_transmittedPower = power;
					}
				}
			}
		}

	}
		
}

