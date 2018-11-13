using RestSharp;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MLFF_IPC_APP
{
    class Program
    {
        private static string myGantrySn = Properties.Settings.Default.GANTRY_SN;
        private static Queue<string> myQueue = new Queue<string>();
        private static string myChar = "", myEPC = "";
        private static DateTime myDateTime = new DateTime();

        private static SerialPort mySerialPort;

        static void Main(string[] args)
        {
            byte[] myTempButeArray;

            mySerialPort = new SerialPort(Properties.Settings.Default.COM);

            Thread myThread = new Thread(Parser);
            Thread myThread2 = new Thread(CommandSender_Recived);

            mySerialPort.BaudRate = 115200;
            mySerialPort.Parity = Parity.None;
            mySerialPort.StopBits = StopBits.One;
            mySerialPort.DataBits = 8;
            mySerialPort.Handshake = Handshake.None;
            //mySerialPort.RtsEnable = true;

            mySerialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandlerAsync);

            myDateTime = DateTime.Now;

            mySerialPort.Open();

            myThread.Start();
            
            myTempButeArray = StringToByteArray("BB01030010004D31303020323064426D2056322E308D7E");
            mySerialPort.Write(myTempButeArray, 0, myTempButeArray.Length);

            myTempButeArray = StringToByteArray("BB01080001010B7E");
            mySerialPort.Write(myTempButeArray, 0, myTempButeArray.Length);

            myThread2.Start();
            Console.WriteLine("Press any key to continue...");
            //for (; ; )
            //{
                
            //    System.Threading.Thread.Sleep(10);
            //}
            Console.WriteLine();
            Console.ReadKey();
            myThread.Join();
            myThread2.Join();
            for (; ; )
            {
                if (myThread.IsAlive == false && myThread2.IsAlive == false)
                {
                    myTempButeArray = StringToByteArray("BB00280000287E");
                    mySerialPort.Write(myTempButeArray, 0, myTempButeArray.Length);
                    break;
                }
                Thread.Sleep(100);
            }
            
            mySerialPort.Close();
        }

        private static void DataReceivedHandlerAsync(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            //string indata = sp.ReadExisting();
            //Console.WriteLine("Data Received:");
            //Console.Write(indata);
            if (sp.ReadBufferSize > 0)
            {
                Byte[] buffer = new Byte[sp.ReadBufferSize];
                Int32 length = (sender as SerialPort).Read(buffer, 0, buffer.Length);
                Array.Resize(ref buffer, length);
                string haxString = ByteArrayToString(buffer);
                haxString = haxString.Replace("bb01ff000115167e", "");
                for (int i = 0; i < haxString.Length; i++)
                {
                    myQueue.Enqueue(haxString.Substring(i, 1));
                }
            }
        }

        private static void Parser()
        {
            for (; ; )
            {
                Thread.Sleep(1);
                if (myQueue.Count % 2 != 0)
                {
                    continue;
                }
                else
                {
                    if (myQueue.Count > 0)
                    {
                        for (int j = 0; j < myQueue.Count; j++)
                        {
                            myChar = myChar + myQueue.Dequeue();
                            myChar = myChar + myQueue.Dequeue();

                            if (myChar.Length == 2 && myChar == "bb")
                            {
                                continue;
                            }
                            else if (myChar.Length == 4 && myChar == "bb02")
                            {
                                continue;
                            }
                            else if (myChar.Length > 4 && myChar.Substring(0, 4) == "bb02")
                            {
                                if (myChar.Length == 48)
                                {
                                    myChar = myChar.Substring(16, 24);

                                    if (myEPC == myChar && DateTime.Now < myDateTime.AddSeconds(10))
                                    {
                                        Console.WriteLine("Same Car, in the same time window!");
                                    }
                                    else
                                    {
                                        myDateTime = DateTime.Now;
                                        Console.WriteLine("EPC:" + myChar + ", date:" + DateTime.Now.ToString("HH:mm:ss.fff"));
                                        myEPC = myChar;
                                        Post2ServerAsync(myEPC);
                                    }
                                    myChar = "";
                                }
                            }
                            else
                            {
                                myChar = "";
                            }
                        }
                    }
                }
            }
        }

        private static void CommandSender_Recived()
        {
            byte[] myTempButeArray;
            for (; ; )
            {
                Thread.Sleep(10);
                myTempButeArray = StringToByteArray("BB00220000227E");
                mySerialPort.Write(myTempButeArray, 0, myTempButeArray.Length);
            }
        }

        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        private static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        private static void Post2ServerAsync(string strEPC)
        {
            var client = new RestClient("https://mlffap.azurewebsites.net/api/Passing");
            var request = new RestRequest(Method.POST);
            request.AddHeader("postman-token", "7431c2da-ffee-ff0f-e162-3d2ba02147f8");
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("content-type", "application/json");
            request.AddParameter("application/json", "{\n  \"EPC\": \"" + strEPC + "\",\n  \"GANTRY_SN\": \"" + myGantrySn + "\"\n}", ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
        }
    }
}
