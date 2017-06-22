using System;
using Sandbox.ModAPI;

namespace LCDCameraMod
{
    public class Communication
    {
        /// <summary>
        /// This is kind of shitty.  I should just bitshift and copy the lengths, but whatever.  We're not sending
        /// this enough to really care.  Max size is 1MB (steam handles fragmentation).  I can implement a message
        /// splitter, but 1MB should be fine for what we need here.
        /// </summary>
        /// <param name="dataId"></param>
        /// <param name="text"></param>
		public static void SendDataToPlayer(ulong steamId, long msgId, string text, ushort id = 9020)
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes(text);
            string msgIdString = msgId.ToString();
            byte[] newData = new byte[data.Length + msgIdString.Length + 1];
            newData[0] = (byte)msgIdString.Length;
            for (int r = 0; r < msgIdString.Length; r++)
                newData[r + 1] = (byte)msgIdString[r];

            Array.Copy(data, 0, newData, msgIdString.Length + 1, data.Length);
            MyAPIGateway.Multiplayer.SendMessageTo(id, newData, steamId);
            Logging.Instance.WriteLine(string.Format("Sending {0} bytes to {1}", newData.Length, steamId));
        }

    }
}
