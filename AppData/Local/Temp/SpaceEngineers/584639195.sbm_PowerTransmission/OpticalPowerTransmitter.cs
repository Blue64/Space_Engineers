using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using VRage.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.Game.EntityComponents;
using System.Collections.Generic;
using VRage.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using System;
using System.Text;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;

namespace Cython.PowerTransmission
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Refinery), "LargeBlockSmallOpticalPowerTransmitter", "SmallBlockSmallOpticalPowerTransmitter")]
	public class OpticalPowerTransmitter: MyGameLogicComponent
	{
		static IMyTerminalControlOnOffSwitch m_controlSender = null;
		static IMyTerminalControlTextbox m_controlId = null;
		static IMyTerminalControlTextbox m_controlPower = null;

		static bool m_controlsInit = false;
		
		MyObjectBuilder_EntityBase m_objectBuilder;

		IMyFunctionalBlock m_functionalBlock;
		IMyCubeBlock m_cubeBlock;
		IMyTerminalBlock m_terminalBlock;

		int m_ticks = 0;

		long m_entityId;

		string m_subtypeName;

		int counter;

		MyResourceSinkComponent m_resourceSink;

		public MyDefinitionId m_electricityDefinition;

		public OpticalPowerTransmitterInfo m_info = new OpticalPowerTransmitterInfo();

		public PTInfo m_saveInfo;

		float m_oldTransmittedPower = 0f;
		public float m_transmittedPower = 0f;

		uint m_senders = 0;

		float m_currentOutput = 0f;

		float m_maxRange = 20.0f;
		float m_maxRangeSquared = 400.0f;

		float m_maxPower = 1.0f;
		float m_currentMaxPower = 1.0f;
		float m_receivingPower = 0f;
		public float m_powerToTransfer = 0f;
		float m_powerMultiplicator = 0.95f;

		float m_oldPowerToTransfer = 0f;
		float m_currentRequiredInput = 0f;

		float m_rayOffset = 4.0f;

		public uint m_id = 0;

		public uint m_targetId = 0;
		uint m_targetIdOld = 0;
		bool m_targetVisible = false;

		public bool m_sender = false;


		OpticalPowerTransmitterInfo m_target = null;

		Dictionary<IMyFunctionalBlock, float> m_receiver = new Dictionary<IMyFunctionalBlock, float> ();

		public override void Init (MyObjectBuilder_EntityBase objectBuilder)
		{
			base.Init (objectBuilder);

			m_objectBuilder = m_objectBuilder;

			Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;

			m_entityId = Entity.EntityId;

			m_functionalBlock = Entity as IMyFunctionalBlock;

			m_electricityDefinition = new MyDefinitionId (typeof(MyObjectBuilder_GasProperties), "Electricity");

			m_terminalBlock = Entity as IMyTerminalBlock;

			m_subtypeName = m_functionalBlock.BlockDefinition.SubtypeName;

			m_maxPower = getMaxPower (m_subtypeName);

			m_powerMultiplicator = getPowerMultiplicator (m_subtypeName);

			m_currentMaxPower = m_maxPower;

			m_maxRange = getMaxRange (m_subtypeName);

			m_maxRangeSquared = m_maxRange * m_maxRange;

			m_rayOffset = this.getRayOffset (m_subtypeName);

			m_info.rayOffset = m_rayOffset;

			m_info.functionalBlock = m_functionalBlock;

			m_info.subtypeName = m_subtypeName;

			m_cubeBlock = Entity as IMyCubeBlock;

			m_cubeBlock.AddUpgradeValue ("OpticalPowerStrength", 1.0f);

			m_functionalBlock.CustomNameChanged += parseName;

			m_cubeBlock.OnUpgradeValuesChanged += onUpgradeValuesChanged;

			m_terminalBlock.AppendingCustomInfo += appendCustomInfo;

		
		}

		public override MyObjectBuilder_EntityBase GetObjectBuilder (bool copy = false)
		{
			return m_objectBuilder;
		}

		public override void UpdateOnceBeforeFrame ()
		{
			base.UpdateOnceBeforeFrame ();

			m_resourceSink = Entity.Components.Get<MyResourceSinkComponent> ();
			parseName ((IMyTerminalBlock)m_functionalBlock);

			m_saveInfo = new PTInfo(Entity.EntityId, m_sender, m_id, m_transmittedPower, "O");

			if(!MyAPIGateway.Multiplayer.IsServer)
				requestSettingsFromServer();

			m_info.strength = m_currentMaxPower;

			createUI();
		}

		void requestSettingsFromServer()
		{
			byte[] message = new byte[20];
			byte[] messageId = BitConverter.GetBytes(11);
			byte[] messageSender = BitConverter.GetBytes(MyAPIGateway.Session.Player.SteamUserId);
			byte[] messageEntityId = BitConverter.GetBytes(Entity.EntityId);

			for(int i = 0; i < 4; i++)
				message[i] = messageId[i];
			for(int i = 0; i < 8; i++)
				message[i+4] = messageSender[i];
			for(int i = 0; i < 8; i++)
				message[i+12] = messageEntityId[i];

			MyAPIGateway.Multiplayer.SendMessageToServer(5910, message, true);
		}

		public override void OnRemovedFromScene ()
		{
			m_info.functionalBlock = null;
			if (TransmissionManager.opticalTransmitters.ContainsKey (m_entityId)) {
				TransmissionManager.opticalTransmitters.Remove (m_entityId);
			}

			base.OnRemovedFromScene ();
		}

		public override void OnAddedToScene ()
		{
			base.OnAddedToScene ();

			m_entityId = Entity.EntityId;
			m_info.functionalBlock = Entity as IMyFunctionalBlock;
			if (Entity.InScene) { 

				if (!TransmissionManager.opticalTransmitters.ContainsKey (m_entityId)) {
					TransmissionManager.opticalTransmitters.Add (m_entityId, m_info);
				}

				if(!TransmissionManager.totalPowerPerGrid.ContainsKey(m_functionalBlock.CubeGrid.EntityId)) {

					TransmissionManager.totalPowerPerGrid.Add (m_functionalBlock.CubeGrid.EntityId, 0);
				}
			}

		}

		public override void UpdateBeforeSimulation ()
		{
			base.UpdateBeforeSimulation();
			//MyAPIGateway.Utilities.ShowNotification ("YEP: " + m_subtypeName, 17);

			if (m_functionalBlock.IsFunctional) {

				if (m_functionalBlock.Enabled) {

					m_info.enabled = true;
				} else {

					m_info.enabled = false;
				}

			} else {
				
				m_info.enabled = false;

			}

			if (!m_sender) {

				if (m_currentRequiredInput != 0) {

					m_resourceSink.SetRequiredInputByType (m_electricityDefinition, 0);
					m_oldTransmittedPower = 0;
					m_currentRequiredInput = 0;
				}

				m_target = null;
					

			} else if (m_functionalBlock.Enabled && m_functionalBlock.IsFunctional) {

				if (m_target != null) {

					if (m_target.functionalBlock == null) {
						m_target = null;
					} else {
						if (!m_target.functionalBlock.Enabled) {
							m_target = null;
						}
					}
				}

				if(m_ticks % 100 == 0) {
					
					if (m_sender) {
						


						maintainConnection ();


					}

					m_targetIdOld = m_targetId;
				}

				if (m_target != null) {

					if (m_targetVisible) {

						m_currentOutput = m_resourceSink.CurrentInputByType (m_electricityDefinition);

						m_powerToTransfer = m_currentOutput * m_powerMultiplicator;

						var transmitterComponent = m_target.functionalBlock.GameLogic.GetAs < OpticalPowerTransmitter>();




						if ((transmitterComponent.m_receivingPower + m_powerToTransfer) > m_target.strength) {

							m_powerToTransfer = m_target.strength - transmitterComponent.m_receivingPower;
						}

						transmitterComponent.m_receivingPower += m_powerToTransfer;
						transmitterComponent.m_senders++;
						//MyAPIGateway.Utilities.ShowNotification ("ADD: " + m_target.functionalBlock.CubeGrid.EntityId + ":" + powerToTransfer, 17, MyFontEnum.DarkBlue);
						TransmissionManager.totalPowerPerGrid [m_target.functionalBlock.CubeGrid.EntityId] = TransmissionManager.totalPowerPerGrid [m_target.functionalBlock.CubeGrid.EntityId] + m_powerToTransfer;
					}
				}

			} else {

				m_target = null;

				if (m_currentRequiredInput != 0) {

					m_resourceSink.SetRequiredInputByType (m_electricityDefinition, 0);
					m_oldTransmittedPower = 0;
					m_currentRequiredInput = 0;
				}
			}

			if(m_ticks % 100 == 0) {

				m_terminalBlock.RefreshCustomInfo ();
			}

			if(m_sender)
			{
				m_saveInfo.ChannelTarget = m_targetId;
			}
			else
			{
				m_saveInfo.ChannelTarget = m_id;
			}

			m_saveInfo.Sender = m_sender;
			m_saveInfo.Power = m_transmittedPower;

			m_receivingPower = 0f;
			m_senders = 0;



			m_ticks++;


		}

		public override void UpdateAfterSimulation ()
		{
			
			if(m_ticks == 1)
			{
				bool contains = false;
				foreach(var ptInfo in TransmitterLogic.transmittersSaveFile.Transmitters) 
				{
					if(ptInfo.Id == Entity.EntityId)
					{
						contains = true;

						m_saveInfo = ptInfo;
					}
				}

				if(!contains)
				{
					m_saveInfo = new PTInfo(Entity.EntityId, m_sender, m_id, m_transmittedPower, "O");
					TransmitterLogic.transmittersSaveFile.Transmitters.Add(m_saveInfo);
				}
			}
		}

		public void maintainConnection() {
			
			//MyAPIGateway.Utilities.ShowNotification ("MAINTAIN", 1000, MyFontEnum.White);

			if (m_target != null) {
				if (m_target.functionalBlock == null) {
					//MyAPIGateway.Utilities.ShowNotification ("TARGET REMOVED", 1000, MyFontEnum.White);
					m_target = null;
				}
			}

			if (m_target != null) {

				//MyAPIGateway.Utilities.ShowNotification ("GOT TARGET", 1000, MyFontEnum.White);

				if (m_targetIdOld != m_target.id) {
					m_target = null;
				} else {

					m_targetIdOld = m_target.id;
				}

			}

			if (m_target == null) {
				//MyAPIGateway.Utilities.ShowNotification ("LF TARGET", 1000, MyFontEnum.White);
				foreach (var transmitter in TransmissionManager.opticalTransmitters) {
					//MyAPIGateway.Utilities.ShowNotification ("CHECK " + transmitter.Value.id + " " + m_targetId, 1000, MyFontEnum.White);
					if (transmitter.Value.id == m_targetId) {
						if (!transmitter.Value.sender) {
							if (transmitter.Value.functionalBlock.Enabled) {
								//MyAPIGateway.Utilities.ShowNotification ("GOT TARGET", 1000, MyFontEnum.White);
								m_target = transmitter.Value;
								m_targetIdOld = m_target.id;
							}
						}

					}

					//MyAPIGateway.Utilities.ShowNotification ("COUNT: " + TransmissionManager.opticalTransmitters.Count, 1667);
				}
			} 


			if (m_target != null) {
				
				//MyAPIGateway.Utilities.ShowNotification ("RECEIVER: " + m_target.functionalBlock.EntityId, 1000, MyFontEnum.White);

				m_targetVisible = false;
				Vector3D thisPosition = m_functionalBlock.GetPosition ();
				Vector3D targetPosition = m_target.functionalBlock.GetPosition ();

				if (m_transmittedPower != m_oldTransmittedPower) {

					m_currentRequiredInput = m_transmittedPower;

					m_resourceSink.SetRequiredInputByType (m_electricityDefinition, m_transmittedPower);
					//MyAPIGateway.Utilities.ShowNotification ("COUNT: " + TransmissionManager.opticalTransmitters.Count, 3000, MyFontEnum.Red);
					m_oldTransmittedPower = m_transmittedPower;
				}

				if (m_target.enabled) {

					double distance = Vector3D.DistanceSquared (thisPosition, targetPosition);

					//MyAPIGateway.Utilities.ShowNotification ("RANGE: " + distance + ":" + m_maxRangeSquared, 1000, MyFontEnum.White);

					if (distance < m_maxRangeSquared) {
						
						Vector3D direction = targetPosition - thisPosition;
						direction.Normalize ();

						//MyAPIGateway.Utilities.ShowNotification ("VEC: " + thisPosition + ":" + targetPosition + ":" + direction * 20, 1000, MyFontEnum.White);

						if (!MyAPIGateway.Entities.IsRaycastBlocked (thisPosition + direction * m_rayOffset, targetPosition - direction * m_target.rayOffset)) {

							m_targetVisible = true;

							//MyAPIGateway.Utilities.ShowNotification ("" + m_target.functionalBlock.CubeGrid.EntityId + " VISIBLE", 1000);

							return;

						} else {

							m_targetVisible = false;
							//MyAPIGateway.Utilities.ShowNotification ("BLOCKED", 1000, MyFontEnum.Red);
						}
					}
				}
				
			}

			m_targetVisible = false;

			if (m_currentRequiredInput != 0) {

				m_resourceSink.SetRequiredInputByType (m_electricityDefinition, 0);
				m_oldTransmittedPower = 0;
				m_currentRequiredInput = 0;
			}
		}

		float getMaxPower(string subtypeName) {

			if (TransmissionManager.configuration.UseMaximumPower) {

				if (subtypeName == "LargeBlockSmallOpticalPowerTransmitter") {
					return TransmissionManager.configuration.LargeBlockSmallOpticalPowerTransmitter.MaximumPower;
				} else if (subtypeName == "SmallBlockSmallOpticalPowerTransmitter") {
					return TransmissionManager.configuration.SmallBlockSmallOpticalPowerTransmitter.MaximumPower;
				}

			} else {

				return float.PositiveInfinity;
			}

			return 1.0f;

		}

		float getPowerMultiplicator(string subtypeName) {

			if (TransmissionManager.configuration.UseMaximumPower) {

				if (subtypeName == "LargeBlockSmallOpticalPowerTransmitter") {
					return TransmissionManager.configuration.LargeBlockSmallOpticalPowerTransmitter.PowerMultiplicator;
				} else if (subtypeName == "SmallBlockSmallOpticalPowerTransmitter") {
					return TransmissionManager.configuration.SmallBlockSmallOpticalPowerTransmitter.PowerMultiplicator;
				}

			} else {

				return float.PositiveInfinity;
			}

			return 1.0f;

		}

		float getMaxRange(string subtypeName) {

			if (TransmissionManager.configuration.UseMaximumPower) {

				if (subtypeName == "LargeBlockSmallOpticalPowerTransmitter") {
					return TransmissionManager.configuration.LargeBlockSmallOpticalPowerTransmitter.MaximumRange;
				} else if (subtypeName == "SmallBlockSmallOpticalPowerTransmitter") {
					return TransmissionManager.configuration.SmallBlockSmallOpticalPowerTransmitter.MaximumRange;
				}

			} else {

				return float.PositiveInfinity;
			}

			return 20.0f;

		}

		float getRayOffset(string subtypeName) {

			if (subtypeName == "LargeBlockSmallOpticalPowerTransmitter") {
				return 4f;
			} else if (subtypeName == "SmallBlockSmallOpticalPowerTransmitter") {
				return 0.5f;
			}

			return 0.5f;
		}

		void parseName(IMyTerminalBlock terminalBlock) {

			int settingsStart = terminalBlock.CustomName.IndexOf ("(");

			if (settingsStart != -1) {

				if(settingsStart < (terminalBlock.CustomName.Length-1)) {

					int start = terminalBlock.CustomName.IndexOf ("P:", settingsStart + 1);

					if (start != -1) {

						if ((start + 2) < (terminalBlock.CustomName.Length - 1)) {

							int end = terminalBlock.CustomName.IndexOf (',', start + 2);

							if (end != -1) {

								try {

									m_transmittedPower = Convert.ToSingle (terminalBlock.CustomName.Substring (start + 2, end - (start + 2)));

									if(m_transmittedPower > m_currentMaxPower) {
										m_transmittedPower = m_currentMaxPower;
									}

									//MyAPIGateway.Utilities.ShowNotification ("" + m_transmittedPower, 1000, MyFontEnum.DarkBlue);

								} catch (Exception e) {
									//MyAPIGateway.Utilities.ShowNotification ("" + (start + 2) + " " + (end - (start + 2)) + e.Message, 1000, MyFontEnum.Red);
								}

							} else {

								end = terminalBlock.CustomName.IndexOf (')', start + 2);

								if (end != -1) {

									try {

										m_transmittedPower = Convert.ToSingle (terminalBlock.CustomName.Substring (start + 2, end - (start + 2)));

										if(m_transmittedPower > m_currentMaxPower) {
											m_transmittedPower = m_currentMaxPower;
										}

										//MyAPIGateway.Utilities.ShowNotification ("" + m_transmittedPower, 1000, MyFontEnum.DarkBlue);

									} catch (Exception e) {
										//MyAPIGateway.Utilities.ShowNotification ("" + (start + 2) + " " + (end - (start + 2)) + e.Message, 1000, MyFontEnum.Red);
									}
								}
							}
						}
					} else {
						m_transmittedPower = 0f;
					}

					start = terminalBlock.CustomName.IndexOf ("T:", settingsStart + 1);

					if (start != -1) {

						if ((start + 2) < (terminalBlock.CustomName.Length-1)) {

							int end = terminalBlock.CustomName.IndexOf (',', start + 2);

							if (end != -1) {

								try {

									m_targetId = Convert.ToUInt32 (terminalBlock.CustomName.Substring (start + 2, end - (start + 2)));

									//MyAPIGateway.Utilities.ShowNotification ("" + m_targetId, 4000, MyFontEnum.DarkBlue);

								} catch (Exception e) {

									//MyAPIGateway.Utilities.ShowNotification ("" + e.Message, 4000, MyFontEnum.Red);
								}

							} else {

								end = terminalBlock.CustomName.IndexOf (')', start + 2);

								if (end != -1) {

									try {

										m_targetId = Convert.ToUInt32 (terminalBlock.CustomName.Substring (start + 2, end - (start + 2)));

										//MyAPIGateway.Utilities.ShowNotification ("" + m_targetId, 4000, MyFontEnum.DarkBlue);

									} catch (Exception e) {
										//MyAPIGateway.Utilities.ShowNotification ("" + e.Message, 4000, MyFontEnum.Red);
									}
								}
							}
						}
					}

					start = terminalBlock.CustomName.IndexOf ("I:", settingsStart + 1);

					if (start != -1) {

						if ((start + 2) < (terminalBlock.CustomName.Length-1)) {

							int end = terminalBlock.CustomName.IndexOf (',', start + 2);

							if (end != -1) {

								try {

									m_id = Convert.ToUInt32 (terminalBlock.CustomName.Substring (start + 2, end - (start + 2)));
									m_info.id = m_id;

									//MyAPIGateway.Utilities.ShowNotification ("" + m_id, 4000, MyFontEnum.DarkBlue);

								} catch (Exception e) {

									//MyAPIGateway.Utilities.ShowNotification ("" + e.Message, 4000, MyFontEnum.Red);
								}

							} else {

								end = terminalBlock.CustomName.IndexOf (')', start + 2);

								if (end != -1) {

									try {

										m_id = Convert.ToUInt32 (terminalBlock.CustomName.Substring (start + 2, end - (start + 2)));
										m_info.id = m_id;
										//MyAPIGateway.Utilities.ShowNotification ("" + m_id, 4000, MyFontEnum.DarkBlue);

									} catch (Exception e) {
										//MyAPIGateway.Utilities.ShowNotification ("" + e.Message, 4000, MyFontEnum.Red);
									}
								}
							}
						}
					}

					start = terminalBlock.CustomName.IndexOf ("M:", settingsStart + 1);

					if (start != -1) {

						if ((start + 2) < (terminalBlock.CustomName.Length - 1)) {

							int end = terminalBlock.CustomName.IndexOf (',', start + 2);

							if (end != -1) {

								try {

									m_sender = terminalBlock.CustomName.Substring (start + 2, end - (start + 2)) == "S";

									m_info.sender = m_sender;
									//MyAPIGateway.Utilities.ShowNotification ("" + m_sender, 1000, MyFontEnum.DarkBlue);

								} catch (Exception e) {

									//MyAPIGateway.Utilities.ShowNotification ("" + e.Message, 1000, MyFontEnum.Red);
								}
							} else {

								end = terminalBlock.CustomName.IndexOf (')', start + 2);

								if (end != -1) {

									try {

										m_sender = terminalBlock.CustomName.Substring (start + 2, end - (start + 2)) == "S";

										m_info.sender = m_sender;
										//MyAPIGateway.Utilities.ShowNotification ("" + m_sender, 1000, MyFontEnum.DarkBlue);

									} catch (Exception e) {
										//MyAPIGateway.Utilities.ShowNotification ("" + e.Message, 1000, MyFontEnum.Red);
									}
								}
							}
						}
					} else {
						m_sender = false;

						m_info.sender = m_sender;
					}
				}
			}
		}



		public void appendCustomInfo(IMyTerminalBlock block, StringBuilder info)
		{
			info.Clear ();
			info.AppendLine (" ");
			info.AppendLine ("-----Optical Transmitter Info-----");
			info.AppendLine (" ");

			if (m_sender) {

				info.AppendLine ("(M)ode: Sender");

				info.AppendLine ("(T)arget ID: " + m_targetId);

				if (m_functionalBlock.Enabled) {
					if (m_target == null) {
						
						info.AppendLine ("Status: Searching for Target");

					} else {

						if (m_targetVisible) {
							
							info.AppendLine ("Status: Connected and Visible");
							info.AppendLine ("(P)ower sent: " + m_currentOutput.ToString ("N") + "MW / " + m_transmittedPower.ToString ("N") + "MW");

						} else {
							
							info.AppendLine ("Status: Connected but Blocked");
						}
					}

				} else {

					info.AppendLine ("Status: Disabled");
				}

				info.AppendLine (" ");

				info.AppendLine ("Range: " + (m_maxRange / 1000d).ToString("N") + "KM");

			} else {

				info.AppendLine ("(M)ode: Receiver");

				info.AppendLine ("(I)D: " + m_id);

				info.AppendLine (" ");

				info.AppendLine ("Power Receiving: " + m_receivingPower.ToString("N") + "MW / " + m_currentMaxPower.ToString("N") + "MW");

				info.AppendLine ("Number of Sources: " + m_senders);

			}

			info.AppendLine (" ");
			info.AppendLine ("-----Usage-----");
			info.AppendLine (" ");
			info.AppendLine ("To configure this Optical Transmitter as a sender you write its configuration tags that are explained below within a pair of brackets into its name.");
			info.AppendLine ("");
			info.AppendLine ("Example: Optical Power Transmitter (T:1, P:10, M:S)");
			info.AppendLine ("");
			info.AppendLine ("T: Defines the target ID of the Optical Transmitter you want to send power to. It has to be a positive number.");
			info.AppendLine ("");
			info.AppendLine ("P: Defines the amount of power in MW to send to the specified target.");
			info.AppendLine ("");
			info.AppendLine ("M: Defines the mode of the Transmitter, in this case it is set so (S)ender. If it is not a sender, it is a receiver by default.");
			info.AppendLine ("");
			info.AppendLine ("To configure this Optical Transmitter as a receiver you write its configuration tags that are explained below within a pair of brackets into its name.");
			info.AppendLine ("");
			info.AppendLine ("Example: Optical Power Transmitter (I:1)");
			info.AppendLine ("");
			info.AppendLine ("I: Defines the ID of this Optical Transmitter. It has to match with the target IDs of the senders to receive their power. It has to be a positive number.");
			info.AppendLine ("");
			info.AppendLine ("(Optional) M: Defines the mode of the Transmitter. By default an Optical Transmitter is in Receiver mode, so you do not have to define it.");
		}

		public override void MarkForClose ()
		{
			m_info.functionalBlock = null;
			if (TransmissionManager.opticalTransmitters.ContainsKey (m_entityId)) {
				TransmissionManager.opticalTransmitters.Remove (m_entityId);
			}

			base.MarkForClose ();
		}

		public override void Close ()
		{
			m_info.functionalBlock = null;
			if (TransmissionManager.opticalTransmitters.ContainsKey (m_entityId)) {
				TransmissionManager.opticalTransmitters.Remove (m_entityId);
			}

			if(TransmitterLogic.transmittersSaveFile.Transmitters.Contains(m_saveInfo)) 
			{
				TransmitterLogic.transmittersSaveFile.Transmitters.Remove(m_saveInfo);
			}

			base.Close ();
		}

		void onUpgradeValuesChanged ()
		{
			m_currentMaxPower = m_cubeBlock.UpgradeValues["OpticalPowerStrength"] * m_maxPower;

			parseName ((IMyTerminalBlock)m_functionalBlock);

			m_info.strength = m_currentMaxPower;

		}

		static void createUI()
		{
			if (m_controlsInit)
				return;

			m_controlsInit = true;

			MyAPIGateway.TerminalControls.CustomControlGetter -= customControlGetter;

			MyAPIGateway.TerminalControls.CustomControlGetter += customControlGetter;

			// sender/receiver switch
			m_controlSender = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyRefinery>("Cython.OPT.SenderReceiver");
			m_controlSender.Enabled = (b) => true;
			m_controlSender.Visible = (b) => b.BlockDefinition.SubtypeId.Equals("LargeBlockSmallOpticalPowerTransmitter") || b.BlockDefinition.SubtypeId.Equals("SmallBlockSmallOpticalPowerTransmitter");
			m_controlSender.Title = MyStringId.GetOrCompute("Mode");
			m_controlSender.Tooltip = MyStringId.GetOrCompute("Switches this transmitters mode to Sender or Receiver");
			m_controlSender.OnText = MyStringId.GetOrCompute("Send");
			m_controlSender.OffText = MyStringId.GetOrCompute("Rec.");
			m_controlSender.Getter = (b) => b.GameLogic.GetAs<OpticalPowerTransmitter>().m_sender;
			m_controlSender.Setter = (b, v) => {
				b.GameLogic.GetAs<OpticalPowerTransmitter>().m_sender = v;
				b.GameLogic.GetAs<OpticalPowerTransmitter>().m_info.sender = v;

				m_controlSender.UpdateVisual();
				m_controlPower.UpdateVisual();

				byte[] message = new byte[13];
				byte[] messageId = BitConverter.GetBytes(3);
				byte[] entityId = BitConverter.GetBytes(b.EntityId);

				for(int i = 0; i < 4; i++)
				{
					message[i] = messageId[i];
				}

				for(int i = 0; i < 8; i++)
				{
					message[i+4] = entityId[i];
				}

				message[12] = BitConverter.GetBytes(v)[0];


				MyAPIGateway.Multiplayer.SendMessageToOthers(5910, message, true);
			};
			MyAPIGateway.TerminalControls.AddControl<IMyRefinery>(m_controlSender);

			// channel field
			m_controlId = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyRefinery>("Cython.OPT.ID");
			m_controlId.Enabled = (b) => true;
			m_controlId.Visible = (b) => b.BlockDefinition.SubtypeId.Equals("LargeBlockSmallOpticalPowerTransmitter") || b.BlockDefinition.SubtypeId.Equals("SmallBlockSmallOpticalPowerTransmitter");
			m_controlId.Title = MyStringId.GetOrCompute("ID");
			m_controlId.Tooltip = MyStringId.GetOrCompute("ID this transmitter is being identified as when being receiver or it is supposed to send to.");
			m_controlId.Getter = (b) => {
			
				if(b.GameLogic.GetAs<OpticalPowerTransmitter>().m_sender)
				{
					return (new StringBuilder()).Append(b.GameLogic.GetAs<OpticalPowerTransmitter>().m_targetId);
				}
				else
				{
					return (new StringBuilder()).Append(b.GameLogic.GetAs<OpticalPowerTransmitter>().m_id);
				}
			};

			m_controlId.Setter = (b, s) => {

				uint id; 

				if(uint.TryParse(s.ToString(), out id)) 
				{
					var OPT = b.GameLogic.GetAs<OpticalPowerTransmitter>();
					if(OPT.m_sender)
					{

						OPT.m_targetId = id;
					}
					else
					{
						OPT.m_id = id;
						OPT.m_info.id = id;
					}

					byte[] message = new byte[16];
					byte[] messageId = BitConverter.GetBytes(4);
					byte[] entityId = BitConverter.GetBytes(b.EntityId);
					byte[] value = BitConverter.GetBytes(id);

					for(int i = 0; i < 4; i++)
					{
						message[i] = messageId[i];
					}

					for(int i = 0; i < 8; i++)
					{
						message[i+4] = entityId[i];
					}

					for(int i = 0; i < 4; i++)
					{
						message[i+12] = value[i];
					}

					MyAPIGateway.Multiplayer.SendMessageToOthers(5910, message, true);
				}
			};

			MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyRefinery>(m_controlId);


			// power field
			m_controlPower = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, Sandbox.ModAPI.Ingame.IMyRefinery>("Cython.OPT.Power");
			m_controlPower.Enabled = (b) => b.GameLogic.GetAs<OpticalPowerTransmitter>().m_sender;
			m_controlPower.Visible = (b) => b.BlockDefinition.SubtypeId.Equals("LargeBlockSmallOpticalPowerTransmitter") || b.BlockDefinition.SubtypeId.Equals("SmallBlockSmallOpticalPowerTransmitter");
			m_controlPower.Title = MyStringId.GetOrCompute("Power");
			m_controlPower.Tooltip = MyStringId.GetOrCompute("Maximum power this transmitter is supposed to send.");
			m_controlPower.Getter = (b) => (new StringBuilder()).Append(b.GameLogic.GetAs<OpticalPowerTransmitter>().m_transmittedPower);
			m_controlPower.Setter = (b, s) => {

				float power; 

				if(float.TryParse(s.ToString(), out power)) 
				{
					var OPT = b.GameLogic.GetAs<OpticalPowerTransmitter>();

					OPT.m_transmittedPower = power;

					if(OPT.m_transmittedPower > OPT.m_currentMaxPower) {
						OPT.m_transmittedPower = OPT.m_currentMaxPower;
					}

					byte[] message = new byte[16];
					byte[] messageId = BitConverter.GetBytes(5);
					byte[] entityId = BitConverter.GetBytes(b.EntityId);
					byte[] value = BitConverter.GetBytes(OPT.m_transmittedPower);

					for(int i = 0; i < 4; i++)
					{
						message[i] = messageId[i];
					}

					for(int i = 0; i < 8; i++)
					{
						message[i+4] = entityId[i];
					}

					for(int i = 0; i < 4; i++)
					{
						message[i+12] = value[i];
					}

					MyAPIGateway.Multiplayer.SendMessageToOthers(5910, message, true);
				}
			};

			MyAPIGateway.TerminalControls.AddControl<IMyRefinery>(m_controlPower);
		}

		static void customControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
		{
			List<IMyTerminalControl> toRemove = new List<IMyTerminalControl>();

			foreach(var control in controls)
			{
				if(block is IMyRefinery) 
				{
					if(block.BlockDefinition.SubtypeName.Equals("LargeBlockSmallOpticalPowerTransmitter") || block.BlockDefinition.SubtypeName.Equals("SmallBlockSmallOpticalPowerTransmitter"))
					{
						if(control.Id.Equals("UseConveyor"))
						{
							toRemove.Add(control);
						}
					}
				}

			}

			foreach(var control in toRemove)
			{
				controls.Remove(control);
			}
		}
	}


}

