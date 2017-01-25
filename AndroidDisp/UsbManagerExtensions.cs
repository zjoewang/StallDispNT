//
// Copyright (c) 2017 Equine Smart Bits, LLC. All rights reserved

using System;
using System.Threading.Tasks;
using Android.Hardware.Usb;
using Android.App;
using Android.Content;
using System.Collections.Generic;
using System.Threading;

namespace ESB
{
	public static class UsbManagerExtensions
	{
		const string ACTION_USB_PERMISSION = "com.Hoho.Android.UsbSerial.Util.USB_PERMISSION";

		static readonly Dictionary<Tuple<Context, UsbDevice>, TaskCompletionSource<bool>> taskCompletionSources = 
			new Dictionary<Tuple<Context, UsbDevice>, TaskCompletionSource<bool>> ();

		public static Task<bool> RequestPermissionAsync(this UsbManager manager, UsbDevice device, Context context)
		{
			var completionSource = new TaskCompletionSource<bool>();

			var usbPermissionReceiver = new UsbPermissionReceiver (completionSource);
			context.RegisterReceiver(usbPermissionReceiver, new IntentFilter(ACTION_USB_PERMISSION));

			var intent = PendingIntent.GetBroadcast (context, 0, new Intent (ACTION_USB_PERMISSION), 0);
			manager.RequestPermission(device, intent);

			return completionSource.Task;
		}

		class UsbPermissionReceiver
			: BroadcastReceiver
		{
			readonly TaskCompletionSource<bool> completionSource;

			public UsbPermissionReceiver(TaskCompletionSource<bool> completionSource)
			{
				this.completionSource = completionSource;
			}

			public override void OnReceive (Context context, Intent intent)
			{
				var device = intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;
				var permissionGranted = intent.GetBooleanExtra (UsbManager.ExtraPermissionGranted, false);
				context.UnregisterReceiver(this);
				completionSource.TrySetResult (permissionGranted);
			}
		}
	}
}

