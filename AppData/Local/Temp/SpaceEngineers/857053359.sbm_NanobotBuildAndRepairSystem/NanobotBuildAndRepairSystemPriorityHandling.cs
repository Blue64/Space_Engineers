namespace SpaceEquipmentLtd.NanobotBuildAndRepairSystem
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using VRage.Game.ModAPI;
   using VRage.ModAPI;
   using VRage.Utils;

   public enum BlockClass
   {
      AutoRepairSystem = 1,
      ShipController,
      Thruster,
      Gyroscope,
      CargoContainer,
      Conveyor,
      ControllableGun,
      Reactor,
      ProgrammableBlock,
      Projector,
      FunctionalBlock,
      ProductionBlock,
      Door,
      ArmorBlock
   }

   public class BlockClassState
   {
      public BlockClass BlockClass { get; }
      public bool Enabled { get; set; }
      public BlockClassState(BlockClass blockClass, bool enabled)
      {
         BlockClass = blockClass;
         Enabled = enabled;
      }
   }

   public class NanobotBuildAndRepairSystemPriorityHandling : List<BlockClassState>
   {
      private bool _BlockClassListDirty = true;
      private List<string> _BlockClassList = new List<string>();


      internal BlockClass? Selected { get; set; } //Visual

      internal NanobotBuildAndRepairSystemPriorityHandling()
      {
         foreach (BlockClass blockClass in Enum.GetValues(typeof(BlockClass)))
         {
            Add(new BlockClassState(blockClass, true));
         }
      }

      /// <summary>
      /// Retrieve the build/repair priority of the block.
      /// </summary>
      internal int GetBuildPriority(IMySlimBlock a)
      {
         var blockClass = GetBlockClass(a, false);
         var keyValue = this.FirstOrDefault((kv) => kv.BlockClass == blockClass);
         return IndexOf(keyValue);
      }

      /// <summary>
      /// Retrieve if the build/repair of this block kind is enabled.
      /// </summary>
      internal bool GetEnabled(IMySlimBlock a)
      {
         var blockClass = GetBlockClass(a, true);
         var keyValue = this.FirstOrDefault((kv) => kv.BlockClass == blockClass);
         return keyValue.Enabled;
      }

      /// <summary>
      /// Get the Block class
      /// </summary>
      /// <param name="a"></param>
      /// <returns></returns>
      private BlockClass GetBlockClass(IMySlimBlock a, bool real)
      {
         var block = a.FatBlock;
         var functionalBlock = a.FatBlock as Sandbox.ModAPI.IMyFunctionalBlock;
         if (!real && functionalBlock != null && !functionalBlock.Enabled) return BlockClass.ArmorBlock; //Switched of -> handle as structural block (if logical class is asked)

         if (block is Sandbox.ModAPI.IMyShipWelder && block.BlockDefinition.SubtypeName.Contains("NanobotBuildAndRepairSystem")) return BlockClass.AutoRepairSystem;
         if (block is Sandbox.ModAPI.IMyShipController) return BlockClass.ShipController;
         if (block is Sandbox.ModAPI.IMyThrust) return BlockClass.Thruster;
         if (block is Sandbox.ModAPI.IMyGyro) return BlockClass.Gyroscope;
         if (block is Sandbox.ModAPI.IMyCargoContainer) return BlockClass.CargoContainer;
         if (block is Sandbox.ModAPI.IMyConveyor || a.FatBlock is Sandbox.ModAPI.IMyConveyorSorter || a.FatBlock is Sandbox.ModAPI.IMyConveyorTube) return BlockClass.Conveyor;
         if (block is Sandbox.ModAPI.IMyUserControllableGun) return BlockClass.ControllableGun;
         if (block is Sandbox.ModAPI.IMyReactor) return BlockClass.Reactor;
         if (block is Sandbox.ModAPI.IMyProgrammableBlock) return BlockClass.ProgrammableBlock;
         if (block is SpaceEngineers.Game.ModAPI.IMyTimerBlock) return BlockClass.ProgrammableBlock;
         if (block is Sandbox.ModAPI.IMyProjector) return BlockClass.Projector;
         if (block is Sandbox.ModAPI.IMyDoor) return BlockClass.Door;
         if (block is Sandbox.ModAPI.IMyProductionBlock) return BlockClass.ProductionBlock;
         if (block is Sandbox.ModAPI.IMyFunctionalBlock) return BlockClass.FunctionalBlock;

         return BlockClass.ArmorBlock;
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="items"></param>
      internal void FillTerminalList(List<MyTerminalControlListBoxItem> items, List<MyTerminalControlListBoxItem> selected)
      {
         foreach(var entry in this)
         {
            var item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(string.Format("{0} ({1})",entry.BlockClass.ToString(), entry.Enabled ? "X" : "-")), MyStringId.NullOrEmpty, entry.BlockClass);
            items.Add(item);

            if (entry.BlockClass == Selected)
            {
               selected.Add(item);
            }
         }
      }

      internal void MoveSelectedUp()
      {
         if (Selected != null)
         {
            var keyValue = this.FirstOrDefault((kv) => kv.BlockClass == Selected);
            var currentPrio = IndexOf(keyValue);
            if (currentPrio > 0) {
               this.Move(currentPrio, currentPrio - 1);
               _BlockClassListDirty = true;
            }
         }
      }

      internal void MoveSelectedDown()
      {
         if (Selected != null)
         {
            var keyValue = this.FirstOrDefault((kv) => kv.BlockClass == Selected);
            var currentPrio = IndexOf(keyValue);
            if (currentPrio >=0 && currentPrio < Count-1)
            {
               this.Move(currentPrio, currentPrio + 1);
               _BlockClassListDirty = true;
            }
         }
      }

      internal void ToggleEnabled()
      {
         if (Selected != null)
         {
            var keyValue = this.FirstOrDefault((kv) => kv.BlockClass == Selected);
            if (keyValue != null)
            {
               keyValue.Enabled = !keyValue.Enabled;
               _BlockClassListDirty = true;
            }
         }
      }

      internal int GetPriority(int blockClass)
      {
         var keyValue = this.FirstOrDefault((kv) => (int)kv.BlockClass == blockClass);
         return IndexOf(keyValue);
      }

      internal void SetPriority(int blockClass, int prio)
      {
         if (prio >= 0 && prio < Count)
         {
            var keyValue = this.FirstOrDefault((kv) => (int)kv.BlockClass == blockClass);
            var currentPrio = IndexOf(keyValue);
            if (currentPrio >= 0)
            {
               this.Move(currentPrio, prio);
               _BlockClassListDirty = true;
            }
         }
      }

      internal bool GetEnabled(int blockClass)
      {
         var keyValue = this.FirstOrDefault((kv) => (int)kv.BlockClass == blockClass);
         return keyValue != null ? keyValue.Enabled : false;
      }

      internal void SetEnabled(int blockClass, bool enabled)
      {
         var keyValue = this.FirstOrDefault((kv) =>(int)kv.BlockClass == blockClass);
         var currentPrio = IndexOf(keyValue);
         if (currentPrio >= 0)
         {
            _BlockClassListDirty = keyValue.Enabled != enabled;
            keyValue.Enabled = enabled;
         }
      }

      internal string GetEntries()
      {
         var value = string.Empty;
         foreach(var entry in this)
         {
            value += string.Format("{0};{1}|", (int)entry.BlockClass, entry.Enabled);
         }
         return value.Remove(value.Length - 1);
      }

      internal void SetEntries(string value)
      {
         if (value == null) return;
         var entries = value.Split('|');
         var prio = 0;
         foreach (var val in entries)
         {
            var blockClassValue = 0;
            var enabled = true;
            var values = val.Split(';');
            if (values.Length >= 2 &&
                int.TryParse(values[0], out blockClassValue) &&
                bool.TryParse(values[1], out enabled))
            {
               var keyValue = this.FirstOrDefault((kv) => kv.BlockClass == (BlockClass)blockClassValue);
               if (keyValue != null)
               {
                  keyValue.Enabled = enabled;
                  var currentPrio = IndexOf(keyValue);
                  this.Move(currentPrio, prio);
                  prio++;
               }
            }
         }
         _BlockClassListDirty = true;
      }

      internal List<string> GetList()
      {
         lock (_BlockClassList)
         {
            if (_BlockClassListDirty)
            {
               _BlockClassListDirty = false;
               _BlockClassList.Clear();
               foreach (var item in this)
               {
                  _BlockClassList.Add(string.Format("{0};{1}", item.BlockClass, item.Enabled));
               }
            }
            return _BlockClassList;
         }
      }
   }
}
