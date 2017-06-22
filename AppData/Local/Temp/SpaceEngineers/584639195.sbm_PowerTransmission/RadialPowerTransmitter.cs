using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using Sandbox.Game.EntityComponents;
using System.Collections.Generic;
using VRage.ModAPI;
using System.Text;
using System;
using VRageMath;
using VRage.Game;
using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;

namespace Cython.PowerTransmission
{
	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Refinery), "LargeBlockSmallRadialPowerTransmitter", "SmallBlockSmallRadialPowerTransmitter")] 
	public class RadialPowerTransmitter: MyGameLogicComponent
	{

		class ReceiverInfo {

			public float powerToAdd = 0f;
			public float maximumPower = 0f;

			public ReceiverInfo(float powerToAdd, float maximumPower) {
				this.powerToAdd = powerToAdd;
				this.maximumPower = maximumPower;
			}
		}

		static bool m_controlsInit = false;

		static IMyTerminalControlOnOffSwitch m_controlSender = null;
		static IMyTerminalControlTextbox m_controlChannel = null;
		static IMyTerminalControlTextbox m_controlPower = null;


		public MyDefinitionId m_electricityDefinition;

		MyObjectBuilder_EntityBase m_objectBuilder;

		IMyFunctionalBlock m_functionalBlock;
		IMyCubeBlock m_cubeBlock;
		IMyTerminalBlock m_terminalBlock;



		int m_ticks = 0;

		long m_entityId;

		string m_subtypeName;

		public RadialPowerTransmitterInfo m_info = new RadialPowerTransmitterInfo();

		public PTInfo m_saveInfo;

		MyResourceSinkComponent m_resourceSink;

		float m_oldTransmittedPower = 0f;
		public float m_transmittedPower = 0f;

		float m_maxRange = 1.0f;
		float m_maxRangeSquared = 1.0f;

		float m_falloffRange = 0.00005f;

		float m_maxPower = 1.0f;
		float m_currentMaxPower = 1.0f;

		public uint m_channel = 0;

		public bool m_sender = false;

		float m_currentOutput = 0f;

		uint m_infoReceivers = 0;
		float m_infoReceivingPower = 0f;


		Dictionary<IMyFunctionalBlock, ReceiverInfo> m_receivers = new Dictionary<IMyFunctionalBlock, ReceiverInfo> ();

		public override void Init (MyObjectBuilder_EntityBase objectBuilder)
		{
			base.Init (objectBuilder);
			m_objectBuilder = objectBuilder;

			Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_FRAME;

			m_entityId = Entity.EntityId;

			m_functionalBlock = Entity as IMyFunctionalBlock;

			m_electricityDefinition = new MyDefinitionId (typeof(MyObjectBuilder_GasProperties), "Electricity");

			m_subtypeName = m_functionalBlock.BlockDefinition.SubtypeName;

			m_falloffRange = getRangeMultiplier (m_subtypeName);

			m_maxPower = getMaxPower (m_subtypeName);

			m_currentMaxPower = m_maxPower;

			m_info.functionalBlock = m_functionalBlock;

			m_info.subtypeName = m_subtypeName;

			m_cubeBlock = Entity as IMyCubeBlock;

			m_cubeBlock.AddUpgradeValue ("RadialPowerStrength", 1.0f);

			m_functionalBlock.CustomNameChanged += parseName;

			m_cubeBlock.OnUpgradeValuesChanged += onUpgradeValuesChanged;

			m_terminalBlock = Entity as IMyTerminalBlock;
			m_terminalBlock.AppendingCustomInfo += appendCustomInfo;
		}

		void onUpgradeValuesChanged ()
		{
			m_currentMaxPower = m_cubeBlock.UpgradeValues["RadialPowerStrength"] * m_maxPower;

			parseName ((IMyTerminalBlock)m_functionalBlock);

			m_info.strength = m_currentMaxPower;

		}

		public void appendCustomInfo(IMyTerminalBlock block, StringBuilder info)
		{
			info.Clear ();
			info.AppendLine (" ");
			info.AppendLine ("-----Radial Transmitter Info-----");
			info.AppendLine (" ");

			if (m_sender) {
				
				info.AppendLine ("(M)ode: Sender");

				info.AppendLine ("(C)hannel: " + m_channel);

				info.AppendLine ("(P)ower Sent: " + m_currentOutput + "MW / " + m_transmittedPower + "MW");

				info.AppendLine (" ");

				info.AppendLine ("Maximum Output: " + m_currentMaxPower.ToString("N") + "MW");

				info.AppendLine ("Range (10kW threshold): " + (Math.Sqrt(m_maxRangeSquared) / 1000d).ToString("N") + "KM");

				info.AppendLine ("Receivers in Range: " + m_receivers.Count);

			} else {
				
				info.AppendLine ("(M)ode: Receiver");

				info.AppendLine ("(C)hannel: " + m_channel);

				info.AppendLine (" ");

				info.AppendLine ("Power Receiving: " + m_infoReceivingPower.ToString("N") + "MW / " + m_currentMaxPower.ToString("N") + "MW");

				info.AppendLine ("Number of Sources: " + m_infoReceivers);

			}

			info.AppendLine (" ");
			info.AppendLine ("-----Usage-----");
			info.AppendLine (" ");
			info.AppendLine ("To configure this Radial Transmitter as a sender you write its configuration tags that are explained below within a pair of brackets into its name.");
			info.AppendLine ("");
			info.AppendLine ("Example: Radial Power Transmitter (C:1, P:10, M:S)");
			info.AppendLine ("");
			info.AppendLine ("C: Defines the channel to send power on. A receiver has to be set on that channel to receive power that is sent on it. It has to be a positive number.");
			info.AppendLine ("");
			info.AppendLine ("P: Defines the amount of power in MW to send on the specified channel.");
			info.AppendLine ("");
			info.AppendLine ("M: Defines the mode of the Transmitter, in this case it is set so (S)ender. If it is not a sender, it is a receiver by default.");
			info.AppendLine ("");
			info.AppendLine ("To configure this Radial Transmitter as a receiver you write its configuration tags that are explained below within a pair of brackets into its name.");
			info.AppendLine ("");
			info.AppendLine ("Example: Radial Power Transmitter (C:1)");
			info.AppendLine ("");
			info.AppendLine ("C: Defines the channel to receive power on. It has to match with the channel your senders send on to receive their power. It has to be a positive number.");
			info.AppendLine ("");
			info.AppendLine ("(Optional) M: Defines the mode of the Transmitter. By default a Radial Transmitter is in Receiver mode, so you do not have to define it.");


		}

		public override void UpdateOnceBeforeFrame ()
		{
			base.UpdateOnceBeforeFrame ();
			
			m_resourceSink = Entity.Components.Get<MyResourceSinkComponent> ();
			parseName ((IMyTerminalBlock)m_functionalBlock);

			m_info.strength = m_currentMaxPower;

			if(!MyAPIGateway.Multiplayer.IsServer)
				requestSettingsFromServer();

			createUI();

			m_saveInfo = new PTInfo(Entity.EntityId, m_sender, m_channel, m_transmittedPower, "R");

		}

		public override void OnRemovedFromScene ()
		{
	
			if (TransmissionManager.radialTransmitters.ContainsKey (m_entityId)) {
				TransmissionManager.radialTransmitters.Remove (m_entityId);
			}
			
			base.OnRemovedFromScene ();
		}

		public override void OnAddedToScene ()
		{
			base.OnAddedToScene ();

			m_entityId = Entity.EntityId;



			if (Entity.InScene) { 
				
				if (!TransmissionManager.radialTransmitters.ContainsKey (m_entityId)) {
					TransmissionManager.radialTransmitters.Add (m_entityId, m_info);
				}

				if(!TransmissionManager.totalPowerPerGrid.ContainsKey(m_functionalBlock.CubeGrid.EntityId)) {

					TransmissionManager.totalPowerPerGrid.Add (m_functionalBlock.CubeGrid.EntityId, 0);
				}
			}

		}

		void updatePowerInput()
		{
			if (!m_sender) {

				m_resourceSink.SetRequiredInputByType (m_electricityDefinition, 0);

			} else if (m_functionalBlock.Enabled && m_functionalBlock.IsFunctional) {

				m_resourceSink.SetRequiredInputByType (m_electricityDefinition, m_transmittedPower);

			} else {

				m_resourceSink.SetRequiredInputByType (m_electricityDefinition, 0);

			}
		}

		void requestSettingsFromServer()
		{
			byte[] message = new byte[20];
			byte[] messageId = BitConverter.GetBytes(10);
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

		public override void UpdateBeforeSimulation ()
		{

			base.UpdateBeforeSimulation();

			if (m_functionalBlock.IsFunctional) {

				if (m_functionalBlock.Enabled) {
					m_info.enabled = true;
				} else {
					m_info.enabled = false;
				}
				
			} else {
				m_info.enabled = false;
			}

			updatePowerInput();

			findReceivers ();

			calculateReceiverPower ();

			m_info.currentInput = 0;

			if(m_receivers.Count == 0)
			{
				m_currentOutput = 0;
				m_resourceSink.SetRequiredInputByType (m_electricityDefinition, 0);
			}

			m_oldTransmittedPower = m_transmittedPower;

			m_terminalBlock.RefreshCustomInfo ();

			m_infoReceivers = 0;
			m_infoReceivingPower = 0f;

			// writing newest values into the save file
			m_saveInfo.ChannelTarget = m_channel;
			m_saveInfo.Sender = m_sender;
			m_saveInfo.Power = m_transmittedPower;

			m_ticks++;
		}

		public override void UpdateAfterSimulation ()
		{
			bool contains = false;

			if(m_ticks == 1)
			{
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
					m_saveInfo = new PTInfo(Entity.EntityId, m_sender, m_channel, m_transmittedPower, "R");
					TransmitterLogic.transmittersSaveFile.Transmitters.Add(m_saveInfo);
				}
			}
		}

		public override void MarkForClose ()
		{
			
			if (TransmissionManager.radialTransmitters.ContainsKey (m_entityId)) {
				TransmissionManager.radialTransmitters.Remove (m_entityId);
			}

			base.MarkForClose ();
		}

		public override void Close ()
		{
			if (TransmissionManager.radialTransmitters.ContainsKey (m_entityId)) {
				TransmissionManager.radialTransmitters.Remove (m_entityId);
			}

			m_functionalBlock.CustomNameChanged -= parseName;

			m_cubeBlock.OnUpgradeValuesChanged -= onUpgradeValuesChanged;

			m_terminalBlock.AppendingCustomInfo -= appendCustomInfo;

			if(TransmitterLogic.transmittersSaveFile.Transmitters.Contains(m_saveInfo)) 
			{
				TransmitterLogic.transmittersSaveFile.Transmitters.Remove(m_saveInfo);
			}

			base.Close ();
		}

		void findReceivers() {

			m_receivers.Clear ();

			if (m_functionalBlock.IsFunctional && m_functionalBlock.Enabled) {
				
				m_currentOutput = m_resourceSink.CurrentInputByType (m_electricityDefinition);

				float falloffMultiplier = m_falloffRange;

				m_maxRangeSquared = (m_currentOutput * 1000f - 10f) / m_falloffRange;
				foreach (var radialTransmitter in TransmissionManager.radialTransmitters) {
					if (radialTransmitter.Key != m_entityId) {
						if (m_sender) {
							if (m_transmittedPower != 0f) {
								if (radialTransmitter.Value.channel == m_channel) {
									if (!radialTransmitter.Value.sender) {
										if (radialTransmitter.Value.enabled) {
											float distanceSquared = (float)Vector3D.DistanceSquared (m_functionalBlock.GetPosition (), radialTransmitter.Value.functionalBlock.GetPosition ());
											if(distanceSquared < m_maxRangeSquared) {


												float powerToTransfer = (m_currentOutput * 1000f - distanceSquared * falloffMultiplier)/1000f;

												if ((powerToTransfer + radialTransmitter.Value.currentInput) > radialTransmitter.Value.strength) {
													powerToTransfer = radialTransmitter.Value.strength - radialTransmitter.Value.currentInput;
												}

												radialTransmitter.Value.currentInput += powerToTransfer;

												m_receivers.Add (radialTransmitter.Value.functionalBlock, new ReceiverInfo(powerToTransfer, radialTransmitter.Value.strength));

											}


											
										}
									}
								}
							}
						}
					}
				}
			}
		}

		void calculateReceiverPower() {
			
			foreach (var receiver in m_receivers) {

				if (TransmissionManager.configuration.RadialFalloff) {

					float powerToAdd = receiver.Value.powerToAdd / m_receivers.Count;



					var transmitterComponent = receiver.Key.GameLogic.GetAs<RadialPowerTransmitter>();
					transmitterComponent.m_infoReceivers++;
					transmitterComponent.m_infoReceivingPower += powerToAdd;
					

					TransmissionManager.totalPowerPerGrid[receiver.Key.CubeGrid.EntityId] += powerToAdd;
				}

			}
		}

		float getRangeMultiplier(string subtypeName) {

			if(subtypeName == "LargeBlockSmallRadialPowerTransmitter") {
				return TransmissionManager.configuration.LargeBlockSmallRadialPowerTransmitter.RadialFalloffMultiplier;
			} else if(subtypeName == "SmallBlockSmallRadialPowerTransmitter") {
				return TransmissionManager.configuration.SmallBlockSmallRadialPowerTransmitter.RadialFalloffMultiplier;
			}

			return 1.0f;

		}

		float getMaxPower(string subtypeName) {

			if (TransmissionManager.configuration.UseMaximumPower) {
				
				if (subtypeName == "LargeBlockSmallRadialPowerTransmitter") {
					return TransmissionManager.configuration.LargeBlockSmallRadialPowerTransmitter.MaximumPower;
				} if (subtypeName == "SmallBlockSmallRadialPowerTransmitter") {
					return TransmissionManager.configuration.SmallBlockSmallRadialPowerTransmitter.MaximumPower;
				}

			} else {
				
				return float.PositiveInfinity;
			}

			return 1.0f;

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


								} catch (Exception e) {
									
								}

							} else {
								
								end = terminalBlock.CustomName.IndexOf (')', start + 2);

								if (end != -1) {
									
									try {
										
										m_transmittedPower = Convert.ToSingle (terminalBlock.CustomName.Substring (start + 2, end - (start + 2)));

										if(m_transmittedPower > m_currentMaxPower) {
											m_transmittedPower = m_currentMaxPower;
										}


									} catch (Exception e) {
										
									}
								}
							}
						}
					} else {
						m_transmittedPower = 0f;
					}

					start = terminalBlock.CustomName.IndexOf ("C:", settingsStart + 1);

					if (start != -1) {
						
						if ((start + 2) < (terminalBlock.CustomName.Length-1)) {
							
							int end = terminalBlock.CustomName.IndexOf (',', start + 2);

							if (end != -1) {
								
								try {
									
									m_channel = Convert.ToUInt32 (terminalBlock.CustomName.Substring (start + 2, end - (start + 2)));

									m_info.channel = m_channel;

								} catch (Exception e) {
									
								}
							} else {
								
								end = terminalBlock.CustomName.IndexOf (')', start + 2);

								if (end != -1) {
									
									try {
										
										m_channel = Convert.ToUInt32 (terminalBlock.CustomName.Substring (start + 2, end - (start + 2)));

										m_info.channel = m_channel;

									} catch (Exception e) {
										
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

								} catch (Exception e) {
									
								}
							} else {

								end = terminalBlock.CustomName.IndexOf (')', start + 2);

								if (end != -1) {

									try {

										m_sender = terminalBlock.CustomName.Substring (start + 2, end - (start + 2)) == "S";

										m_info.sender = m_sender;

									} catch (Exception e) {
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

		public override MyObjectBuilder_EntityBase GetObjectBuilder (bool copy = false)
		{
			return m_objectBuilder;
		}

		static void createUI()
		{
			if (m_controlsInit)
				return;

			m_controlsInit = true;

			MyAPIGateway.TerminalControls.CustomControlGetter -= customControlGetter;

			MyAPIGateway.TerminalControls.CustomControlGetter += customControlGetter;

			// sender/receiver switch
			m_controlSender = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyRefinery>("Cython.RPT.SenderReceiver");
			m_controlSender.Enabled = (b) => true;
			m_controlSender.Visible = (b) => b.BlockDefinition.SubtypeId.Equals("LargeBlockSmallRadialPowerTransmitter") || b.BlockDefinition.SubtypeId.Equals("SmallBlockSmallRadialPowerTransmitter");
			m_controlSender.Title = MyStringId.GetOrCompute("Mode");
			m_controlSender.Tooltip = MyStringId.GetOrCompute("Switches this transmitters mode to Sender or Receiver");
			m_controlSender.OnText = MyStringId.GetOrCompute("Send");
			m_controlSender.OffText = MyStringId.GetOrCompute("Rec.");
			m_controlSender.Getter = (b) => b.GameLogic.GetAs<RadialPowerTransmitter>().m_sender;
			m_controlSender.Setter = (b, v) => {
				b.GameLogic.GetAs<RadialPowerTransmitter>().m_sender = v;
				b.GameLogic.GetAs<RadialPowerTransmitter>().m_info.sender = v;

				m_controlSender.UpdateVisual();
				m_controlPower.UpdateVisual();

				byte[] message = new byte[13];
				byte[] messageId = BitConverter.GetBytes(0);
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
			m_controlChannel = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyRefinery>("Cython.RPT.Channel");
			m_controlChannel.Enabled = (b) => true;
			m_controlChannel.Visible = (b) => b.BlockDefinition.SubtypeId.Equals("LargeBlockSmallRadialPowerTransmitter") || b.BlockDefinition.SubtypeId.Equals("SmallBlockSmallRadialPowerTransmitter");
			m_controlChannel.Title = MyStringId.GetOrCompute("Channel");
			m_controlChannel.Tooltip = MyStringId.GetOrCompute("Channel this transmitter is supposed to send or receive on.");
			m_controlChannel.Getter = (b) => (new StringBuilder()).Append(b.GameLogic.GetAs<RadialPowerTransmitter>().m_channel);
			m_controlChannel.Setter = (b, s) => {

				uint channel; 

				if(uint.TryParse(s.ToString(), out channel)) 
				{
					var RPT = b.GameLogic.GetAs<RadialPowerTransmitter>();

					RPT.m_channel = channel;
					RPT.m_info.channel = channel;

					byte[] message = new byte[16];
					byte[] messageId = BitConverter.GetBytes(1);
					byte[] entityId = BitConverter.GetBytes(b.EntityId);
					byte[] value = BitConverter.GetBytes(channel);

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

			MyAPIGateway.TerminalControls.AddControl<IMyRefinery>(m_controlChannel);

			// power field
			m_controlPower = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyRefinery>("Cython.RPT.Power");
			m_controlPower.Enabled = (b) => b.GameLogic.GetAs<RadialPowerTransmitter>().m_sender;
			m_controlPower.Visible = (b) => b.BlockDefinition.SubtypeId.Equals("LargeBlockSmallRadialPowerTransmitter") || b.BlockDefinition.SubtypeId.Equals("SmallBlockSmallRadialPowerTransmitter");
			m_controlPower.Title = MyStringId.GetOrCompute("Power");
			m_controlPower.Tooltip = MyStringId.GetOrCompute("Maximum power this transmitter is supposed to send.");
			m_controlPower.Getter = (b) => (new StringBuilder()).Append(b.GameLogic.GetAs<RadialPowerTransmitter>().m_transmittedPower);
			m_controlPower.Setter = (b, s) => {

				float power; 

				if(float.TryParse(s.ToString(), out power)) 
				{
					var RPT = b.GameLogic.GetAs<RadialPowerTransmitter>();

					RPT.m_transmittedPower = power;

					if(RPT.m_transmittedPower > RPT.m_currentMaxPower) {
						RPT.m_transmittedPower = RPT.m_currentMaxPower;
					}

					byte[] message = new byte[16];
					byte[] messageId = BitConverter.GetBytes(2);
					byte[] entityId = BitConverter.GetBytes(b.EntityId);
					byte[] value = BitConverter.GetBytes(RPT.m_transmittedPower);

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
					if(block.BlockDefinition.SubtypeName.Equals("LargeBlockSmallRadialPowerTransmitter") || block.BlockDefinition.SubtypeName.Equals("SmallBlockSmallRadialPowerTransmitter"))
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

