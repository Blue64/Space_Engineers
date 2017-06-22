namespace SpaceEquipmentLtd.NanobotBuildAndRepairSystem
{
   using System;
   using System.Collections.Generic;
   using System.Text;
   using VRage.ModAPI;
   using VRage.Utils;
   using VRageMath;
   using Sandbox.ModAPI;
   using Sandbox.ModAPI.Interfaces.Terminal;
   using SpaceEquipmentLtd.Utils;

   [Flags]
   public enum SearchModes
   {
      /// <summary>
      /// Search Target blocks only inside connected blocks
      /// </summary>
      Grids = 0x0001,

      /// <summary>
      /// Search Target blocks in bounding boy independend of connection
      /// </summary>
      BoundingBox = 0x0002
   }

   [Flags]
   public enum WorkModes
   {
      /// <summary>
      /// Grind only if nothing to weld
      /// </summary>
      WeldBeforeGrind = 0x0001,

      /// <summary>
      /// Weld onyl if nothing to grind
      /// </summary>
      GrindBeforeWeld = 0x0002,

      /// <summary>
      /// Grind only if nothing to weld or
      /// build waiting for missing items
      /// </summary>
      GrindIfWeldGetStuck = 0x0004
   }

   public static class NanobotBuildAndRepairSystemTerminal
   {
      public static bool CustomControlsInit = false;
      private static List<IMyTerminalControl> CustomControls = new List<IMyTerminalControl>();
      private static List<IMyTerminalAction> CustomActions = new List<IMyTerminalAction>();

      private static IMyTerminalControlButton _EnableDisableButton;
      private static IMyTerminalControlButton _PriorityButtonUp;
      private static IMyTerminalControlButton _PriorityButtonDown;
      private static IMyTerminalControlListbox _PriorityListBox;

      /// <summary>
      /// Initialize custom control definition
      /// </summary>
      public static void InitializeControls()
      {
         lock (CustomControls)
         {
            if (CustomControlsInit) return;
            CustomControlsInit = true;
            try
            {
               // As CustomControlGetter is only called if the Terminal is opened, 
               // I add also some properties immediately and permanent to support scripting.
               // !! As we can't subtype here they will be also available in every Shipwelder but without function !!

               if (Mod.Log.ShouldLog(Logging.Level.Event)) Mod.Log.Write(Logging.Level.Event, "InitializeControls");

               MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
               MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionsGetter;

               IMyTerminalControlCheckbox checkbox;
               IMyTerminalControlCombobox comboBox;
               IMyTerminalControlSeparator separateArea;
               IMyTerminalControlSlider slider;

               // --- AllowBuild CheckBox
               if (!NanobotBuildAndRepairSystemMod.Settings.Welder.AllowBuildFixed)
               {
                  checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyShipWelder>("AllowBuild");
                  checkbox.Title = MyStringId.GetOrCompute("Build new");
                  checkbox.Tooltip = MyStringId.GetOrCompute("When checked, the BuildAndRepairSystem will also construct projected blocks.");
                  checkbox.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.AllowBuild : false;
                  };
                  checkbox.Setter = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        system.Settings.AllowBuild = y;
                        checkbox.UpdateVisual();
                     }
                  };
                  CreateCheckBoxAction("AllowBuild", checkbox);
                  CustomControls.Add(checkbox);
                  CreateProperty(checkbox);
               } 

               // --- Select search mode
               if ((NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes & (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes - 1)) != 0)
               {
                  comboBox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyShipWelder>("Mode");
                  comboBox.Title = MyStringId.GetOrCompute("Mode");
                  comboBox.Tooltip = MyStringId.GetOrCompute("Select how the nanobots search and reach their targets");
                  comboBox.Visible = (block) =>
                  {
                     return (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes & (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes - 1)) != 0;
                  };

                  comboBox.ComboBoxContent = (list) =>
                  {
                     if (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes.HasFlag(SearchModes.Grids))
                        list.Add(new MyTerminalControlComboBoxItem() { Key = (long)SearchModes.Grids, Value = MyStringId.GetOrCompute("Walk mode") });
                     if (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes.HasFlag(SearchModes.BoundingBox))
                        list.Add(new MyTerminalControlComboBoxItem() { Key = (long)SearchModes.BoundingBox, Value = MyStringId.GetOrCompute("Fly mode") });
                  };
                  comboBox.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system == null) return 0;
                     else return (long)system.Settings.SearchMode;
                  };
                  comboBox.Setter = (block, value) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        if (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedSearchModes.HasFlag((SearchModes)value))
                        {
                           system.Settings.SearchMode = (SearchModes)value;
                           comboBox.UpdateVisual();
                        }
                     }
                  };
                  CustomControls.Add(comboBox);
                  CreateProperty(comboBox);
               }

               // --- Select work mode
               if ((NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes & (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes - 1)) != 0)
               {
                  comboBox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyShipWelder>("Work Mode");
                  comboBox.Title = MyStringId.GetOrCompute("WorkMode");
                  comboBox.Tooltip = MyStringId.GetOrCompute("Select how the nanobots decide what to do (weld or grind)");
                  comboBox.Visible = (block) =>
                  {
                     return (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes & (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes - 1)) != 0;
                  };

                  comboBox.ComboBoxContent = (list) =>
                  {
                     if (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes.HasFlag(WorkModes.WeldBeforeGrind))
                        list.Add(new MyTerminalControlComboBoxItem() { Key = (long)WorkModes.WeldBeforeGrind, Value = MyStringId.GetOrCompute("Weld before grind") });
                     if (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes.HasFlag(WorkModes.GrindBeforeWeld))
                        list.Add(new MyTerminalControlComboBoxItem() { Key = (long)WorkModes.GrindBeforeWeld, Value = MyStringId.GetOrCompute("Grind before weld") });
                     if (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes.HasFlag(WorkModes.GrindIfWeldGetStuck))
                        list.Add(new MyTerminalControlComboBoxItem() { Key = (long)WorkModes.GrindIfWeldGetStuck, Value = MyStringId.GetOrCompute("Grind if weld get stuck") });
                  };
                  comboBox.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system == null) return 0;
                     else return (long)system.Settings.WorkMode;
                  };
                  comboBox.Setter = (block, value) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        if (NanobotBuildAndRepairSystemMod.Settings.Welder.AllowedWorkModes.HasFlag((WorkModes)value))
                        {
                           system.Settings.WorkMode = (WorkModes)value;
                           comboBox.UpdateVisual();
                        }
                     }
                  };
                  CustomControls.Add(comboBox);
                  CreateProperty(comboBox);
               }

               // --- Set Color that marks blocks as 'ignore'
               if (!NanobotBuildAndRepairSystemMod.Settings.Welder.UseIgnoreColorFixed)
               {
                  separateArea = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyShipWelder>("SeparateIgnoreColor");
                  CustomControls.Add(separateArea);

                  checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyShipWelder>("UseIgnoreColor");
                  checkbox.Title = MyStringId.GetOrCompute("Use Ignore Color");
                  checkbox.Tooltip = MyStringId.GetOrCompute("When checked, the system will ignore blocks with the color defined further down.");
                  checkbox.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.UseIgnoreColor : false;
                  };
                  checkbox.Setter = (block, value) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        system.Settings.UseIgnoreColor = value;
                        foreach (var ctrl in CustomControls)
                        {
                           if (ctrl.Id.Contains("IgnoreColor")) ctrl.UpdateVisual();
                        }
                     }
                  };
                  CreateCheckBoxAction("UseIgnoreColor", checkbox);
                  CustomControls.Add(checkbox);
                  CreateProperty(checkbox);

                  Func<IMyTerminalBlock, bool> colorPickerEnabled = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.UseIgnoreColor : false;
                  };

                  slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("IgnoreColorHue");
                  slider.Title = MyStringId.GetOrCompute("Hue");
                  slider.SetLimits(0, 360);
                  slider.Enabled = colorPickerEnabled;
                  slider.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.IgnoreColor.X * 360f : 0;
                  };
                  slider.Setter = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.IgnoreColor;
                        y = y < 0 ? 0 : y > 360 ? 360 : y;
                        hsv.X = (float)Math.Round(y) / 360;
                        system.Settings.IgnoreColor = hsv;
                        slider.UpdateVisual();
                     }
                  };
                  slider.Writer = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.IgnoreColor;
                        y.Append(Math.Round(hsv.X * 360f));
                     }
                  };
                  CustomControls.Add(slider);
                  CreateSliderActions("IgnoreColorHue", slider, 0, 360);

                  slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("IgnoreColorSaturation");
                  slider.Title = MyStringId.GetOrCompute("Saturation");
                  slider.SetLimits(-100, 100);
                  slider.Enabled = colorPickerEnabled;
                  slider.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.IgnoreColor.Y * 100f : 0;
                  };
                  slider.Setter = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.IgnoreColor;
                        y = y < -100 ? -100 : y > 100 ? 100 : y;
                        hsv.Y = (float)Math.Round(y) / 100f;
                        system.Settings.IgnoreColor = hsv;
                        slider.UpdateVisual();
                     }
                  };
                  slider.Writer = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.IgnoreColor;
                        y.Append(Math.Round(hsv.Y * 100f));
                     }
                  };
                  CustomControls.Add(slider);
                  CreateSliderActions("IgnoreColorSaturation", slider, -100, 100);

                  slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("IgnoreColorValue");
                  slider.Title = MyStringId.GetOrCompute("Value");
                  slider.SetLimits(-100, 100);
                  slider.Enabled = colorPickerEnabled;
                  slider.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.IgnoreColor.Z * 100f : 0;
                  };
                  slider.Setter = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.IgnoreColor;
                        y = y < -100 ? -100 : y > 100 ? 100 : y;
                        hsv.Z = (float)Math.Round(y) / 100f;
                        system.Settings.IgnoreColor = hsv;
                        slider.UpdateVisual();
                     }
                  };
                  slider.Writer = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.IgnoreColor;
                        y.Append(Math.Round(hsv.Z * 100f));
                     }
                  };
                  CustomControls.Add(slider);
                  CreateSliderActions("ColorValue", slider, -100, 100);

                  var propertyIC = MyAPIGateway.TerminalControls.CreateProperty<Vector3, IMyShipWelder>("BuildAndRepair.IgnoreColor");
                  propertyIC.SupportsMultipleBlocks = false;
                  propertyIC.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.IgnoreColor : Vector3.Zero;
                  };
                  propertyIC.Setter = (block, value) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        if (value.X < 0f) value.X = 0f;
                        if (value.X > 1f) value.X = 1f;
                        if (value.Y < -1f) value.X = -1f;
                        if (value.Y > 1f) value.X = 1f;
                        if (value.Z < -1f) value.X = -1f;
                        if (value.Z > 1f) value.X = 1f;
                        system.Settings.IgnoreColor = value;
                     }
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertyIC);
               }

               // --- Set Color that marks blocks as 'grind'
               if (!NanobotBuildAndRepairSystemMod.Settings.Welder.UseGrindColorFixed)
               {
                  separateArea = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyShipWelder>("SeparateGrindColor");
                  CustomControls.Add(separateArea);

                  checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyShipWelder>("UseGrindColor");
                  checkbox.Title = MyStringId.GetOrCompute("Use Grind Color");
                  checkbox.Tooltip = MyStringId.GetOrCompute("When checked, the system will grind blocks with the color defined further down.");
                  checkbox.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.UseGrindColor : false;
                  };
                  checkbox.Setter = (block, value) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        system.Settings.UseGrindColor = value;
                        foreach (var ctrl in CustomControls)
                        {
                           if (ctrl.Id.Contains("GrindColor")) ctrl.UpdateVisual();
                        }
                     }
                  };
                  CreateCheckBoxAction("UseGrindColor", checkbox);
                  CustomControls.Add(checkbox);
                  CreateProperty(checkbox);

                  Func<IMyTerminalBlock, bool> colorPickerEnabled = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.UseGrindColor : false;
                  };

                  slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("GrindColorHue");
                  slider.Title = MyStringId.GetOrCompute("Hue");
                  slider.SetLimits(0, 360);
                  slider.Enabled = colorPickerEnabled;
                  slider.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.GrindColor.X * 360f : 0;
                  };
                  slider.Setter = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.GrindColor;
                        y = y < 0 ? 0 : y > 360 ? 360 : y;
                        hsv.X = (float)Math.Round(y) / 360;
                        system.Settings.GrindColor = hsv;
                        slider.UpdateVisual();
                     }
                  };
                  slider.Writer = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.GrindColor;
                        y.Append(Math.Round(hsv.X * 360f));
                     }
                  };
                  CustomControls.Add(slider);
                  CreateSliderActions("GrindColorHue", slider, 0, 360);

                  slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("GrindColorSaturation");
                  slider.Title = MyStringId.GetOrCompute("Saturation");
                  slider.SetLimits(-100, 100);
                  slider.Enabled = colorPickerEnabled;
                  slider.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.GrindColor.Y * 100f : 0;
                  };
                  slider.Setter = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.GrindColor;
                        y = y < -100 ? -100 : y > 100 ? 100 : y;
                        hsv.Y = (float)Math.Round(y) / 100f;
                        system.Settings.GrindColor = hsv;
                        slider.UpdateVisual();
                     }
                  };
                  slider.Writer = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.GrindColor;
                        y.Append(Math.Round(hsv.Y * 100f));
                     }
                  };
                  CustomControls.Add(slider);
                  CreateSliderActions("GrindColorSaturation", slider, -100, 100);

                  slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("GrindColorValue");
                  slider.Title = MyStringId.GetOrCompute("Value");
                  slider.SetLimits(-100, 100);
                  slider.Enabled = colorPickerEnabled;
                  slider.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.GrindColor.Z * 100f : 0;
                  };
                  slider.Setter = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.GrindColor;
                        y = y < -100 ? -100 : y > 100 ? 100 : y;
                        hsv.Z = (float)Math.Round(y) / 100f;
                        system.Settings.GrindColor = hsv;
                        slider.UpdateVisual();
                     }
                  };
                  slider.Writer = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var hsv = system.Settings.GrindColor;
                        y.Append(Math.Round(hsv.Z * 100f));
                     }
                  };
                  CustomControls.Add(slider);
                  CreateSliderActions("ColorValue", slider, -100, 100);

                  var propertyGC = MyAPIGateway.TerminalControls.CreateProperty<Vector3, IMyShipWelder>("BuildAndRepair.GrindColor");
                  propertyGC.SupportsMultipleBlocks = false;
                  propertyGC.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.GrindColor : Vector3.Zero;
                  };
                  propertyGC.Setter = (block, value) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        if (value.X < 0f) value.X = 0f;
                        if (value.X > 1f) value.X = 1f;
                        if (value.Y < -1f) value.X = -1f;
                        if (value.Y > 1f) value.X = 1f;
                        if (value.Z < -1f) value.X = -1f;
                        if (value.Z > 1f) value.X = 1f;
                        system.Settings.GrindColor = value;
                     }
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertyGC);
               }

               // -- Highlight Area
               if (!NanobotBuildAndRepairSystemMod.Settings.Welder.ShowAreaFixed || !NanobotBuildAndRepairSystemMod.Settings.Welder.AreaSizeFixed)
               {
                  separateArea = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyShipWelder>("SeparateArea");
                  CustomControls.Add(separateArea);

                  Func<IMyTerminalBlock, float> getLimitMin = (block) => NanobotBuildAndRepairSystemBlock.WELDER_RANGE_MIN_IN_M;
                  Func<IMyTerminalBlock, float> getLimitMax = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.WelderMaximumRange : NanobotBuildAndRepairSystemBlock.WELDER_RANGE_MIN_IN_M;
                  };

                  if (!NanobotBuildAndRepairSystemMod.Settings.Welder.ShowAreaFixed)
                  {
                     checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyShipWelder>("ShowArea");
                     checkbox.Title = MyStringId.GetOrCompute("Show Area");
                     checkbox.Tooltip = MyStringId.GetOrCompute("When checked, it will show you the area this system covers");
                     checkbox.Getter = (block) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           return system.Settings.ShowArea;
                        }

                        return false;
                     };
                     checkbox.Setter = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           system.Settings.ShowArea = y;
                           checkbox.UpdateVisual();
                        }
                     };
                     CustomControls.Add(checkbox);
                     CreateProperty(checkbox);
                  }

                  if (!NanobotBuildAndRepairSystemMod.Settings.Welder.AreaSizeFixed)
                  {
                     slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("AreaWidthLeft");
                     slider.Title = MyStringId.GetOrCompute("Area Width Left");
                     slider.SetLimits(getLimitMin, getLimitMax);
                     slider.Getter = (block) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        return system != null ? system.Settings.AreaWidthLeft : 0;
                     };
                     slider.Setter = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           var min = getLimitMin(block);
                           var max = getLimitMax(block);
                           y = y < min ? min : y > max ? max : y;
                           system.Settings.AreaWidthLeft = (int)y;
                           slider.UpdateVisual();
                        }
                     };
                     slider.Writer = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           y.Append(system.Settings.AreaWidthLeft + " m");
                        }
                     };
                     CustomControls.Add(slider);
                     CreateSliderActionsArea("AreaWidthLeft", slider);
                     CreateProperty(slider);

                     slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("AreaWidthRight");
                     slider.Title = MyStringId.GetOrCompute("Area Width Right");
                     slider.SetLimits(getLimitMin, getLimitMax);
                     slider.Getter = (block) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        return system != null ? system.Settings.AreaWidthRight : 0;
                     };
                     slider.Setter = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           var min = getLimitMin(block);
                           var max = getLimitMax(block);
                           y = y < min ? min : y > max ? max : y;
                           system.Settings.AreaWidthRight = (int)y;
                           slider.UpdateVisual();
                        }
                     };
                     slider.Writer = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           y.Append(system.Settings.AreaWidthRight + " m");
                        }
                     };
                     CustomControls.Add(slider);
                     CreateSliderActionsArea("AreaWidthRight", slider);
                     CreateProperty(slider);

                     slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("AreaHeightBottom");
                     slider.Title = MyStringId.GetOrCompute("Area Height Bottom");
                     slider.SetLimits(getLimitMin, getLimitMax);
                     slider.Getter = (block) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        return system != null ? system.Settings.AreaHeightBottom : 0;
                     };
                     slider.Setter = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           var min = getLimitMin(block);
                           var max = getLimitMax(block);
                           y = y < min ? min : y > max ? max : y;
                           system.Settings.AreaHeightBottom = (int)y;
                           slider.UpdateVisual();
                        }
                     };
                     slider.Writer = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           y.Append(system.Settings.AreaHeightBottom + " m");
                        }
                     };
                     CustomControls.Add(slider);
                     CreateSliderActionsArea("AreaHeightBottom", slider);
                     CreateProperty(slider);

                     slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("AreaHeightTop");
                     slider.Title = MyStringId.GetOrCompute("Area Height Top");
                     slider.SetLimits(getLimitMin, getLimitMax);
                     slider.Getter = (block) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        return system != null ? system.Settings.AreaHeightTop : 0;
                     };
                     slider.Setter = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           var min = getLimitMin(block);
                           var max = getLimitMax(block);
                           y = y < min ? min : y > max ? max : y;
                           system.Settings.AreaHeightTop = (int)y;
                           slider.UpdateVisual();
                        }
                     };
                     slider.Writer = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           y.Append(system.Settings.AreaHeightTop + " m");
                        }
                     };
                     CustomControls.Add(slider);
                     CreateSliderActionsArea("AreaHeightTop", slider);
                     CreateProperty(slider);

                     slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("AreaDepthRear");
                     slider.Title = MyStringId.GetOrCompute("Area Depth Rear");
                     slider.SetLimits(getLimitMin, getLimitMax);
                     slider.Getter = (block) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        return system != null ? system.Settings.AreaDepthRear : 0;
                     };
                     slider.Setter = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           var min = getLimitMin(block);
                           var max = getLimitMax(block);
                           y = y < min ? min : y > max ? max : y;
                           system.Settings.AreaDepthRear = (int)y;
                           slider.UpdateVisual();
                        }
                     };
                     slider.Writer = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           y.Append(system.Settings.AreaDepthRear + " m");
                        }
                     };
                     CustomControls.Add(slider);
                     CreateSliderActionsArea("AreaDepthRear", slider);
                     CreateProperty(slider);

                     slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("AreaDepthFront");
                     slider.Title = MyStringId.GetOrCompute("Area Depth Front");
                     slider.SetLimits(getLimitMin, getLimitMax);
                     slider.Getter = (block) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        return system != null ? system.Settings.AreaDepthFront : 0;
                     };
                     slider.Setter = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           var min = getLimitMin(block);
                           var max = getLimitMax(block);
                           y = y < min ? min : y > max ? max : y;
                           system.Settings.AreaDepthFront = (int)y;
                           slider.UpdateVisual();
                        }
                     };
                     slider.Writer = (block, y) =>
                     {
                        var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                        if (system != null)
                        {
                           y.Append(system.Settings.AreaDepthFront + " m");
                        }
                     };
                     CustomControls.Add(slider);
                     CreateSliderActionsArea("AreaDepthFront", slider);
                     CreateProperty(slider);
                  }
               }

               // -- Priority
               if (!NanobotBuildAndRepairSystemMod.Settings.Welder.PriorityFixed)
               {
                  separateArea = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyShipWelder>("SeparatePrio");
                  CustomControls.Add(separateArea);

                  var textbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyShipWelder>("Priority");
                  textbox.Label = MyStringId.GetOrCompute("Build-Repair Priority");
                  CustomControls.Add(textbox);

                  var button = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipWelder>("EnableDisable");
                  _EnableDisableButton = button;
                  button.Title = MyStringId.GetOrCompute("Enable/Disable");
                  button.Enabled = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.BuildPriority.Selected != null : false;
                  };
                  button.Action = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        system.BuildPriority.ToggleEnabled();
                        system.Settings.BuildPriority = system.BuildPriority.GetEntries();
                        _PriorityListBox.UpdateVisual();
                     }
                  };
                  CustomControls.Add(button);

                  button = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipWelder>("PriorityUp");
                  _PriorityButtonUp = button;
                  button.Title = MyStringId.GetOrCompute("Priority Up");
                  button.Enabled = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.BuildPriority.Selected != null : false;
                  };
                  button.Action = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        system.BuildPriority.MoveSelectedUp();
                        system.Settings.BuildPriority = system.BuildPriority.GetEntries();
                        _PriorityListBox.UpdateVisual();
                     }
                  };
                  CustomControls.Add(button);

                  button = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipWelder>("PriorityDown");
                  _PriorityButtonDown = button;
                  button.Title = MyStringId.GetOrCompute("Priority Down");
                  button.Enabled = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.BuildPriority.Selected != null : false;
                  };
                  button.Action = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        system.BuildPriority.MoveSelectedDown();
                        system.Settings.BuildPriority = system.BuildPriority.GetEntries();
                        _PriorityListBox.UpdateVisual();
                     }
                  };
                  CustomControls.Add(button);

                  var listbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyShipWelder>("Priority");
                  _PriorityListBox = listbox;

                  listbox.Multiselect = false;
                  listbox.VisibleRowsCount = 15;
                  listbox.ItemSelected = (block, selected) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        if (selected.Count > 0)
                        {
                           system.BuildPriority.Selected = (BlockClass)selected[0].UserData;
                        }
                        else system.BuildPriority.Selected = null;
                        _EnableDisableButton.UpdateVisual();
                        _PriorityButtonUp.UpdateVisual();
                        _PriorityButtonDown.UpdateVisual();
                     }
                  };
                  listbox.ListContent = (block, items, selected) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        system.BuildPriority.FillTerminalList(items, selected);
                     }
                  };
                  CustomControls.Add(listbox);
               }

               // -- Sound enabled
               if (!NanobotBuildAndRepairSystemMod.Settings.Welder.SoundVolumeFixed)
               {
                  separateArea = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyShipWelder>("SeparateOther");
                  CustomControls.Add(separateArea);

                  slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyShipWelder>("SoundVolume");
                  slider.Title = MyStringId.GetOrCompute("Sound Volume");
                  slider.SetLimits(0f, 100f);
                  slider.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? 100f * system.Settings.SoundVolume / NanobotBuildAndRepairSystemBlock.WELDER_SOUND_VOLUME : 0f;
                  };
                  slider.Setter = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        var min = 0;
                        var max = 100;
                        y = y < min ? min : y > max ? max : y;
                        system.Settings.SoundVolume = (float)Math.Round(y * NanobotBuildAndRepairSystemBlock.WELDER_SOUND_VOLUME) / 100f;
                        slider.UpdateVisual();
                     }
                  };
                  slider.Writer = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        y.Append(Math.Round(100f * system.Settings.SoundVolume / NanobotBuildAndRepairSystemBlock.WELDER_SOUND_VOLUME) + " %");
                     }
                  };
                  CustomControls.Add(slider);
                  CreateSliderActionsArea("SoundVolume", slider);
                  CreateProperty(slider);
               }

               // -- Script Control
               if (!NanobotBuildAndRepairSystemMod.Settings.Welder.ScriptControllFixed)
               {
                  separateArea = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyShipWelder>("SeparateScriptControl");
                  CustomControls.Add(separateArea);

                  checkbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyShipWelder>("ScriptControlled");
                  checkbox.Title = MyStringId.GetOrCompute("Controlled by Script");
                  checkbox.Tooltip = MyStringId.GetOrCompute("When checked, the system will not build/repair blocks automatically. Each block has to be picked by calling scripting functions.");
                  checkbox.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.ScriptControlled : false;
                  };
                  checkbox.Setter = (block, y) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        system.Settings.ScriptControlled = y;
                        checkbox.UpdateVisual();
                     }
                  };
                  CreateCheckBoxAction("ScriptControlled", checkbox);
                  CustomControls.Add(checkbox);
                  CreateProperty(checkbox);

                  //Scripting support for Priority and enabling BlockClasses
                  var propertyBlockClassList = MyAPIGateway.TerminalControls.CreateProperty<List<string>, IMyShipWelder>("BuildAndRepair.BlockClassList");
                  propertyBlockClassList.SupportsMultipleBlocks = false;
                  propertyBlockClassList.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.BuildPriority.GetList() : null;
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertyBlockClassList);

                  var propertySP = MyAPIGateway.TerminalControls.CreateProperty<Action<int, int>, IMyShipWelder>("BuildAndRepair.SetPriority");
                  propertySP.SupportsMultipleBlocks = false;
                  propertySP.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        return system.BuildPriority.SetPriority;
                     }
                     return null;
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertySP);

                  var propertyGP = MyAPIGateway.TerminalControls.CreateProperty<Func<int, int>, IMyShipWelder>("BuildAndRepair.GetPriority");
                  propertyGP.SupportsMultipleBlocks = false;
                  propertyGP.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        return system.BuildPriority.GetPriority;
                     }
                     return null;
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertyGP);

                  var propertySE = MyAPIGateway.TerminalControls.CreateProperty<Action<int, bool>, IMyShipWelder>("BuildAndRepair.SetEnabled");
                  propertySE.SupportsMultipleBlocks = false;
                  propertySE.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        return system.BuildPriority.SetEnabled;
                     }
                     return null;
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertySE);

                  var propertyGE = MyAPIGateway.TerminalControls.CreateProperty<Func<int, bool>, IMyShipWelder>("BuildAndRepair.GetEnabled");
                  propertyGE.SupportsMultipleBlocks = false;
                  propertyGE.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        return system.BuildPriority.GetEnabled;
                     }
                     return null;
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertyGE);

                  var propertyMissingComponentsDict = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<VRage.Game.MyDefinitionId, int>, IMyShipWelder>("BuildAndRepair.MissingComponents");
                  propertyMissingComponentsDict.SupportsMultipleBlocks = false;
                  propertyMissingComponentsDict.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.GetMissingComponentsDict() : null;
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertyMissingComponentsDict);

                  var propertyPossibleWeldTargetsList = MyAPIGateway.TerminalControls.CreateProperty<List<VRage.Game.ModAPI.Ingame.IMySlimBlock>, IMyShipWelder>("BuildAndRepair.PossibleTargets");
                  propertyPossibleWeldTargetsList.SupportsMultipleBlocks = false;
                  propertyPossibleWeldTargetsList.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.GetPossibleWeldTargetsList() : null;
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertyPossibleWeldTargetsList);

                  var propertyCPT = MyAPIGateway.TerminalControls.CreateProperty<VRage.Game.ModAPI.Ingame.IMySlimBlock, IMyShipWelder>("BuildAndRepair.CurrentPickedTarget");
                  propertyCPT.SupportsMultipleBlocks = true;
                  propertyCPT.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.Settings.CurrentPickedWeldingBlock : null;
                  };
                  propertyCPT.Setter = (block, value) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     if (system != null)
                     {
                        system.Settings.CurrentPickedWeldingBlock = value;
                     }
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertyCPT);

                  var propertyCT = MyAPIGateway.TerminalControls.CreateProperty<VRage.Game.ModAPI.Ingame.IMySlimBlock, IMyShipWelder>("BuildAndRepair.CurrentTarget");
                  propertyCT.SupportsMultipleBlocks = false;
                  propertyCT.Getter = (block) =>
                  {
                     var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
                     return system != null ? system.State.CurrentWeldingBlock : null;
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertyCT);

                  //Publish functions to scripting
                  var propertyPEQ = MyAPIGateway.TerminalControls.CreateProperty<Func<IEnumerable<long>, VRage.Game.MyDefinitionId, int, int>, IMyShipWelder>("BuildAndRepair.ProductionBlock.EnsureQueued");
                  propertyPEQ.SupportsMultipleBlocks = false;
                  propertyPEQ.Getter = (block) =>
                  {
                     return UtilsProductionBlock.EnsureQueued;
                  };
                  MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(propertyPEQ);
               }
            }
            catch (Exception ex)
            {
               Mod.Log.Write(Logging.Level.Error, "BuildAndRepairSystemBlock {0}: InitializeControls exception: {1}", ex);
            }
         }
      }

      /// <summary>
      /// 
      /// </summary>
      private static void CreateCheckBoxAction(string name, IMyTerminalControlCheckbox checkbox)
      {
         var action = MyAPIGateway.TerminalControls.CreateAction<IMyShipWelder>(string.Format("{0}OnOff", name));
         action.Name = new StringBuilder(string.Format("{0} On/Off", name));
         action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
         action.Enabled = (block) => true;
         action.Action = (block) =>
         {
            checkbox.Setter(block, !checkbox.Getter(block));
         };
         CustomActions.Add(action);
      }

      /// <summary>
      /// 
      /// </summary>
      private static void CreateSliderActions(string sliderName, IMyTerminalControlSlider slider, int minValue, int maxValue)
      {
         var action = MyAPIGateway.TerminalControls.CreateAction<IMyShipWelder>(string.Format("{0}_Increase", sliderName));
         action.Name = new StringBuilder(string.Format("{0} Increase", sliderName));
         action.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
         action.Enabled = (block) => true;
         action.Action = (block) =>
         {
            var val = slider.Getter(block);
            if (val < maxValue)
               slider.Setter(block, val + 1);
         };
         CustomActions.Add(action);

         action = MyAPIGateway.TerminalControls.CreateAction<IMyShipWelder>(string.Format("{0}_Decrease", sliderName));
         action.Name = new StringBuilder(string.Format("{0} Decrease", sliderName));
         action.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
         action.Enabled = (block) => true;
         action.Action = (block) =>
         {
            var val = slider.Getter(block);
            if (val > minValue)
               slider.Setter(block, val - 1);
         };
         CustomActions.Add(action);
      }

      /// <summary>
      /// 
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="control"></param>
      private static void CreateProperty<T>(IMyTerminalValueControl<T> control)
      {
         var property = MyAPIGateway.TerminalControls.CreateProperty<T, IMyShipWelder>("BuildAndRepair." + control.Id);
         property.SupportsMultipleBlocks = false;
         property.Getter = control.Getter;
         property.Setter = control.Setter;
         MyAPIGateway.TerminalControls.AddControl<IMyShipWelder>(property);
      }

      /// <summary>
      /// 
      /// </summary>
      private static void CreateSliderActionsArea(string sliderName, IMyTerminalControlSlider slider)
      {
         var action = MyAPIGateway.TerminalControls.CreateAction<IMyShipWelder>(string.Format("{0}_Increase", sliderName));
         action.Name = new StringBuilder(string.Format("{0} Increase", sliderName));
         action.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
         action.Enabled = (block) => true;
         action.Action = (block) =>
         {
            var system = block.GameLogic.GetAs<NanobotBuildAndRepairSystemBlock>();
            var max = system != null ? system.WelderMaximumRange : NanobotBuildAndRepairSystemBlock.WELDER_RANGE_MIN_IN_M;
            var val = slider.Getter(block);
            if (val < max)
               slider.Setter(block, val + 1);
         };
         CustomActions.Add(action);

         action = MyAPIGateway.TerminalControls.CreateAction<IMyShipWelder>(string.Format("{0}_Decrease", sliderName));
         action.Name = new StringBuilder(string.Format("{0} Decrease", sliderName));
         action.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
         action.Enabled = (block) => true;
         action.Action = (block) =>
         {
            var min = NanobotBuildAndRepairSystemBlock.WELDER_RANGE_MIN_IN_M;
            var val = slider.Getter(block);
            if (val > min)
               slider.Setter(block, val - 1);
         };
         CustomActions.Add(action);
      }

      /// <summary>
      /// Callback to add custom controls
      /// </summary>
      private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
      {
         if (block.BlockDefinition.SubtypeName.StartsWith("SELtd") && block.BlockDefinition.SubtypeName.Contains("NanobotBuildAndRepairSystem"))
         {
            foreach (var item in CustomControls)
               controls.Add(item);
         }
      }

      /// <summary>
      /// Callback to add custom actions
      /// </summary>
      private static void CustomActionsGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
      {
         if (block.BlockDefinition.SubtypeName.StartsWith("SELtd") && block.BlockDefinition.SubtypeName.Contains("NanobotBuildAndRepairSystem"))
         {
            foreach (var item in CustomActions)
               actions.Add(item);
         }
      }
   }
}
