using System;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.SPOT;

// using Samraksh.AppNote.Utility;
using Samraksh.eMote.Net;
using Samraksh.eMote.Net.MAC;
using Samraksh.eMote.SensorBoard;

namespace KiwiBuzzer
{
    public class Program
    {
        
        // This is used as a header for the packet payload to identify the app
        private const string Header = "PingPong";

        // The current value
        static int _currVal;

        private static MACBase _macBase;
        
        public static void Main()
        {
	        while(true)
	        {
		        
	        }
        }

        private static void RadioReceive(IMAC macBase, DateTime receiveDateTime, Packet packet)
        {
            Debug.Print("Received " + packet.Payload.Length + " bytes from " + packet.Src);

            // Check if message is for us
            var msgByte = packet.Payload;
            var msgChar = Encoding.UTF8.GetChars(msgByte);
            var msgStr = new string(msgChar);
            if (msgStr.Substring(0, Header.Length) != Header)
            {
                return;
            }
            // Get payload and check if it is in the correct format (an integer)
            string payload = msgStr.Substring(Header.Length);
            int recVal;
            try
            {
                recVal = int.Parse(payload);
            }
            catch
            {
                return;
            }

            //
            // We've received a correct message
            //

            // Reset the no-response timer
            StartOneshotTimer(ref _noResponseDelayTimer, NoResponseDelayTimerCallback, NoResponseInterval);

            // Update the current value
            int origVal = _currVal;
            _currVal = System.Math.Max(_currVal, recVal);
            _currVal++;
            Lcd.Write(_currVal);
            Debug.Print("Orig val " + origVal + ", rec val " + recVal + ", new val " + _currVal);
            Debug.Print("\nrssi: " + packet.RSSI);

            // Wait a bit before sending reply
            StartOneshotTimer(ref _replyTimer, ReplyTimerCallback, SendInterval);
        }

        private static void RadioSend(string toSend)
        {
            var toSendByte = Encoding.UTF8.GetBytes(Header + toSend);
            var neighborList = MACBase.NeighborListArray();
            _macBase.NeighborList(neighborList);
            foreach (var theNeighbor in neighborList)
            {
                if (theNeighbor == 0)
                {
                    break;
                }
                Debug.Print("Sending message \"" + toSend + "\" to " + theNeighbor);
                _macBase.Send(theNeighbor, toSendByte, 0, (ushort)toSendByte.Length);
            }
        }
    }
}
