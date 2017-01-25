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
	class DataViewActivity : Activity
	{
		static readonly string TAG = typeof(DataViewActivity).Name;

		public const string EXTRA_TAG = "PortInfo";

		IUsbSerialPort port;

		UsbManager usbManager;
		TextView hrTextView;
		TextView spTextView;
		TextView tempTextView;
		TextView rawhrTextView;
		TextView rawspTextView;

        string input_line;

		SerialInputOutputManager serialIoManager;

		protected override void OnCreate (Bundle bundle)
		{
			Log.Info (TAG, "OnCreate");

			base.OnCreate (bundle);

			SetContentView (Resource.Layout.DataView);

			usbManager = GetSystemService(Context.UsbService) as UsbManager;
			hrTextView = FindViewById<TextView>(Resource.Id.hr);
			spTextView = FindViewById<TextView>(Resource.Id.sp);
			tempTextView = FindViewById<TextView>(Resource.Id.temp);
			rawhrTextView = FindViewById<TextView>(Resource.Id.rawhr);
			rawspTextView = FindViewById<TextView>(Resource.Id.rawsp);
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

            input_line = "";

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
				tempTextView.Text = "No serial device.";
				return;
			}
			Log.Info (TAG, "port=" + port);

			tempTextView.Text = "Serial device: " + port.GetType().Name;

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
				tempTextView.Text = "Error opening device: " + e.Message;
				return;
			}
		}

		void UpdateReceivedData(byte[] data)
		{
            string result = System.Text.Encoding.UTF8.GetString(data);

            input_line += result;

            int count = result.Length;

            if (!result.EndsWith("\n"))
                return;

            string line = input_line;

            input_line = "";

            int hr, sp;
            double  temp;
            bool    calculated;

            ParseLog.GetData(line, out hr, out sp, out temp, out calculated);

            if (temp > 0.0)
            {
                tempTextView.Text = "Temp = " + temp.ToString() + "F";
            }
            else if (calculated)
            {
                hrTextView.Text = "HR = " + hr.ToString() + " bpm";
                spTextView.Text = "SP = " + sp.ToString() + "%";
            }
            else
            {
                rawhrTextView.Text = "raw HR = " + hr.ToString() + " bpm";
                rawspTextView.Text = "raw SP = " + sp.ToString() + "%";
            }
		}
	}
}

