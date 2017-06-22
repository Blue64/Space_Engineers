using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LCDCameraMod.DataMessage
{
    public abstract class DataHandlerBase
    {
        public virtual Boolean CanHandle(byte[] data)
        {
            long dataId = DecodeData(data);
            if (GetDataId() == dataId)
                return true;

            return false;
        }

        /// <summary>
        /// Processes byte data receieved from a client.  A client byte data contains an ID, a steamId, and the data.
        /// </summary>
        /// <param name="data">Unparsed data</param>
        /// <param name="newData">New data minus headers</param>
        /// <param name="steamId">Steam id of sender</param>
		public void ProcessCommand(byte[] data, out byte[] newData, out ulong steamId)
        {
            byte length = data[0];
            byte steamLength = data[length + 1];
            steamId = DecodeSteamId(data);
            newData = new byte[data.Length - 1 - length - 1 - steamLength];
            Array.Copy(data, length + 1 + steamLength + 1, newData, 0, newData.Length);
            //Buffer.BlockCopy(data, length + 1 + steamLength + 1, newData, 0, newData.Length);
        }

        protected byte[] EncodeData(long data)
        {
            string convert = data.ToString();
            byte[] result = new byte[convert.Length + 1];
            result[0] = (byte)convert.Length;
            for (int r = 1; r < convert.Length; r++)
                result[r] = (byte)convert[r - 1];
            return result;
        }

        protected long DecodeData(byte[] data)
        {
            byte length = data[0];
            string convert = "";
            for (int r = 1; r < length + 1; r++)
                convert += (char)data[r];

            return long.Parse(convert);
        }

        protected ulong DecodeSteamId(byte[] data)
        {
            byte length = data[0];
            byte steamIdLength = data[length + 1];
            string convert = "";
            for (int r = 1; r < steamIdLength + 1; r++)
                convert += (char)data[length + 1 + r];

            return ulong.Parse(convert);
        }

        public virtual long GetDataId()
        {
            return 0;
        }

        public virtual void HandleCommand(ulong steamId, byte[] data)
        {

        }
    }
}
