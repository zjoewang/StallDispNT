//
// Copyright (c) 2017 Equine Smart Bits, LLC. All rights reserved

using System;
using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using Android.OS;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Xamarin.Android;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Usb;
using Android.Util;
using System.Linq;
using System.Text;
using System.Threading;
using Hoho.Android.UsbSerial.Driver;
using Hoho.Android.UsbSerial.Util;
using OxyPlot.Axes;

namespace ESB
{
    [Activity(Label = "@string/app_name", LaunchMode = LaunchMode.SingleTop)]
    public class ChartViewActivity : Activity
    {
        static readonly string TAG = typeof(ChartViewActivity).Name;

        public const string EXTRA_TAG = "PortInfo";

        private PlotView plotViewModel;
        public PlotModel MyModel { get; set; }

        UsbManager usbManager;
        IUsbSerialPort port;

        LineSeries seriesHR;
        LineSeries seriesSP;

        int curHR = -1;
        int curSP = -1;
        int curRawHR = -1;
        int curRawSP = -1;
        double curTemp = -1;
        long curTS = 0;

        string input_line;

        SerialInputOutputManager serialIoManager;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.ChartView);

            usbManager = GetSystemService(Context.UsbService) as UsbManager;

            plotViewModel = FindViewById<PlotView>(Resource.Id.plotViewModel);

            var plotModel1 = new PlotModel();

            plotModel1.PlotMargins = new OxyThickness(40, 40, 40, 40);
            plotModel1.Background = OxyColors.Black;
            plotModel1.TitleFontSize = 40;
            plotModel1.TitleColor = OxyColors.Green;
            plotModel1.SubtitleFontSize = 24;
            plotModel1.TextColor = OxyColors.White;

            var linearAxis1 = new LinearAxis();
            linearAxis1.MajorGridlineStyle = LineStyle.Solid;
            linearAxis1.MinorGridlineStyle = LineStyle.Dot;
            linearAxis1.Title = "HR";
            linearAxis1.Key = "HR";
            linearAxis1.Position = AxisPosition.Left;
            linearAxis1.Minimum = 10.0;
            linearAxis1.Maximum = 250.0;
            plotModel1.Axes.Add(linearAxis1);
            var linearAxis2 = new LinearAxis();
            linearAxis2.MajorGridlineStyle = LineStyle.Solid;
            linearAxis2.MinorGridlineStyle = LineStyle.Dot;
            linearAxis2.Position = AxisPosition.Right;
            linearAxis2.Title = "%SpO2";
            linearAxis2.Key = "SP";
            linearAxis2.Minimum = 50.0;
            linearAxis2.Maximum = 100.0;
            plotModel1.Axes.Add(linearAxis2);

            seriesHR = new LineSeries
            {
                Title = "Heart Rate (bpm)",
                Color = OxyColors.Magenta,
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                MarkerStroke = OxyColors.Magenta,
                MarkerFill = OxyColors.Magenta,
                YAxisKey = "HR",
                MarkerStrokeThickness = 1.0,
                StrokeThickness = 5
            };

            plotModel1.Series.Add(seriesHR);

            MyModel = plotModel1;
            plotViewModel.Model = MyModel;

            seriesSP = new LineSeries()
            {
                Title = "Oxygen Saturation Level (%)",
                Color = OxyColors.Yellow,
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                MarkerStroke = OxyColors.Yellow,
                MarkerFill = OxyColors.Yellow,
                YAxisKey = "SP",
                MarkerStrokeThickness = 1.0,
                StrokeThickness = 5
            };

            plotModel1.Series.Add(seriesSP);
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

            input_line = "";

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
                MyModel.Title = "No serial device.";
                return;
            }
            Log.Info(TAG, "port=" + port);

            MyModel.Title = "Serial device: " + port.GetType().Name;

            serialIoManager = new SerialInputOutputManager(port)
            {
                BaudRate = 9600,
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = Parity.None,
            };
            serialIoManager.DataReceived += (sender, e) => {
                RunOnUiThread(() => {
                    UpdateReceivedData(e.Data);
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
                Thread.Sleep(2000);
                byte[] cmd = Encoding.ASCII.GetBytes("  ");
                port.Write(cmd, 1000);
                Thread.Sleep(1000);
            }
            catch (Java.IO.IOException e)
            {
                MyModel.Title = "Error opening device: " + e.Message;
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
            double temp;
            bool calculated;

            ParseLog.GetData(line, out hr, out sp, out temp, out calculated);

            if (temp > 0.0)
            {
                curTemp = temp;
            }
            else if (calculated)
            {
                ++curTS;
                curHR = hr;
                curSP = sp;

                seriesHR.Points.Add(new DataPoint(curTS, curHR));
                seriesSP.Points.Add(new DataPoint(curTS, curSP));
            }
            else
            {
                curRawHR = hr;
                curRawSP = sp;
            }

            // Thnning data so we don't accumulate too much historical data
            if (seriesHR.Points.Count > 200)
            {
                seriesHR.Points.RemoveRange(0, 50);
                seriesSP.Points.RemoveRange(0, 50);
            }

            MyModel.Title = string.Format("HR = {0} bpm, SP = {1}%", curHR, curSP);
            MyModel.Subtitle= string.Format("T= {0}F, raw HR={1}, SP={2}", curTemp, curRawHR, curRawSP);
            MyModel.InvalidatePlot(true);
        }
    }
}
