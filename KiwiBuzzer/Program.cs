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
        private const string HeaderRequest = "Request";
        private const string HeaderRespond= "Respond";
        private const int HeadLength = 7;

        private static MACBase _macBase;

        static int _N = 0;
        static long _offset = 0;
        
        public static void Main()
        {
	        while(true)
	        {
	        }
        }

        private static long time2Long(DateTime dateTime) {
            return (dateTime.ToUniversalTime().Ticks - 621355968000000000) / 10000;
        }

        private static void RadioReceive(IMAC macBase, DateTime receiveDateTime, Packet packet)
        {
            long sentTime = time2Long(packet.SenderEventTimeStamp); // t1 for requesting message, t3 for response message
            long recvTime = time2Long(receiveDateTime); // t4 for cacluate time offset, t2 for respond
            Debug.Print("Received " + packet.Payload.Length + " bytes from " + packet.Src);
            var msgByte = packet.Payload;
            var msgChar = Encoding.UTF8.GetChars(msgByte);
            var msgStr = new string(msgChar);
            if (msgStr.Substring(0, HeadLength) == HeaderRespond)
            {
                string payload = msgStr.Substring(HeaderRespond.Length);
                String[] timeStrings = payload.Split(' ');
                long requstTime, recvRequestTime;
                long respondTime = sentTime; 
                long recvResponseTime = recvTime;
                try
                {
                    requstTime = long.Parse(timeStrings[0]);
                    recvRequestTime = long.Parse(timeStrings[1]);
                }
                catch
                {
                    return;
                }

                _N++;
                long rtt = (recvResponseTime  - requstTime) - (respondTime - recvRequestTime);
                _offset = (_offset * (_N - 1) + (recvRequestTime - requstTime) - (rtt / 2)) / _N;
            }
            else if (msgStr.Substring(0, HeadLength) == HeaderRequest)
            {
                RadioSend(sentTime.ToString() + " " + recvTime.ToString(), packet.Src);
            } 
            return;
        }

        private static void RadioSend(string toSend)
        {
            var toSendByte = Encoding.UTF8.GetBytes(HeaderRequest + toSend);
            var neighborList = MACBase.NeighborListArray();
            _macBase.NeighborList(neighborList);
            foreach (var theNeighbor in neighborList)
            {
                if (theNeighbor == 0)
                {
                    break;
                }
                Debug.Print("Sending request message  \"" + toSend + "\" to " + theNeighbor);
                _macBase.Send(theNeighbor, toSendByte, 0, (ushort)toSendByte.Length, DateTime.Now);
            }
        }

        private static void RadioSend(string toSend, ushort address)
        {
            var toSendByte = Encoding.UTF8.GetBytes(HeaderRespond + toSend);
            Debug.Print("Sending response message \"" + toSend + "\" to " + address);
            _macBase.Send(address, toSendByte, 0, (ushort)toSendByte.Length, DateTime.Now);
        }
    }
}
