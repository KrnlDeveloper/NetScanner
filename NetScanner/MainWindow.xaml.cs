using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace NetScanner
{
	public partial class MainWindow : Window
	{
		BackgroundWorker progressWorker = null;
		Ping ping = null;
		List<HostItem> hostList = null;

		[DllImport("Iphlpapi.dll", EntryPoint = "SendARP")]
		internal extern static UInt32 SendArp(UInt32 DestIpAddress, UInt32 SrcIpAddress, Byte[] MacAddress, ref UInt32 MacAddressLength);

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private PhysicalAddress GetMACFromNetworkComputer(IPAddress IpAddress)
		{
			Byte[] mac = new Byte[6];
			UInt32 len = (UInt32)mac.Length;
			Byte[] addressBytes = IpAddress.GetAddressBytes();

			UInt32 dest = ((UInt32)addressBytes[3] << 24) + ((UInt32)addressBytes[2] << 16) + ((UInt32)addressBytes[1] << 8) + ((UInt32)addressBytes[0]);

			if (SendArp(dest, 0, mac, ref len) != 0)
			{
				return null;
			}

			return new PhysicalAddress(mac);
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private String GetMACAsString(String IpAddress)
		{
			PhysicalAddress mac = this.GetMACFromNetworkComputer(IPAddress.Parse(IpAddress));

			String hostName = String.Empty;

			if (mac == null)
			{
				hostName = String.Empty;
			}
			else
			{
				StringBuilder macStr = new StringBuilder(); ;
				String macHex = mac.ToString();

				macStr.Append(macHex.Substring(0, 2) + ":");

				for (Int32 pos = 2; pos < 10; pos += 2)
				{
					macStr.Append(macHex.Substring(pos, 2) + ":");
				}

				macStr.Append(macHex.Substring(macHex.Length - 2, 2));

				hostName = macStr.ToString(); ;
			}

			return hostName;
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		public MainWindow()
		{
			InitializeComponent();

			this.ping = new Ping();

			this.InitProgressBar();
			this.InitBackgroundWorker();

			this.CloseButton.Click += CloseButton_Click;
			this.MinButton.Click += MinButton_Click;
			this.ScanButton.Click += ScanButton_Click;
			this.Closing += MainWindow_Closing;
			this.MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
			this.IpList.SelectionChanged += IpList_SelectionChanged;

			Uri iconUri = new Uri("pack://application:,,,/NetScanner.ico", UriKind.RelativeOrAbsolute);
			this.Icon = BitmapFrame.Create(iconUri);

			this.GetHostBaseAddress();

			this.hostList = new List<HostItem>();
			this.HostGrid.ItemsSource = this.hostList;
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void IpList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				this.IpEdit.Text = this.IpList.SelectedItem.ToString();
			}

			catch (Exception) { }
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.DragMove();
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void MainWindow_Closing(object sender, CancelEventArgs e)
		{
			if (this.progressWorker.IsBusy == true)
			{
				this.progressWorker.CancelAsync();
				System.Windows.Forms.MessageBox.Show("Scanning has been terminated!\nApplication will be closed.", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void MinButton_Click(object sender, RoutedEventArgs e)
		{
			this.WindowState = WindowState.Minimized;
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void InitProgressBar()
		{
			this.ScanProgressBar.Minimum = 0;
			this.ScanProgressBar.Maximum = 255;
			this.ScanProgressBar.Value = 0;

			this.TaskbarItemInfo = new TaskbarItemInfo();
			this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;
			this.TaskbarItemInfo.ProgressValue = 0;
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void InitBackgroundWorker()
		{
			this.progressWorker = new BackgroundWorker();
			this.progressWorker.WorkerReportsProgress = true;
			this.progressWorker.WorkerSupportsCancellation = true;

			this.progressWorker.DoWork += ProgressWorker_DoWork;
			this.progressWorker.RunWorkerCompleted += ProgressWorker_RunWorkerCompleted;
			this.progressWorker.ProgressChanged += ProgressWorker_ProgressChanged;
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void GetHostBaseAddress()
		{
			List<String> interfaces = new List<String>();

			try
			{
				var host = Dns.GetHostEntry(Dns.GetHostName());

				foreach (var ip in host.AddressList)
				{
					if (ip.AddressFamily == AddressFamily.InterNetwork)
					{
						String ipStr = ip.ToString();

						interfaces.Add(ipStr.Substring(0, ipStr.LastIndexOf('.')));
					}
				}
			}

			catch(Exception) { }

			this.IpList.ItemsSource = interfaces;

			if(interfaces.Count > 0)
			{
				this.IpEdit.Text = interfaces[0];
				this.IpList.SelectedIndex = 0;
			}

			else
			{
				this.IpEdit.Text = "192.168.1";
			}
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void ProgressWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			this.ScanProgressBar.Value = e.ProgressPercentage;

			this.TaskbarItemInfo.ProgressValue = (double)(e.ProgressPercentage) / (this.ScanProgressBar.Maximum - this.ScanProgressBar.Minimum);

			this.HostGrid.Items.Refresh();
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void ProgressWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				//MessageBox.Show(e.Error.Message);
			}

			else if (e.Cancelled)
			{
				this.ProgressLabel.Content = ("Scanning has been terminated!" + Environment.NewLine);
				this.ScanProgressBar.Value = 0;
			}

			else
			{
				this.ProgressLabel.Content = ("Scanning completed" + Environment.NewLine);
			}

			this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
			this.ScanButton.Content = "Scan";
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void ProgressWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = sender as BackgroundWorker;

			if (worker == null)
			{
				return;
			}

			String address = String.Empty;

			MethodInvoker getTextAction = delegate { address = this.IpEdit.Text + "."; };
			Dispatcher.Invoke(getTextAction);

			MethodInvoker actionStart = delegate { this.ProgressLabel.Content = ("Scanning started..." + Environment.NewLine); };
			Dispatcher.BeginInvoke(actionStart);

			for (Int32 i = 0; i < 256; i++)
			{
				try
				{
					if (this.progressWorker.CancellationPending)
					{
						e.Cancel = true;

						break;
					}

					String currentAddress = address + i.ToString();
					PingReply res = ping.Send(currentAddress, 100);

					if (res.Status == IPStatus.Success)
					{
						String hostName = String.Empty;

						try
						{
							hostName = Dns.GetHostEntry(currentAddress).HostName;
						}

						catch(Exception)
						{
							hostName = String.Empty;
						}

						finally
						{
							MethodInvoker findAction = delegate 
							{ 
								HostItem item = new HostItem();
								item.IP = currentAddress;
								item.Host = hostName;
								item.MAC = this.GetMACAsString(currentAddress);

								this.hostList.Add(item);
								this.HostGrid.Items.Refresh();
							};
							Dispatcher.BeginInvoke(findAction);
						}
					}
				}

				finally
				{
					worker.ReportProgress(i);
				}
			}
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private void ScanButton_Click(object sender, RoutedEventArgs e)
		{
			this.hostList.Clear();
			this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Normal;

			if (this.progressWorker.IsBusy != true)
			{
				Boolean isAddressCorrect = this.CheckBaseAddress(this.IpEdit.Text);

				if (isAddressCorrect == true)
				{
					this.progressWorker.RunWorkerAsync();

					this.ScanButton.Content = "Stop";
				}

				else
				{
					System.Windows.Forms.MessageBox.Show("Incorrect base IP address", "Incorrect address", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}

			else
			{
				this.progressWorker.CancelAsync();
			}
		}

		//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		private Boolean CheckBaseAddress(String BaseAddress)
		{
			try
			{
				if (BaseAddress.Length > 11)
				{
					return false;
				}

				String[] ipBytes = BaseAddress.Split('.');
				if (ipBytes.Length != 3)
				{
					return false;
				}

				foreach (var item in ipBytes)
				{
					Int32 valByte = Int32.Parse(item);
					if ((valByte < 0) || (valByte > 255))
					{
						return false;
					}
				}

				return true;
			}

			catch (Exception)
			{
				return false;
			}
		}
	}
}
