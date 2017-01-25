//
// Copyright (c) 2017 Equine Smart Bits, LLC. All rights reserved

using System;
using System.Collections.Generic;
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
using Hoho.Android.UsbSerial.Util;
using System.Threading;

namespace ESB
{
	[Activity (Label = "@string/app_name", LaunchMode = LaunchMode.SingleTop)]			
	class LogViewActivity : Activity
	{
		static readonly string TAG = typeof(LogViewActivity).Name;

		public const string EXTRA_TAG = "PortInfo";

		IUsbSerialPort port;

		UsbManager usbManager;
		TextView titleTextView;
		TextView dumpTextView;
		ScrollView scrollView;

		SerialInputOutputManager serialIoManager;

		protected override void OnCreate (Bundle bundle)
		{
			Log.Info (TAG, "OnCreate");

			base.OnCreate (bundle);

			SetContentView (Resource.Layout.LogView);

			usbManager = GetSystemService(Context.UsbService) as UsbManager;
			titleTextView = FindViewById<TextView>(Resource.Id.demoTitle);
			dumpTextView = FindViewById<TextView>(Resource.Id.consoleText);
			scrollView = FindViewById<ScrollView>(Resource.Id.demoScroller);
		}

		protected override void OnPause ()
		{
			Log.Info (TAG, "OnPause");

			base.OnPause ();

			if (serialIoManager != null && serialIoManager.IsOpen) {
				Log.Info (TAG, "Stopping IO manager ..");
				try {
					serialIoManager.Close ();
				}
				catch (Java.IO.IOException) {
					// ignore
				}
			}
		}

		protected async override void OnResume ()
		{
			Log.Info (TAG, "OnResume");

			base.OnResume ();

			var portInfo = Intent.GetParcelableExtra(EXTRA_TAG) as UsbSerialPortInfo;
			int vendorId = portInfo.VendorId;
			int deviceId = portInfo.DeviceId;
			int portNumber = portInfo.PortNumber;

			Log.Info (TAG, string.Format("VendorId: {0} DeviceId: {1} PortNumber: {2}", vendorId, deviceId, portNumber));

			var drivers = await MainActivity.FindAllDriversAsync (usbManager);
			var driver = drivers.Where((d) => d.Device.VendorId == vendorId && d.Device.DeviceId == deviceId).FirstOrDefault();
			if(driver == null)
				throw new Exception ("Driver specified in extra tag not found.");

			port = driver.Ports [portNumber];
			if (port == null) {
				titleTextView.Text = "No serial device.";
				return;
			}
			Log.Info (TAG, "port=" + port);

			titleTextView.Text = "Serial device: " + port.GetType().Name;

			serialIoManager = new SerialInputOutputManager (port) {
				BaudRate = 9600,
				DataBits = 8,
				StopBits = StopBits.One,
				Parity = Parity.None,
			};
			serialIoManager.DataReceived += (sender, e) => {
				RunOnUiThread (() => {
					UpdateReceivedData (e.Data);
				});
			};
			serialIoManager.ErrorReceived += (sender, e) => {
				RunOnUiThread (() => {
					var intent = new Intent(this, typeof(MainActivity));
					StartActivity(intent);
				});
			};

			Log.Info (TAG, "Starting IO manager ..");
			try {
				serialIoManager.Open (usbManager);
                Thread.Sleep(2000);
                byte[] cmd = Encoding.ASCII.GetBytes("  ");
                port.Write(cmd, 1000);
                Thread.Sleep(1000);
            }
			catch (Java.IO.IOException e) {
				titleTextView.Text = "Error opening device: " + e.Message;
				return;
			}
		}

		void UpdateReceivedData(byte[] data)
		{
			/*var message = "Read " + data.Length + " bytes: \n"
				+ HexDump.DumpHexString (data) + "\n\n";
            dumpTextView.Append(message);*/
            string result = System.Text.Encoding.UTF8.GetString(data);

            dumpTextView.Append(result);
			scrollView.SmoothScrollTo(0, dumpTextView.Bottom);		
		}
	}
}

