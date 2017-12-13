using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimeSync
{
    class Program
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            public short wYear;
            public short wMonth;
            public short wDayOfWeek;
            public short wDay;
            public short wHour;
            public short wMinute;
            public short wSecond;
            public short wMilliseconds;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetSystemTime([In] ref SYSTEMTIME st);

        [DllImport("kernel32.dll")]
        static extern uint GetLastError();

        static void Main(string[] args)
        {
            try
            {
                bool Syncing = true;

                int SyncInterval = Convert.ToInt32(
                                    ConfigurationManager.AppSettings[
                                    "SyncInterval"].ToString());

                string NtpUrl = ConfigurationManager.AppSettings[
                    "TimeServerURL"].ToString();


                while (Syncing)
                {
                    DateTime NetworkDateTime = GetNetworkTime(NtpUrl);

                    SYSTEMTIME SysTime = new SYSTEMTIME();
                    SysTime.wYear = (short)NetworkDateTime.Year;
                    SysTime.wMonth = (short)NetworkDateTime.Month;
                    SysTime.wDay = (short)NetworkDateTime.Day;
                    SysTime.wHour = (short)NetworkDateTime.Hour;
                    SysTime.wMinute = (short)NetworkDateTime.Minute;
                    SysTime.wSecond = (short)NetworkDateTime.Second;

                    var ret = SetSystemTime(ref SysTime);

                    System.Console.WriteLine("Last error : " + GetLastError());
                    Console.WriteLine("SetSystemTime return : " + ret);


                    for (int i = 0; i < SyncInterval; ++i)
                        Thread.Sleep(1000 * 60);
                }
            }
            catch (Exception ex)
            {

            }
        }

        public static DateTime GetNetworkTime(string ntpUrl)
        {
            string ntpServer = ntpUrl;
            if (String.IsNullOrEmpty(ntpUrl))
                ntpServer = "pool.ntp.org";

            // NTP message size - 16 bytes of the digest (RFC 2030)
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;

            //The UDP port number assigned to NTP is 123
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            //NTP uses UDP
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(ipEndPoint);

                //Stops code hang if NTP is blocked
                socket.ReceiveTimeout = 3000;

                socket.Send(ntpData);
                socket.Receive(ntpData);
            }

            //Offset to get to the "Transmit Timestamp" field (time at which the reply 
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            //**UTC** time
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime;
        }

        // stackoverflow.com/a/3294698/162671
        static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
    }
}
