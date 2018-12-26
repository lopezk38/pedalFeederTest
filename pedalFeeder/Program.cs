using System;
using System.Collections.Generic;
using vJoyInterfaceWrap;
using System.IO.Ports;
using System.Threading;

namespace pedalFeeder
{
    class Program
    {
        static public vJoy joystick;
        static public vJoy.JoystickState iReport;
        static public uint id = 1;

        static void Main(string[] args)
        {

            // Create one joystick object and a position structure.
            joystick = new vJoy();
            iReport = new vJoy.JoystickState();


            // Device ID can only be in the range 1-16
            if (args.Length > 0 && !String.IsNullOrEmpty(args[0]))
                id = Convert.ToUInt32(args[0]);
            if (id <= 0 || id > 16)
            {
                Console.WriteLine("Illegal device ID {0}\nExit!", id);
                return;
            }

            // Get the driver attributes (Vendor ID, Product ID, Version Number)
            if (!joystick.vJoyEnabled())
            {
                Console.WriteLine("vJoy driver not enabled: Failed Getting vJoy attributes.\n");
                return;
            }
            else
                Console.WriteLine("Vendor: {0}\nProduct :{1}\nVersion Number:{2}\n", joystick.GetvJoyManufacturerString(), joystick.GetvJoyProductString(), joystick.GetvJoySerialNumberString());

            // Get the state of the requested device
            VjdStat status = joystick.GetVJDStatus(id);
            switch (status)
            {
                case VjdStat.VJD_STAT_OWN:
                    Console.WriteLine("vJoy Device {0} is already owned by this feeder\n", id);
                    break;
                case VjdStat.VJD_STAT_FREE:
                    Console.WriteLine("vJoy Device {0} is free\n", id);
                    break;
                case VjdStat.VJD_STAT_BUSY:
                    Console.WriteLine("vJoy Device {0} is already owned by another feeder\nCannot continue\n", id);
                    return;
                case VjdStat.VJD_STAT_MISS:
                    Console.WriteLine("vJoy Device {0} is not installed or disabled\nCannot continue\n", id);
                    return;
                default:
                    Console.WriteLine("vJoy Device {0} general error\nCannot continue\n", id);
                    return;
            };

            // Check which axes are supported
            bool AxisX = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_X);
            bool AxisY = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_Y);
            bool AxisZ = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_Z);
            int nButtons = joystick.GetVJDButtonNumber(id);

            // Print results
            Console.WriteLine("\nvJoy Device {0} capabilities:\n", id);
            Console.WriteLine("Axis X\t\t{0}\n", AxisX ? "Yes" : "No");
            Console.WriteLine("Axis Y\t\t{0}\n", AxisX ? "Yes" : "No");
            Console.WriteLine("Axis Z\t\t{0}\n", AxisX ? "Yes" : "No");
            Console.WriteLine("Number of buttons\t\t{0}\n", nButtons);

            // Test if DLL matches the driver
            UInt32 DllVer = 0, DrvVer = 0;
            bool match = joystick.DriverMatch(ref DllVer, ref DrvVer);
            if (match)
                Console.WriteLine("Version of Driver Matches DLL Version ({0:X})\n", DllVer);
            else
                Console.WriteLine("Version of Driver ({0:X}) does NOT match DLL Version ({1:X})\n", DrvVer, DllVer);


            // Acquire the target
            if ((status == VjdStat.VJD_STAT_OWN) || ((status == VjdStat.VJD_STAT_FREE) && (!joystick.AcquireVJD(id))))
            {
                Console.WriteLine("Failed to acquire vJoy device number {0}.\n", id);
                return;
            }
            else
                Console.WriteLine("Acquired: vJoy device number {0}.\n", id);

            int X, Y, Z, tempX, tempY, tempZ, tempX1, tempX2;

            X = 0;
            Y = 0;
            Z = 0;
            iReport.Buttons = (uint)(0x0);

            tempX = 0;
            tempY = 0;
            tempZ = 0;

            tempX1 = 0;
            tempX2 = 0;

            long maxVal = 0; 

            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_X, ref maxVal);

            long halfVal = maxVal / 2;

            //MapClass mapper = new MapClass(0, 255, 0, maxVal); //Start at beginning of axis and go up
            //MapClass mapper = new MapClass(0, 255, halfVal, maxVal); //Start at center of axis and go up
            MapClass mapper1 = new MapClass(0, 705, halfVal, 0); //Start at center of axis and go down. Limit maximum 
            MapClass mapper2 = new MapClass(0, 705, 0, maxVal); //Start at beginning of axis and go up. Limit maximum
            SerialCommunicator serPort = new SerialCommunicator();

            while (true)
                {
                iReport.bDevice = (byte)id;
                iReport.AxisX = X;
                iReport.AxisY = Y;
                //iReport.AxisZ = Z;

                /*** Feed the driver with the position packet - is fails then wait for input then try to re-acquire device ***/
                if (!joystick.UpdateVJD(id, ref iReport))
                {
                    Console.WriteLine("Feeding vJoy device number {0} failed - try to enable device then press enter\n", id);
                    Console.ReadKey(true);
                    joystick.AcquireVJD(id);
                }

                System.Threading.Thread.Sleep(20);

                serPort.AskForData(ref tempX, ref tempY, ref tempZ);
                tempX1 = (int)mapper2.Map(tempX);
                Y = (int)mapper1.Map(tempY);
                tempX2 = (int)mapper2.Map(tempZ);
                X = (int)halfVal + ((tempX1 / 2) - (tempX2 / 2));
                Console.WriteLine((X.ToString()) + ", " + (Y.ToString()) + ", " + (Z.ToString()));
                if (tempX > 200 && tempZ > 200)
                {
                    iReport.Buttons = (uint)(0x1000);
                } else
                {
                    iReport.Buttons = (uint)(0x0000);
                }
            }; // While
        }

    }

    class MapClass
    {
        private Dictionary<long, long> mapTable;
        private long startVal = 0;
        private long endVal = 255;

        public MapClass()
        {
            mapTable = new Dictionary<long, long>(255); 
     
            for (int i = 0; i <= 255; i++)
            {
                mapTable.Add(i, (long)(i * (100.0 / 255.0)));
            }
        }

        public MapClass(long minRange, long maxRange, long minTargetRange, long maxTargetRange)
        {
            if (minRange > maxRange)
            {
                long temp = maxRange;
                maxRange = minRange;
                minRange = temp;
            }

            mapTable = new Dictionary<long, long>(Math.Abs((int)(maxRange - minRange)));

            for (long i = minRange; i <= maxRange; i++)
            {
                mapTable.Add(i, (long)(minTargetRange + (i - minRange) * (double)(maxTargetRange - minTargetRange) / (double)(maxRange - minRange)));
            }

            startVal = minRange;
            endVal = maxRange;
        }

        public long Map(long input)
        {
            long output;

            if (!(mapTable.TryGetValue(input, out output)))
            {
                if (input < startVal)
                {
                    output = startVal;
                } else if (input > endVal)
                {
                    output = endVal;
                }
            }

            return output;
        }
    }

    class SerialCommunicator
    {
        // Create the serial port with basic settings
        private SerialPort port;

        private ManualResetEvent waitForData = new ManualResetEvent(false);

        private readonly char[] readySignal = new char[] { 'R' };

        public SerialCommunicator()
        {
            port = new SerialPort("COM6", 9600, Parity.None, 8, StopBits.One);

            port.ReceivedBytesThreshold = 3;

            port.DataReceived += new
              SerialDataReceivedEventHandler(Port_DataReceived);

            port.Open();
            port.DiscardInBuffer();
        }

        public SerialCommunicator(string serialPort)
        {
            port = new SerialPort(serialPort, 9600, Parity.None, 8, StopBits.One);

            port.ReceivedBytesThreshold = 3;

            port.DataReceived += new
              SerialDataReceivedEventHandler(Port_DataReceived);

            port.Open();
            port.DiscardInBuffer();
        }

        private void Port_DataReceived(object sender,
          SerialDataReceivedEventArgs e)
        {
            waitForData.Set();
        }

        public void AskForData(ref int x, ref int y, ref int z)
        {
            port.Write(readySignal, 0, 1);
            waitForData.WaitOne();
            waitForData.Reset();
            x = port.ReadByte();
            y = port.ReadByte();
            z = port.ReadByte();
        }
    }
}
