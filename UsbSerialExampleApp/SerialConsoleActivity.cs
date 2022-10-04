/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using Hoho.Android.UsbSerial.Driver;
using Hoho.Android.UsbSerial.Extensions;
using Hoho.Android.UsbSerial.Util;
using Java.Nio;
using Javax.Xml.Transform.Sax;


namespace UsbSerialExampleApp
{
    [Activity(Label = "@string/app_name", LaunchMode = LaunchMode.SingleTop)]
    public class SerialConsoleActivity : Activity
    {
        static readonly string TAG = typeof(SerialConsoleActivity).Name;

        public const string EXTRA_TAG = "PortInfo";
        const int READ_WAIT_MILLIS = 200;
        const int WRITE_WAIT_MILLIS = 200;

        const int PACKET_SIZE = 58; // 512;

        UsbSerialPort port;

        UsbManager usbManager;
        TextView titleTextView;
        TextView dumpTextView;
        ScrollView scrollView;
        Button sleepButton;
        Button wakeButton;

        SerialInputOutputManager serialIoManager;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            Log.Info(TAG, "OnCreate");

            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.serial_console);

            usbManager = GetSystemService(Context.UsbService) as UsbManager;
            titleTextView = FindViewById<TextView>(Resource.Id.demoTitle);
            dumpTextView = FindViewById<TextView>(Resource.Id.consoleText);
            scrollView = FindViewById<ScrollView>(Resource.Id.demoScroller);

            sleepButton = FindViewById<Button>(Resource.Id.sleepButton);
            wakeButton = FindViewById<Button>(Resource.Id.wakeupButton);

            // The following arrays contain data that is used for a custom firmware for
            // the Elatec TWN4 RFID reader. This code is included here to show how to
            // send data back to a USB serial device
            byte[] sleepdata = new byte[] { 0xf0, 0x04, 0x10, 0xf1 };
            byte[] wakedata = new byte[] { 0xf0, 0x04, 0x11, 0xf1 };

            sleepButton.Click += delegate
            {
                //WriteData(sleepdata);

                string message;
                
                message = "\nmPortNumber=";
                dumpTextView.Append(message);
                message = port.PortNumber.ToString();
                dumpTextView.Append(message);

                message = "\nDSR=";
                dumpTextView.Append(message);
                message = port.GetDSR().ToString();
                dumpTextView.Append(message);

                message = "\nRI=";
                dumpTextView.Append(message);
                message = port.GetRI().ToString();
                dumpTextView.Append(message);
                /*
                port.SetDTR(false);
                port.SetRTS(false);
                */
                message = "\nDTR=";
                dumpTextView.Append(message);
                message = port.GetDTR().ToString();
                dumpTextView.Append(message);

                message = "\nRTS=";
                dumpTextView.Append(message);
                message = port.GetRTS().ToString();
                dumpTextView.Append(message);
            };

