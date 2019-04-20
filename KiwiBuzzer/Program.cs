using System;
using System.Reflection;
using System.Collections;

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
        private const string HeaderRequest = "Request";
        private const string HeaderRespond= "Respond";
        private const int HeadLength = 7;

        private const int syncFrequency = 3;
        private const int buzzerOpenTime = 500;
        private const int buzzerOffTime = 9000;
        private static MACBase _macBase;

        static int _N = 1;
        static long _offset = 0;
        static long _offsetSum = 0;
        private static readonly EnhancedEmoteLcd Lcd = new EnhancedEmoteLcd();
        static int _nodeSynced = 0;
        static int _nodeResponsed = 0;
        static bool _sentLock = true;
        static Hashtable _responsesRecv = new Hashtable();
        static Hashtable _requestsRecv = new Hashtable();
        static Hashtable _noResponseDelayTimers = new Hashtable();

        private const int NoResponseInterval = 1000;  
        private static readonly TimerCallback NoResponseDelayTimerCallback = noResponseDelay_Timeout;

        private static DateTime _startTime;
        
        public static void Main()
        {
            _startTime = DateTime.Now;
            Debug.EnableGCMessages(false);  //We don't want to see garbage collector messages in the Output window	
            Debug.Print(VersionInfo.VersionBuild(Assembly.GetExecutingAssembly()));

            Thread.Sleep(4000);
            _macBase = RadioConfiguration.GetMAC();
            _macBase.OnReceive += RadioReceive;
            _macBase.OnNeighborChange += MacBase_OnNeighborChange;

            Debug.Print("=======================================");
            var info = "MAC Type: " + _macBase.GetType()
                + ",\nChannel: " + _macBase.MACRadioObj.Channel
                + ",\nPower: " + _macBase.MACRadioObj.TxPower
                + ",\nRadio Address: " + _macBase.MACRadioObj.RadioAddress
                + ",\nRadio Type: " + _macBase.MACRadioObj.RadioName
                + ",\nNeighbor Liveness Delay: " + _macBase.NeighborLivenessDelay;
            Debug.Print(info);
            Debug.Print("=======================================");

	        while(true)
	        {

                _N = MACBase.NeighborListArray().Length;
                _nodeSynced = 0;
                _nodeResponsed = 0;
                _offsetSum = 0;
                _requestsRecv.Clear();
                _responsesRecv.Clear();

                while (!_sentLock);
                RadioSend(time2Long(DateTime.Now).ToString());
                long time1 = time2Long(DateTime.Now);
                while (_N != _nodeSynced + 1 || _N != _nodeResponsed + 1);
                long time2 = time2Long(DateTime.Now);
                long offsetDistence = (_offsetSum / _N) % buzzerOffTime - _offset;
                _offset = (_offsetSum / _N) % buzzerOffTime;
                int resetTime = (buzzerOffTime - (int)(time2 - time1 + (int)offsetDistence) % buzzerOffTime);
                Debug.Print("_offset: " + _offset + ",  _N: " + _N + ", resetTime: "
                    + resetTime + ", offsetDistence: " + offsetDistence);
                Thread.Sleep(resetTime);
                for (int i = 0; i < syncFrequency; i++) {
                    //Buzzer.On();
                    Debug.Print("Beep. Local time: " + time2Long(DateTime.Now));
                    Thread.Sleep(buzzerOpenTime);
                    //Buzzer.Off();
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
            long recvTime = time2Long(receiveDateTime);  //t4 for cacluate time offset, t2 for respond
            long currentTime;
            var msgByte = packet.Payload;
            var msgChar = Encoding.UTF8.GetChars(msgByte);
            var msgStr = new string(msgChar);
            ushort recvFromAddress = packet.Src;
            Debug.Print("Received: \"" + msgStr + "\"" + " from " + packet.Src);
            if (msgStr.Length < HeadLength) {
                return;
            }

            if (msgStr.Substring(0, HeadLength) == HeaderRespond)
            {
                ((Timer)(_noResponseDelayTimers[recvFromAddress])).Change(Timeout.Infinite, Timeout.Infinite);
                string payload = msgStr.Substring(HeaderRespond.Length);
                _responsesRecv[recvFromAddress] = payload;
                String[] timeStrings = payload.Split(' ');
                long requstTime, recvRequestTime, respondTime, recvResponseTime = recvTime;
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
                long rtt = (recvResponseTime - requstTime) - (respondTime - recvRequestTime);
                _offsetSum += (recvRequestTime - requstTime) - (rtt / 2);
                _nodeSynced++;
            }
            else if (msgStr.Substring(0, HeadLength) == HeaderRequest)
            {
                _sentLock = false;
                string sentTimeStr = msgStr.Substring(HeaderRespond.Length);
                _requestsRecv[recvFromAddress] = sentTimeStr;
                currentTime = time2Long(DateTime.Now);
                string response = sentTimeStr + " " + recvTime.ToString() + " " + currentTime.ToString();
                RadioSend(response, recvFromAddress);
                _nodeResponsed++;
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
                _macBase.Send(theNeighbor, toSendByte, 0, (ushort)toSendByte.Length);
                if (_noResponseDelayTimers[theNeighbor] == null)
                {
                    _noResponseDelayTimers[theNeighbor] = new Timer(noResponseDelay_Timeout, null, NoResponseInterval, Timeout.Infinite);
                }
                else {
                    ((Timer)(_noResponseDelayTimers[theNeighbor])).Change(NoResponseInterval, Timeout.Infinite);
                }
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

        static void noResponseDelay_Timeout(object obj)
        {
            RadioSend(time2Long(DateTime.Now).ToString());
            // Restart the no-response timer & display a message
            Debug.Print("No message received ...");
        }
    }
}
