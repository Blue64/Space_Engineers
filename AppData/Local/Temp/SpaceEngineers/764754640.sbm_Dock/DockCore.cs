using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using Draygo.API;
using VRageMath;
using VRage.Utils;

namespace DockingAssist
{
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class DockCore : MySessionComponentBase
	{
		public static DockCore instance;
		bool init = false;
		bool running = false;
		bool isServer = false;
		public bool isDedicated = false;
		public HudAPIv2 TextAPI;
		internal static Action OnDraw;
		internal bool DrawHud = false;
		private bool canDraw = false;
		private int countdown = 0;
		private bool ctrlpressed =	false;
		private bool shiftpressed = false;
		public int idx = 1;
		public EntityCache indexer = new EntityCache();
		public static readonly MyStringId WHITEDOT = MyStringId.GetOrCompute("WhiteDot");

		bool HudInit = false;
		HudAPIv2.BillBoardHUDMessage CenterDot;
		HudAPIv2.BillBoardHUDMessage TargetDot;
		HudAPIv2.BillBoardHUDMessage TrackingDot;
		HudAPIv2.BillBoardHUDMessage AlignmentDot;
		HudAPIv2.HUDMessage Notification;
		HudAPIv2.HUDMessage Instructions;
		HudAPIv2.HUDMessage Distance;

		static StringBuilder CanActivateHud = new StringBuilder("Press <color=teal>CTRL <color=white>to <color=lime>enable<color=white> Docking HUD");
		Vector2D V_CanActivateHud = Vector2D.Zero;
		static StringBuilder HudActivated = new StringBuilder("Press <color=teal>CTRL <color=white>to <color=red>disable<color=white> Docking HUD\nPress <color=lime>SHIFT<color=white> to select docking point.");
		Vector2D V_HudActivated = Vector2D.Zero;
		static StringBuilder RotateMessage = new StringBuilder("<color=purple>Rotate<color=teal> the ship so that the red dot is on top of the blue dot.\n");
		static StringBuilder TranslationMessage = new StringBuilder("<color=purple>Move<color=teal> the ship so that the yellow dot is on top of the blue dot.\n");
		static StringBuilder ApproachMessage = new StringBuilder("Approach Docking Clamp.\n");
		StringBuilder DistanceMessage = new StringBuilder();
		public bool IsInMenu
		{
			get
			{
				if (MyAPIGateway.Gui == null) return false;
				return (MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible);
			}
		}
		public bool CanDraw
		{
			get
			{
				return canDraw;
			}

			set
			{
				canDraw = value;
				if(value == true)
					countdown = 150;
			}
		}

		public override void UpdateAfterSimulation()
		{
			
			if (!init)
				return;
			if (isDedicated) return;
			InitHudElements();
			if (!HudInit)
				return;
			if (countdown <= 0)
			{
				CanDraw = false;
			}
			else
			{
				countdown--;
			}

			if (!CanDraw)
			{
				indexer.Clear();//remove
				Notification.Visible = false;
				Instructions.Visible = false;
				Distance.Visible = false;
				TargetDot.Visible = false;
				CenterDot.Visible = false;
				AlignmentDot.Visible = false;
				TrackingDot.Visible = false;
			}

			if (MyAPIGateway.Input != null && MyAPIGateway.Input.IsAnyCtrlKeyPressed())
			{
				if(!ctrlpressed && !IsInMenu)
					DrawHud = !DrawHud;
				ctrlpressed = true;

			}
			else
				ctrlpressed = false;
			if (MyAPIGateway.Input != null && MyAPIGateway.Input.IsAnyShiftKeyPressed())
			{
				if (!shiftpressed && !IsInMenu)
				{
					if (DrawHud)
						idx++;
				}
				shiftpressed = true;

			}
			else
				shiftpressed = false;
			if (DrawHud == false)
				idx = 1;


			Notification.Visible = false;
			if (DrawHud && !canDraw)
			{
				DrawHud = !DrawHud;

			}
			if (!DrawHud && canDraw)
			{

				SetCanActivateHud();

				//TextAPI.Send(new HUDTextNI.HUDMessage(40, 20, new Vector2D(-0.2, 0.95), 1,true, true, Color.Black, "Press <color=teal>CTRL <color=white>to <color=lime>enable<color=white> Docking HUD"));
            }
			if (DrawHud && canDraw)
			{

				SetHudActivated();

				//TextAPI.Send(new HUDTextNI.HUDMessage(40, 20, new Vector2D(-0.2, 0.95), 1, true, true, Color.Black, "Press <color=teal>CTRL <color=white>to <color=red>disable<color=white> Docking HUD\nPress <color=lime>SHIFT<color=white> to select docking point."));
			}

		}
		bool ActivateMessage = false;
		private void SetCanActivateHud()
		{
			Notification.Visible = true;
			Instructions.Visible = false;
			Distance.Visible = false;
			
			TargetDot.Visible = false;
			CenterDot.Visible = false;
			AlignmentDot.Visible = false;
			TrackingDot.Visible = false;
			if (!ActivateMessage)
			{
				Notification.Message = CanActivateHud;
				Instructions.Offset = new Vector2D(0, V_CanActivateHud.Y);
				ActivateMessage = true;
			}

		}


		private void SetHudActivated()
		{

			Notification.Visible = true;
			Instructions.Visible = true;
			Distance.Visible = true;
			
			TargetDot.Visible = true;
			CenterDot.Visible = true;
			AlignmentDot.Visible = true;
			//TrackingDot.Visible = true;
			if (ActivateMessage)
			{
				Notification.Message = HudActivated;
				Instructions.Offset = new Vector2D(0, V_HudActivated.Y);
				ActivateMessage = false;
			}

		}
		private void InitHudElements()
		{
			if (TextAPI == null) return;
			if (!TextAPI.Heartbeat)
				return;
			if (HudInit)
				return;
			if (Instructions == null)
			{
				Instructions = new HudAPIv2.HUDMessage();
				Instructions.Scale = 1.5d;
				Instructions.Visible = false;
			}
			if (Notification == null)
			{
				Notification = new HudAPIv2.HUDMessage();
				Notification.Visible = false;
				Notification.Scale = 1.5d;
				Notification.Origin = new Vector2D(-0.2, 1.0);
				Notification.Message = CanActivateHud;
				V_CanActivateHud = Notification.GetTextLength();
				Notification.Message = HudActivated;
				V_HudActivated = Notification.GetTextLength();
				Instructions.Origin = Notification.Origin;
				Instructions.Offset = new Vector2D(0, V_CanActivateHud.Y);

			}
			if (Distance == null)
			{
				Distance = new HudAPIv2.HUDMessage();
				Distance.Visible = false;
			}
			if (CenterDot == null)
			{
				CenterDot = new HudAPIv2.BillBoardHUDMessage(WHITEDOT, Vector2D.Zero, Color.Blue);
				CenterDot.Options |= HudAPIv2.Options.Shadowing | HudAPIv2.Options.Fixed;
				CenterDot.Scale = 0.0012d;
                CenterDot.Visible = false;
			}
			if (TargetDot == null)
			{
				TargetDot = new HudAPIv2.BillBoardHUDMessage(WHITEDOT, Vector2D.Zero, Color.Orange);
				TargetDot.Options |= HudAPIv2.Options.Shadowing | HudAPIv2.Options.Fixed;
				TargetDot.Scale = 0.0012d;
				TargetDot.Visible = false;
			}
			if (TrackingDot == null)
			{
				TrackingDot = new HudAPIv2.BillBoardHUDMessage(WHITEDOT, Vector2D.Zero, Color.White);
				TrackingDot.Options |= HudAPIv2.Options.Shadowing | HudAPIv2.Options.Fixed;
				TrackingDot.Scale = 0.0008d;
				TrackingDot.Visible = false;
			}
			if(AlignmentDot == null)
			{
				AlignmentDot = new HudAPIv2.BillBoardHUDMessage(WHITEDOT, Vector2D.Zero, Color.Red);
				AlignmentDot.Options |= HudAPIv2.Options.Shadowing | HudAPIv2.Options.Fixed;
				AlignmentDot.Scale = 0.0012d;
				AlignmentDot.Visible = false;
			}
			HudInit = true;
        }
		internal void SetDistanceMessage(string v)
		{
			if (!HudInit)
				return;
			DistanceMessage.Clear();
			Distance.Message = DistanceMessage;
			DistanceMessage.Append(v);
			//DockCore.instance.TextAPI.Send(new HUDTextNI.HUDMessage(3, 20, dotpos,1, true, true, Color.Black, )));
		}

		internal void SetApproachMessage()
		{
			if(HudInit)
			{
				Instructions.Message = ApproachMessage;
				Instructions.Visible = true;
			}
		}

		internal void HideArrow()
		{
			if (!HudInit)
				return;
			TrackingDot.Visible = false;
		}

		internal void SetTranslationMessage()
		{
			if (!HudInit)
				return;
			Instructions.Message = TranslationMessage;
			Instructions.Visible = true;

		}

		internal void SetAngleMessage()
		{
			if (!HudInit)
				return;
			Instructions.Message = RotateMessage;
			Instructions.Visible = true;
			
		}

		public override void Draw()
		{
			Update();
		}
		protected override void UnloadData()
		{
			Unload();
		}
		public void Close()
		{

		}
		public void Unload()
		{

			if (init && !isDedicated)
			{
				if (instance != null) TextAPI.Close();
				TextAPI.Close();
				init = false;
			}
			isServer = false;
			isDedicated = false;
			running = false;

		}
		public void Init()
		{
			if (init) return;//script already initialized, abort.


			instance = this;
			init = true;
			running = true;
			isServer = MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer;
			isDedicated = (MyAPIGateway.Utilities.IsDedicated && isServer);
			if (isDedicated) return;

			TextAPI = new HudAPIv2();

		}
		public void DrawCenterDot()
		{
			if(HudInit)
			{
				if (CenterDot != null && CenterDot.Visible != true)
					CenterDot.Visible = true;
			}

			//DrawDot(new Vector2D(0, 0), Color.Blue);
		}
		public enum DotObject
		{
			Center,
			Rotate,
			Tracker,
			Translate
		}

		public Vector2D DrawOtherDot(MatrixD _Matrix, Vector3D TargetPos, DotObject ScreenObject)
		{
			MatrixD ViewMatrix = MatrixD.Invert(_Matrix);
			MatrixD ViewProjectionMatrix = ViewMatrix * MyAPIGateway.Session.Camera.ProjectionMatrix;
			Vector3D Screenpos = Vector3D.Transform(TargetPos, ViewProjectionMatrix) * 3;
			var dotpos = new Vector2D(MathHelper.Clamp(Screenpos.Y, -0.98, 0.98), MathHelper.Clamp(Screenpos.X, -0.98, 0.98));
            DrawDot(dotpos, ScreenObject);
			return dotpos;
		}

		public void DrawDot(Vector2D Origin, DotObject ScreenObject)
		{
			if (!HudInit)
				return;
			switch (ScreenObject)
			{
				case DotObject.Center:
					CenterDot.Origin = Origin;
					return;
				case DotObject.Translate:
					TargetDot.Origin = Origin;
					return;
				case DotObject.Rotate:
					AlignmentDot.Origin = Origin;
					return;
				case DotObject.Tracker:
					TrackingDot.Origin = Origin;
					return;
			}


		}
		public void DrawArrow(Vector2D Origin)
		{

			Vector2D origin = Origin - Vector2D.Normalize(Origin) / 100;
			TrackingDot.Origin = origin;
			TrackingDot.Visible = true;
	
		}
		private void Update()
		{

			if (!init)
			{

				if (MyAPIGateway.Session == null)
					return;
				if (MyAPIGateway.Multiplayer == null && MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE)
					return;
				Init();

			}
			if (MyAPIGateway.Session == null)
			{
				Unload();
			}
			if (isDedicated) return;
			if (MyAPIGateway.Session?.Camera == null) return;
			if (OnDraw != null)
				OnDraw();
		}


	}
}
