//
// Copyright (c) 2017 Equine Smart Bits, LLC. All rights reserved

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Hoho.Android.UsbSerial.Driver;
using Hoho.Android.UsbSerial.Util;
using System.Globalization;
using System.Threading;

[assembly: UsesFeature ("android.hardware.usb.host")]

namespace ESB
{
	[Activity (Label = "@string/app_name", MainLauncher = true, Icon = "@drawable/esblogo")]
	[IntentFilter (new[] { UsbManager.ActionUsbDeviceAttached })]
	[MetaData (UsbManager.ActionUsbDeviceAttached, Resource = "@xml/device_filter")]
	class MainActivity : Activity
	{
		static readonly string TAG = typeof(MainActivity).Name;
		const string ACTION_USB_PERMISSION = "com.hoho.android.usbserial.examples.USB_PERMISSION";

        string build_number = "0.812";

		UsbManager usbManager;
		ListView listView;
		TextView progressBarTitle;
		ProgressBar progressBar;
        Button buttonData;
        Button buttonChart;

        UsbSerialPortAdapter adapter;
		// BroadcastReceiver detachedReceiver;
		IUsbSerialPort selectedPort;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			SetContentView (Resource.Layout.Main);

            this.Title += " (ver " + build_number + ")";

			usbManager = GetSystemService(Context.UsbService) as UsbManager;
			listView = FindViewById<ListView>(Resource.Id.deviceList);
			progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar);
			progressBarTitle = FindViewById<TextView>(Resource.Id.progressBarTitle);
            buttonData = FindViewById<Button>(Resource.Id.button1);
            buttonChart = FindViewById<Button>(Resource.Id.button2);
        }

		protected override async void OnResume ()
		{
			base.OnResume ();

			adapter = new UsbSerialPortAdapter (this);
			listView.Adapter = adapter;

			listView.ItemClick += async (sender, e) => {
				await OnItemClick(sender, e);
			};

            buttonData.Click += async (sender, e) => {
                await OnButtonDataClicked(sender, e);
            };

            buttonChart.Click += async (sender, e) => {
                await OnButtonChartClicked(sender, e);
            };

            await PopulateListAsync();

			//register the broadcast receivers
			// detachedReceiver = new UsbDeviceDetachedReceiver (this);
			// RegisterReceiver(detachedReceiver, new IntentFilter(UsbManager.ActionUsbDeviceDetached));
		}

        async Task OnButtonDataClicked(object sender, EventArgs e)
        {
            // request user permisssion to connect to device
            // NOTE: no request is shown to user if permission already granted
            selectedPort = adapter.GetItem(0);
            var permissionGranted = await usbManager.RequestPermissionAsync(selectedPort.Driver.Device, this);
            if (permissionGranted)
            {
                // start the SerialConsoleActivity for this device
                var intendDataView = new Intent(this, typeof(DataViewActivity));
                intendDataView.PutExtra(DataViewActivity.EXTRA_TAG, new UsbSerialPortInfo(selectedPort));
                StartActivity(intendDataView);
            }
        }

        async Task OnButtonChartClicked(object sender, EventArgs e)
        {
            // request user permisssion to connect to device
            // NOTE: no request is shown to user if permission already granted
            selectedPort = adapter.GetItem(0);
            var permissionGranted = await usbManager.RequestPermissionAsync(selectedPort.Driver.Device, this);
            if (permissionGranted)
            {
                // start the SerialConsoleActivity for this device
                var intendChartView = new Intent(this, typeof(ChartViewActivity));
                intendChartView.PutExtra(ChartViewActivity.EXTRA_TAG, new UsbSerialPortInfo(selectedPort));
                StartActivity(intendChartView);
            }
        }

        protected override void OnPause ()
		{
			base.OnPause ();

			// unregister the broadcast receivers
			// var temp = detachedReceiver; // copy reference for thread safety
			// if(temp != null)
				// UnregisterReceiver (temp);
		}

		protected override void OnDestroy ()
		{
			base.OnDestroy ();

		}

		internal static Task<IList<IUsbSerialDriver>> FindAllDriversAsync(UsbManager usbManager)
		{
			// using the default probe table
			// return UsbSerialProber.DefaultProber.FindAllDriversAsync (usbManager);

			// adding a custom driver to the default probe table
			var table = UsbSerialProber.DefaultProbeTable;
			table.AddProduct(0x1b4f, 0x0008, Java.Lang.Class.FromType(typeof(CdcAcmSerialDriver))); // IOIO OTG
            table.AddProduct(0x1f00, 0x2012, Java.Lang.Class.FromType(typeof(CdcAcmSerialDriver))); // IOIO OTG
            var prober = new UsbSerialProber (table);
			return prober.FindAllDriversAsync (usbManager);
		}

		async Task OnItemClick(object sender, AdapterView.ItemClickEventArgs e)
		{
			Log.Info(TAG, "Pressed item " + e.Position);
			if (e.Position >= adapter.Count) {
				Log.Info(TAG, "Illegal position.");
				return;
			}

			// request user permisssion to connect to device
			// NOTE: no request is shown to user if permission already granted
			selectedPort = adapter.GetItem(e.Position);
			var permissionGranted = await usbManager.RequestPermissionAsync(selectedPort.Driver.Device, this);
			if(permissionGranted) {
				// start the SerialConsoleActivity for this device
				var intendLogView = new Intent (this, typeof(LogViewActivity));
                intendLogView.PutExtra (LogViewActivity.EXTRA_TAG, new UsbSerialPortInfo(selectedPort));
				StartActivity (intendLogView);
			}
		}

		async Task PopulateListAsync ()
		{
			ShowProgressBar ();

			Log.Info (TAG, "Refreshing device list ...");

			var drivers = await FindAllDriversAsync (usbManager);

			adapter.Clear ();
			foreach (var driver in drivers) {
				var ports = driver.Ports;
				Log.Info (TAG, string.Format ("+ {0}: {1} port{2}", driver, ports.Count, ports.Count == 1 ? string.Empty : "s"));
				foreach(var port in ports)
					adapter.Add (port);
			}

			adapter.NotifyDataSetChanged();
			progressBarTitle.Text = string.Format("{0} device{1} found", adapter.Count, adapter.Count == 1 ? string.Empty : "s");
			HideProgressBar();
			Log.Info(TAG, "Done refreshing, " + adapter.Count + " entries found.");

            if (adapter.Count == 1)
            {
                buttonData.Enabled = true;
                buttonChart.Enabled = true;
            }
            else
            {
                buttonData.Enabled = false;
                buttonChart.Enabled = false;
            }
		}

		void ShowProgressBar()
		{
			progressBar.Visibility = ViewStates.Visible;
			progressBarTitle.Text = GetString(Resource.String.refreshing);
		}

		void HideProgressBar()
		{
			progressBar.Visibility = ViewStates.Invisible;
		}

		#region UsbSerialPortAdapter implementation

		class UsbSerialPortAdapter : ArrayAdapter<IUsbSerialPort>
		{
			public UsbSerialPortAdapter(Context context)
				: base(context, global::Android.Resource.Layout.SimpleExpandableListItem2)
			{
			}

			public override View GetView (int position, View convertView, ViewGroup parent)
			{
				var row = convertView;
				if (row == null) {
					var inflater = Context.GetSystemService (Context.LayoutInflaterService) as LayoutInflater;
					row = inflater.Inflate (global::Android.Resource.Layout.SimpleListItem2, null);
				}

				var port = this.GetItem(position);
				var driver = port.Driver;
				var device = driver.Device;

				var title = string.Format ("Vendor {0} Product {1}",
					HexDump.ToHexString ((short)device.VendorId),
					HexDump.ToHexString ((short)device.ProductId));
				row.FindViewById<TextView> (global::Android.Resource.Id.Text1).Text = title;

				var subtitle = device.Class.SimpleName;
				row.FindViewById<TextView> (global::Android.Resource.Id.Text2).Text = subtitle;

				return row;
			}
		}

		#endregion

        /*
		#region UsbDeviceDetachedReceiver implementation

		class UsbDeviceDetachedReceiver
			: BroadcastReceiver
		{
			readonly string TAG = typeof(UsbDeviceDetachedReceiver).Name;
			readonly MainActivity activity;

			public UsbDeviceDetachedReceiver(MainActivity activity)
			{
				this.activity = activity;
			}

			public override void OnReceive (Context context, Intent intent)
			{
				var device = intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;

				Log.Info (TAG, "USB device detached: " + device.DeviceName);

				activity.PopulateListAsync ();
			}
		}

		#endregion
        */
	}
}


