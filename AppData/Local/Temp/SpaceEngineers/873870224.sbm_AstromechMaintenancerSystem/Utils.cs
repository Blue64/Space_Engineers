using Sandbox.ModAPI;
using System;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities;
using Sandbox.Game;
using VRage.Game.Entity;
using VRageMath;

namespace SpaceEquipmentLtd.Utils
{
   public static class Utils
   {
      /// <summary>
      /// Is the block damaged/incomplete
      /// </summary>
      public static bool NeedRepair(this IMySlimBlock target)
      {
         //I use target.HasDeformation && target.MaxDeformation > X) as I had several times both situations, a landing gear reporting HasDeformation or a block reporting target.MaxDeformation > 0.1 both weren't repairable and caused welding this blocks forever!
         return !target.IsDestroyed && !target.IsFullyDismounted && (target.FatBlock == null || !target.FatBlock.Closed) && ((target.HasDeformation && target.MaxDeformation > 0.0001f) || !target.IsFullIntegrity);
      }

      /// <summary>
      /// Is the block a projected block
      /// </summary>
      public static bool IsProjected(this IMySlimBlock target)
      {
         var cubeGrid = target.CubeGrid as MyCubeGrid;
         return (cubeGrid != null && cubeGrid.Projector != null);
      }

      /// <summary>
      /// Could the projected block could be build
      /// !GUI Thread!
      /// </summary>
      /// <param name="target"></param>
      /// <returns></returns>
      public static bool CouldBuild(this IMySlimBlock target)
      {
         var cubeGrid = target.CubeGrid as MyCubeGrid;
         if (cubeGrid == null || cubeGrid.Projector == null) return false;
         var canBuild = ((IMyProjector)cubeGrid.Projector).CanBuild(target, true);
         return canBuild == BuildCheckResult.OK;
      }

      /// <summary>
      /// The inventory is filled to X percent
      /// </summary>
      /// <param name="inventory"></param>
      /// <returns></returns>
      public static float IsFilledToPercent(this IMyInventory inventory)
      {
         return Math.Max((float)inventory.CurrentVolume / (float)inventory.MaxVolume, (float)inventory.CurrentMass / (float)((MyInventory)inventory).MaxMass);
      }

      /// <summary>
      /// Checks if block is inside the given BoundingBox 
      /// </summary>
      /// <param name="block"></param>
      /// <param name="areaBox"></param>
      /// <returns></returns>
      public static bool IsInRange(this IMySlimBlock block, ref MyOrientedBoundingBoxD areaBox, out double distance)
      {
         Vector3 halfExtents;
         block.ComputeScaledHalfExtents(out halfExtents);
         var matrix = block.CubeGrid.WorldMatrix;
         matrix.Translation = block.CubeGrid.GridIntegerToWorld(block.Position);
         var box = new MyOrientedBoundingBoxD(new BoundingBoxD(-(halfExtents), (halfExtents)), matrix);
         var inRange = areaBox.Intersects(ref box);
         distance = inRange ? (areaBox.Center - box.Center).Length() : 0;
         return inRange;
      }

      /// <summary>
      /// Get the block name for GUI
      /// </summary>
      /// <param name="slimBlock"></param>
      /// <returns></returns>
      public static string BlockName(this IMySlimBlock slimBlock)
      {
         if (slimBlock != null)
         {
            var terminalBlock = slimBlock.FatBlock as IMyTerminalBlock;
            if (terminalBlock != null)
            {
               return string.Format("{0}.{1}", terminalBlock.CubeGrid != null ? terminalBlock.CubeGrid.DisplayName : "Unknown Grid", terminalBlock.CustomName);
            }
            else
            {
               return string.Format("{0}.{1}", slimBlock.CubeGrid != null ? slimBlock.CubeGrid.DisplayName : "Unknown Grid", slimBlock.BlockDefinition.DisplayNameText);
            }
         }
         else return "(none)";
      }

      public static string BlockName(this VRage.Game.ModAPI.Ingame.IMySlimBlock slimBlock)
      {
         if (slimBlock != null)
         {
            var terminalBlock = slimBlock.FatBlock as Sandbox.ModAPI.Ingame.IMyTerminalBlock;
            if (terminalBlock != null)
            {
               return string.Format("{0}.{1}", terminalBlock.CubeGrid != null ? terminalBlock.CubeGrid.DisplayName : "Unknown Grid", terminalBlock.CustomName);
            }
            else
            {
               return string.Format("{0}.{1}", slimBlock.CubeGrid != null ? slimBlock.CubeGrid.DisplayName : "Unknown Grid", slimBlock.BlockDefinition.ToString());
            }
         }
         else return "(none)";
      }
   }

}