            wakeButton.Click += delegate
            {
                //WriteData(wakedata);

                string message;
                message = "\nDSR=";
                dumpTextView.Append(message);
                message = port.GetDSR().ToString();
                dumpTextView.Append(message);

                message = "\nRI=";
                dumpTextView.Append(message);
                message = port.GetRI().ToString();
                dumpTextView.Append(message);
                /*
                port.SetDTR(true);
                port.SetRTS(true);
                */
                message = "\nDTR=";
                dumpTextView.Append(message);
                message = port.GetDTR().ToString();
                dumpTextView.Append(message);

                message = "\nRTS=";
                dumpTextView.Append(message);
                message = port.GetRTS().ToString();
                dumpTextView.Append(message);
            };
        }

        protected override void OnPause()
        {
            Log.Info(TAG, "OnPause");

            base.OnPause();

            if (serialIoManager != null && serialIoManager.IsOpen)
            {
                Log.Info(TAG, "Stopping IO manager ..");
                try
                {
                    serialIoManager.Close();
                }
                catch (Java.IO.IOException)
                {
                    // ignore
                }
            }
        }

        protected async override void OnResume()
        {
            Log.Info(TAG, "OnResume");

            base.OnResume();

            var portInfo = Intent.GetParcelableExtra(EXTRA_TAG) as UsbSerialPortInfo;
            int vendorId = portInfo.VendorId;
            int deviceId = portInfo.DeviceId;
            int portNumber = portInfo.PortNumber;

            Log.Info(TAG, string.Format("VendorId: {0} DeviceId: {1} PortNumber: {2}", vendorId, deviceId, portNumber));

            var drivers = await MainActivity.FindAllDriversAsync(usbManager);
            var driver = drivers.Where((d) => d.Device.VendorId == vendorId && d.Device.DeviceId == deviceId).FirstOrDefault();
            if (driver == null)
                throw new Exception("Driver specified in extra tag not found.");

            port = driver.Ports[portNumber];
            if (port == null)
            {
                titleTextView.Text = "No serial device.";
                return;
            }
            Log.Info(TAG, "port=" + port);

            titleTextView.Text = "Serial device: " + port.GetType().Name;

            serialIoManager = new SerialInputOutputManager(port)
            {
                BaudRate = 115200, // 2400, // 9600, // 
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = Parity.None,
            };
            serialIoManager.DataReceived += (sender, e) => {
                RunOnUiThread(() => {
                    UpdateReceivedData(e.Data);
                    
                    //WriteData(e.Data);
                });
            };
            serialIoManager.ErrorReceived += (sender, e) => {
                RunOnUiThread(() => {
                    var intent = new Intent(this, typeof(MainActivity));
                    StartActivity(intent);
                });
            };

            Log.Info(TAG, "Starting IO manager ..");
            try
            {
                serialIoManager.Open(usbManager);
            }
            catch (Java.IO.IOException e)
            {
                titleTextView.Text = "Error opening device: " + e.Message;
                return;
            }
        }
        public bool AUX()
        {
            return !port.GetDSR();
        }

        long Millis()
        {
            return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }

        int WriteData(byte[] data)
        {
            int offset = 0;
            if (serialIoManager.IsOpen)
            {
                if (false)
                {
                    port.Write(data, WRITE_WAIT_MILLIS);
                } 
                else
                {
                    int writeLength;
                    int amtWritten;
                    byte[] writeBuffer;

                    while (offset < data.Length)
                    {
                        writeLength = Math.Min(data.Length - offset, PACKET_SIZE);
                        writeBuffer = new byte[writeLength];
                        System.Buffer.BlockCopy(data, offset, writeBuffer, 0, writeLength);

                        while (!AUX())
                            ; // wait for ready

                        // alternative 1
                        // nothing to do
                        /*
                        // alternative 2
                        System.Threading.Thread.Sleep(75);
                        */
                        amtWritten = port.Write(writeBuffer, WRITE_WAIT_MILLIS);
                        if (amtWritten > 0)
                        {
                            
                            // alternative 1
                            while (AUX())
                                ; // wait for starting
                            /*
                            // alternative 2
                            long t = Millis();
                            while (AUX() && ((Millis() - t) < 50))
                                ; // wait for starting
                            */
                            offset += amtWritten;
                        }
                    }
                }
            }
            return offset;
        }

        void UpdateReceivedData(byte[] data)
        {
            //var message = "Read " + data.Length + " bytes: \n"
            //    + HexDump.DumpHexString(data) + "\n\n";

            // From byte array to string
            string message = System.Text.Encoding.UTF8.GetString(data, 0, data.Length);
            dumpTextView.Append(message);
            /*
            message = "\nDSR=";
            dumpTextView.Append(message);
            message = port.GetDSR().ToString();
            dumpTextView.Append(message);
            message = "\n";
            dumpTextView.Append(message);
            */
            scrollView.ScrollTo(0, dumpTextView.Bottom);

            String strStart = ">START";
            String strEnd = "END<";
            int Start = dumpTextView.Text.LastIndexOf(strStart);
            if (Start >= 0) 
            {
                int End = dumpTextView.Text.IndexOf(strEnd, Start);
                if ((End >= 0) && (Start < End))
                {
                    string strToSend = dumpTextView.Text.Substring(Start, End + strEnd.Length - Start);
                    //string strToSend = dumpTextView.Text[Start..(End + strEnd.Length - Start)];
                    dumpTextView.Text = strToSend + " read_len=" + strToSend.Length.ToString();
                    scrollView.ScrollTo(0, dumpTextView.Bottom);
                    int written = WriteData(System.Text.UTF8Encoding.UTF8.GetBytes(strToSend));
                    dumpTextView.Append(" written=" + written.ToString());
                    scrollView.ScrollTo(0, dumpTextView.Bottom);
                }
            }
        }
    }
}
