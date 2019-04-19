using System;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.SPOT;

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

        private const int syncFrequency = 3;
        private const int buzzerOpenTime = 500;
        private const int buzzerOffTime = 9000;

       // private const int cycleTime = (syncFrequency + 1) * buzzerOffTime + syncFrequency * buzzerOpenTime;

        private static MACBase _macBase;

        static int _N = 1;
        static long _offset = 0;
        static long _offsetSum = 0;
        private static readonly EnhancedEmoteLcd Lcd = new EnhancedEmoteLcd();
        static int _nodeSynced = 0;
        static int _nodeResponsed = 0;
        static bool _sentLock = true;

        private static DateTime _startTime;
        
        public static void Main()
        {
            _startTime = DateTime.Now; 
            Debug.EnableGCMessages(false);  // We don't want to see garbage collector messages in the Output window	
            Debug.Print(VersionInfo.VersionBuild(Assembly.GetExecutingAssembly()));

             // Display a welcome message
            Lcd.Write("Hola");
            Thread.Sleep(4000);
             _macBase = RadioConfiguration.GetMAC();
            _macBase.OnReceive += RadioReceive;
            _macBase.OnNeighborChange += MacBase_OnNeighborChange;

             Debug.Print("=======================================");
            var info = "MAC Type: " + _macBase.GetType()
                + ", Channel: " + _macBase.MACRadioObj.Channel
                + ", Power: " + _macBase.MACRadioObj.TxPower
                + ", Radio Address: " + _macBase.MACRadioObj.RadioAddress
                + ", Radio Type: " + _macBase.MACRadioObj.RadioName
                + ", Neighbor Liveness Delay: " + _macBase.NeighborLivenessDelay;
            Debug.Print(info);
            Debug.Print("=======================================");
	        while(true)
	        {
                while (!_sentLock);
                RadioSend(time2Long(DateTime.Now).ToString());
                long time1 = time2Long(DateTime.Now);
                while (_N != _nodeSynced + 1 || _N != _nodeResponsed + 1);
                long time2 = time2Long(DateTime.Now);
                long offsetDistence = (_offsetSum / _N) % buzzerOffTime - _offset;
                _offset = (_offsetSum / _N) % buzzerOffTime;
                Debug.Print("offset is " + _offset + "  _N is " + _N);
                int resetTime = (buzzerOffTime - (int)(time2 - time1 + (int)offsetDistence) % buzzerOffTime);
                Thread.Sleep(resetTime);
                _N = 1;
                _nodeSynced = 0;
                _nodeResponsed = 0;
                _offsetSum = 0;
                for (int i = 0; i < syncFrequency; i++) {
                    //Buzzer.On();
                    Debug.Print("local time: " + time2Long(DateTime.Now));
                    Thread.Sleep(buzzerOpenTime);
                   // Buzzer.Off();
                    Thread.Sleep(buzzerOffTime);
                }
	        }
        }

        private static long time2Long(DateTime dateTime) 
        {
            return (dateTime.ToUniversalTime().Ticks - _startTime.ToUniversalTime().Ticks) / 10000;
        }

        private static void RadioReceive(IMAC macBase, DateTime receiveDateTime, Packet packet)
        {
            long recvTime = time2Long(receiveDateTime); // t4 for cacluate time offset, t2 for respond
            long currentTime;
            Debug.Print("Received " + packet.Payload.Length + " bytes from " + packet.Src);
            var msgByte = packet.Payload;
            var msgChar = Encoding.UTF8.GetChars(msgByte);
            var msgStr = new string(msgChar);
            Debug.Print("receive message is " + msgStr.Substring(0, HeadLength) + " " + msgStr.Substring(HeadLength));
            if (msgStr.Substring(0, HeadLength) == HeaderRespond)
            {
                string payload = msgStr.Substring(HeaderRespond.Length);
                String[] timeStrings = payload.Split(' ');
                long requstTime, recvRequestTime;
                long respondTime; 
                long recvResponseTime = recvTime;
                try
                {
                    requstTime = long.Parse(timeStrings[0]);
                    recvRequestTime = long.Parse(timeStrings[1]);
                    respondTime = long.Parse(timeStrings[2]);
                }
                catch
                {
                    return;
                }
                long rtt = (recvResponseTime  - requstTime) - (respondTime - recvRequestTime);
                _offsetSum += (recvRequestTime - requstTime) - (rtt / 2);
                _nodeSynced++;
            }
            else if (msgStr.Substring(0, HeadLength) == HeaderRequest)
            {
                _sentLock = false;
                string sentTimeStr = msgStr.Substring(HeaderRespond.Length);
                currentTime = time2Long(DateTime.Now);
                _nodeResponsed++;
                RadioSend(sentTimeStr + " " + recvTime.ToString() + " " + currentTime.ToString(), (ushort)packet.Src);
                _sentLock = true;
            } 
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
                _N = _N + 1;
                _macBase.Send(theNeighbor, toSendByte, 0, (ushort)toSendByte.Length);
            }
        }

        private static void RadioSend(string toSend, ushort address)
        {
            var toSendByte = Encoding.UTF8.GetBytes(HeaderRespond + toSend);
            Debug.Print("Sending response message \"" + toSend + "\" to " + address);
            _macBase.Send(address, toSendByte, 0, (ushort)toSendByte.Length);
        }

        static void MacBase_OnNeighborChange(IMAC macInstance, DateTime time)
        {
            var neighborList = MACBase.NeighborListArray();
            macInstance.NeighborList(neighborList);
            PrintNeighborList("Neighbor list CHANGE for Node [" + _macBase.MACRadioObj.RadioAddress + "]: ", neighborList);
        }

        private static void PrintNeighborList(string prefix, ushort[] neighborList)
        {
            PrintNumericVals(prefix, neighborList);
        }

        public static void PrintNumericVals(string prefix, ushort[] messageEx)
        {
            var msgBldr = new StringBuilder(prefix);
            foreach (var val in messageEx)
            {
                msgBldr.Append(val + " ");
            }
            Debug.Print(msgBldr.ToString());
        }
    }
}
