using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.ModAPI;

namespace SpaceEquipmentLtd.Utils
{
   public static class UtilsInventory
   {
      /// <summary>
      /// Push all components into destinations
      /// </summary>
      public static bool PushComponents(this IMyInventory srcInventory, List<IMyInventory> destinations)
      {
         var moved = false;
         lock (destinations)
         {
            var srcItems = srcInventory.GetItems();
            for (int i1 = srcItems.Count-1; i1 >= 0; i1--)
            {
               var srcItem = srcItems[i1];
               foreach (var destInventory in destinations)
               {
                  if (destInventory.CanItemsBeAdded(srcItem.Amount, srcItem.Content.GetId()))
                  {
                     moved = srcInventory.TransferItemTo(destInventory, i1, null, true, srcItem.Amount) || moved;
                     break;
                  }
                  //If not the whole amount could be transfered i give up.
                  //I could check if parts could be transfered, but as long as ComputeAmountThatFits is not available,
                  //it would be try and error and a performance gain
               }
            }
         }
         return moved;
      }
   }
}
