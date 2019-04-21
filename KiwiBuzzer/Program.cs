using System;
using System.Reflection;
using System.Collections;

using System.Text;
using System.Threading;
using Microsoft.SPOT;

using Samraksh.eMote.Net;
using Samraksh.eMote.Net.MAC;
using Samraksh.eMote.SensorBoard;

namespace TimeSync
{
    public class Program
    {
        private const string HeaderRequest = "Request";
        private const string HeaderRespond= "Respond";
        private const int HeadLength = 7;

        private const int beepEvery = 5000;
        private const int requestEvery = 10000;
        private const int epsilon = 100;
        private const int buzzerOnTime = 500;

        private static MACBase _macBase;

        static long _offset = 0;
        static long _offsetSum = 0;
        static long _realOffset = 0;

        //private static readonly EnhancedEmoteLcd Lcd = new EnhancedEmoteLcd();
        static int _numResponded = 0;

        private static DateTime _startTime;

        static long getLocalTime()
        {
            return time2Long(DateTime.Now) + _offset;
        }

        // beep thread
        static void Beep()
        {
            while(true) {
                if ((getLocalTime() % beepEvery) < epsilon)
                {
                    Buzzer.On();
                    Debug.Print("Beep. Local time: " + getLocalTime() + " offset is " + _offset);
                    Thread.Sleep(buzzerOnTime);
                    Buzzer.Off();
                }
            }
        }

        // request thread
        static void Request()
        {
            while (true)
            {
                if ((getLocalTime() % requestEvery) < epsilon)
                {
                    // initialize _offsetSum
                    _offsetSum = 0;
                    _numResponded = 0;
                    
                    // send request
                    RadioSend(getLocalTime().ToString());

                    // wait for reply
                    Thread.Sleep(500);

                    // update offset
                    _offset += (_numResponded == 0) ? 0 : (_offsetSum / (_numResponded + 1));
                    _realOffset += (_numResponded == 0) ? 0 : (_offsetSum / (_numResponded));
                    //Debug.Print("real_offset: " + real_offset + " responded number: " + _numResponded);
                   
                }
            }
        }
        
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

            Thread beep = new Thread(Beep);
            Thread request = new Thread(Request);
            beep.Start();
            request.Start();
        }

        private static long time2Long(DateTime dateTime) 
        {
            return (dateTime.ToUniversalTime().Ticks - _startTime.ToUniversalTime().Ticks) / 10000;
        }

        private static void RadioReceive(IMAC macBase, DateTime receiveDateTime, Packet packet)
        {
            /*
             * t1: when the local node sent a request
             * t2: when a partner node received the request
             * t3: when the partner node sent a response
             * t4: when then local node received the response
             * 
             * requset: "Request t1"
             * respond: "Respond t1 t2 t3"
             */
            long recvTime = time2Long(receiveDateTime) + _offset;  // could be t2 or t4
            
            var msgByte = packet.Payload;
            var msgChar = Encoding.UTF8.GetChars(msgByte);
            var msgStr = new string(msgChar);
            
            ushort recvFromAddress = packet.Src;
            
            Debug.Print("\tReceived: \"" + msgStr + "\"" + " from " + packet.Src);
            
            if (msgStr.Length < HeadLength) {
                return;
            }

            if (msgStr.Substring(0, HeadLength) == HeaderRespond)
            {
                string payload = msgStr.Substring(HeaderRespond.Length);
                String[] timeStrings = payload.Split(' ');
                long t1, t2, t3, t4 = recvTime;
                try
                {
                    t1 = long.Parse(timeStrings[0]);
                    t2 = long.Parse(timeStrings[1]);
                    t3 = long.Parse(timeStrings[2]);
                }
                catch
                {
                    return;
                }

                _offsetSum += (t2 - t1 + t3 - t4) / 2;
                _numResponded++;
            }
            else if (msgStr.Substring(0, HeadLength) == HeaderRequest)
            {
                string t1Str = msgStr.Substring(HeaderRespond.Length);
                long t3 = getLocalTime();
                string response = t1Str + " " + recvTime.ToString() + " " + t3.ToString();
                RadioSend(response, recvFromAddress);
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
                Debug.Print("\tSending request message  \"" + toSend + "\" to " + theNeighbor);
                _macBase.Send(theNeighbor, toSendByte, 0, (ushort)toSendByte.Length);
            }
        }

        private static void RadioSend(string toSend, ushort address)
        {
            var toSendByte = Encoding.UTF8.GetBytes(HeaderRespond + toSend);
            Debug.Print("\tSending response message \"" + toSend + "\" to " + address);
            _macBase.Send(address, toSendByte, 0, (ushort)toSendByte.Length);
        }

        static void MacBase_OnNeighborChange(IMAC macInstance, DateTime time)
        {
            var neighborList = MACBase.NeighborListArray();
            macInstance.NeighborList(neighborList);
            PrintNeighborList("\t\tNeighbor list CHANGE for Node [" + _macBase.MACRadioObj.RadioAddress + "]: ", neighborList);
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
