/* *********************************************************
 * 串口调试助手。初始代码参考，在此感谢分享
 * http://blog.csdn.net/q45213212/article/details/35265773
 * *******************************************************
 * XAML部分基本没修改，CS部分有很大修改
 * 
 * */

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Management;
using Microsoft.Win32;
using System.IO;

namespace UartGui
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		/****************************************************************************
		 * 各种类型定义
		 ***************************************************************************/
		SerialPort ComPort = new SerialPort();				//串口基类
		List<customer> ComList = new List<customer>();      //可用串口列表，该列表不是一次性，会刷新
		Thread _ComSend = null;                             //线程还是不要局部变量了
		DispatcherTimer AutoSendTick = new DispatcherTimer();	//定时发送线程
		//各种标志位
		private struct Flag_t
		{
			public bool IsOpen;		//串口是否逻辑打开，注意和SerialPort.IsOpen的实际打开区别
			public bool WaitClose;	//invoke里判断是否正在关闭串口是否正在关闭串口，执行Application.DoEvents，并阻止再次invoke ,解决关闭串口时，程序假死，具体参见http://news.ccidnet.com/art/32859/20100524/2067861_4.html 仅在单线程收发使用，但是在公共代码区有相关设置，所以未用#define隔离
			public bool RecvSta;    //当前是否正在接收
			public bool Sending;	//当前线程是否正在发送中
		}
		private Flag_t UartFlag = new Flag_t();

		private struct SendArgv_t//发送数据线程传递参数的结构体格式
		{
			public string	data;		//发送的数据
			public bool		hex;		//发送模式,是否为16进制
		}
		//private SendArgv_t SendArgv = new SendArgv_t();//发送数据线程传递参数的结构体

		/****************************************************************************
		 * 串口配置类，用于combobox的下拉控件。
		 * Combobox的显示(DisplayMemberPath)类型是String,真实值SelectedValue类型是object
		 * 经过测试，校验位的显示值(Odd,Even)不能直接传入SerialPort，必须使用对应的enum
		 * 停止位使用(1,2)这种可以直接传入SerialPort，使用(One,Two)则不行
		 ***************************************************************************/
		internal class customer
		{
			public string com { get; set; }			//可用串口
			public string BaudRate { get; set; }		//波特率
			public string Dbits { get; set; }
			public Parity PbitsValue { get; set; }	
			public string Pbits { get; set; }
			public string Sbits { get; set; }
		}

#if false
		/****************************************************************************
		 * 功能：获取串口硬件名字
		 * 描述：
		 * 参数：
		 * 返回：
		 ***************************************************************************/
		public enum HardwareEnum
		{
			// 硬件
			Win32_Processor, // CPU 处理器
			Win32_PhysicalMemory, // 物理内存条
			Win32_Keyboard, // 键盘
			Win32_PointingDevice, // 点输入设备，包括鼠标。
			Win32_FloppyDrive, // 软盘驱动器
			Win32_DiskDrive, // 硬盘驱动器
			Win32_CDROMDrive, // 光盘驱动器
			Win32_BaseBoard, // 主板
			Win32_BIOS, // BIOS 芯片
			Win32_ParallelPort, // 并口
			Win32_SerialPort, // 串口
			Win32_SerialPortConfiguration, // 串口配置
			Win32_SoundDevice, // 多媒体设置，一般指声卡。
			Win32_SystemSlot, // 主板插槽 (ISA & PCI & AGP)
			Win32_USBController, // USB 控制器
			Win32_NetworkAdapter, // 网络适配器
			Win32_NetworkAdapterConfiguration, // 网络适配器设置
			Win32_Printer, // 打印机
			Win32_PrinterConfiguration, // 打印机设置
			Win32_PrintJob, // 打印机任务
			Win32_TCPIPPrinterPort, // 打印机端口
			Win32_POTSModem, // MODEM
			Win32_POTSModemToSerialPort, // MODEM 端口
			Win32_DesktopMonitor, // 显示器
			Win32_DisplayConfiguration, // 显卡
			Win32_DisplayControllerConfiguration, // 显卡设置
			Win32_VideoController, // 显卡细节。
			Win32_VideoSettings, // 显卡支持的显示模式。

			// 操作系统
			Win32_TimeZone, // 时区
			Win32_SystemDriver, // 驱动程序
			Win32_DiskPartition, // 磁盘分区
			Win32_LogicalDisk, // 逻辑磁盘
			Win32_LogicalDiskToPartition, // 逻辑磁盘所在分区及始末位置。
			Win32_LogicalMemoryConfiguration, // 逻辑内存配置
			Win32_PageFile, // 系统页文件信息
			Win32_PageFileSetting, // 页文件设置
			Win32_BootConfiguration, // 系统启动配置
			Win32_ComputerSystem, // 计算机信息简要
			Win32_OperatingSystem, // 操作系统信息
			Win32_StartupCommand, // 系统自动启动程序
			Win32_Service, // 系统安装的服务
			Win32_Group, // 系统管理组
			Win32_GroupUser, // 系统组帐号
			Win32_UserAccount, // 用户帐号
			Win32_Process, // 系统进程
			Win32_Thread, // 系统线程
			Win32_Share, // 共享
			Win32_NetworkClient, // 已安装的网络客户端
			Win32_NetworkProtocol, // 已安装的网络协议
			Win32_PnPEntity,//all device
		}
		/// <summary>
		/// WMI取硬件信息
		/// </summary>
		/// <param name="hardType"></param>
		/// <param name="propKey"></param>
		/// <returns></returns>
		public static string[] MulGetHardwareInfo(HardwareEnum hardType, string propKey)
		{

			List<string> strs = new List<string>();
			try
			{
				using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from " + hardType))
				{
					var hardInfos = searcher.Get();
					foreach (var hardInfo in hardInfos)
					{
						if (hardInfo.Properties[propKey].Value.ToString().Contains("COM"))
						{
							strs.Add(hardInfo.Properties[propKey].Value.ToString());
						}

					}
					searcher.Dispose();
				}
				return strs.ToArray();
			}
			catch
			{
				return null;
			}
			finally
			{ strs = null; }
		}
		//通过WMI获取COM端口
		string[] ss = MulGetHardwareInfo(HardwareEnum.Win32_PnPEntity, "Name");
#endif
		/****************************************************************************
		 * 功能：刷新当前可用串口，并添加到Combobox中
		 * 描述：
		 * 参数：
		 * 返回：存在串口返回true，不存在返回false
		 ***************************************************************************/
		private bool GetPort()
		{
			ComList.Clear();							//若不移除，会重复
			string[] port = SerialPort.GetPortNames();	//获取可用串口，static方法
			if(port.Length > 0)
			{
				for(int i=0; i<port.Length; i++)
				{
					ComList.Add(new customer() { com = port[i]});	//使用匿名方法添加串口列表
				}
				wpf_port.ItemsSource = ComList;         //资源路径
				wpf_port.DisplayMemberPath = "com";     //显示路径
				wpf_port.SelectedValuePath = "com";     //值路径
				wpf_port.SelectedIndex = 0;				//同上

				return true;
			}
			else
			{
				return false;
			}
		}

		/****************************************************************************
		 * 功能：当窗口初步生成时，发生
		 * 描述：
		 * 参数：
		 * 返回：
		 ***************************************************************************/
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			//↓↓↓↓↓↓↓↓↓可用串口下拉控件↓↓↓↓↓↓↓↓↓
			if(GetPort() == false)
			{
				MessageBox.Show("无可用串口！");
			}
			//↑↑↑↑↑↑↑↑↑可用串口下拉控件↑↑↑↑↑↑↑↑↑

			//↓↓↓↓↓↓↓↓↓波特率下拉控件↓↓↓↓↓↓↓↓↓
			List<customer> RateList = new List<customer>();
			string[] baudrate = { "1200", "2400", "4800", "9600", "14400", "19200", "28800", "38400", "57600", "115200" };
			for (int i=0; i<baudrate.Length; i++)
			{
				RateList.Add(new customer() { BaudRate = baudrate[i]});
			}
			wpf_baudrate.ItemsSource = RateList;
			wpf_baudrate.DisplayMemberPath = "BaudRate";
			wpf_baudrate.SelectedValuePath = "BaudRate";
			wpf_baudrate.SelectedIndex = 3;            //默认9600
			//↑↑↑↑↑↑↑↑↑波特率下拉控件↑↑↑↑↑↑↑↑↑

			//↓↓↓↓↓↓↓↓↓校验位下拉控件↓↓↓↓↓↓↓↓↓
			List<customer> ParityList = new List<customer>();
			ParityList.Add(new customer() { Pbits = "None" });
			ParityList.Add(new customer() { Pbits = "Odd" });
			ParityList.Add(new customer() { Pbits = "Even" });
			ParityList.Add(new customer() { Pbits = "Mark" });
			ParityList.Add(new customer() { Pbits = "Space" });
			wpf_parity.ItemsSource = ParityList;
			wpf_parity.DisplayMemberPath = "Pbits";
			wpf_parity.SelectedValuePath = "PbitsValue";
			wpf_parity.SelectedIndex = 0;
			//↑↑↑↑↑↑↑↑↑校验位下拉控件↑↑↑↑↑↑↑↑↑

			//↓↓↓↓↓↓↓↓↓数据位下拉控件↓↓↓↓↓↓↓↓↓
			List<customer> DataList = new List<customer>();
			DataList.Add(new customer() { Dbits = "8" });
			DataList.Add(new customer() { Dbits = "7" });
			DataList.Add(new customer() { Dbits = "6" });
			wpf_databit.ItemsSource = DataList;
			wpf_databit.DisplayMemberPath = "Dbits";
			wpf_databit.SelectedValuePath = "Dbits";
			wpf_databit.SelectedIndex = 0;
			//↑↑↑↑↑↑↑↑↑数据位下拉控件↑↑↑↑↑↑↑↑↑

			//↓↓↓↓↓↓↓↓↓停止位下拉控件↓↓↓↓↓↓↓↓↓
			List<customer> StopList = new List<customer>();
			StopList.Add(new customer() { Sbits = "1" });
			StopList.Add(new customer() { Sbits = "1.5" });
			StopList.Add(new customer() { Sbits = "2" });
			wpf_stopbit.ItemsSource = StopList;
			wpf_stopbit.DisplayMemberPath = "Sbits";
			wpf_stopbit.SelectedValuePath = "Sbits";
			wpf_stopbit.SelectedIndex = 0;
			//↑↑↑↑↑↑↑↑↑停止位下拉控件↑↑↑↑↑↑↑↑↑

			//↓↓↓↓↓↓↓↓↓其他默认设置↓↓↓↓↓↓↓↓↓
			ComPort.ReadTimeout = 8000;         //读超时8s
			ComPort.WriteTimeout = 8000;
			ComPort.ReadBufferSize = 1024;      //读数据缓存
			ComPort.WriteBufferSize = 1024;
			wpf_Send.IsEnabled = false;         //发送按钮默认不可用
			wpf_SendHex.IsChecked = true;       //16进制发送默认选中
			wpf_RecvHex.IsChecked = true;
			UartFlag.IsOpen = false;
			UartFlag.WaitClose = false;
			UartFlag.RecvSta = true;
			//↑↑↑↑↑↑↑↑↑其他默认设置↑↑↑↑↑↑↑↑↑
			ComPort.DataReceived += InterrputComRecvive;       //添加串口接收中断处理
			AutoSendTick.Tick += InterruptAutoSend;				//定时发送
		}

		/****************************************************************************
		* 功能：自动发送
		* 描述：
		* 参数：
		* 返回：
		***************************************************************************/
		private void InterruptAutoSend(object sender, EventArgs e)
		{
			send();	
		}

		/****************************************************************************
		* 功能：串口接收中断处理程序
		* 描述：中断是一个单独线程
		* 参数：
		* 返回：
		***************************************************************************/
		private void InterrputComRecvive(object sender, SerialDataReceivedEventArgs e)
		{
			//if (WaitClose) return;//如果正在关闭串口，则直接返回
			//Thread.Sleep(10);//发送和接收均为文本时，接收中为加入判断是否为文字的算法，发送你（C4E3），接收可能识别为C4,E3，可用在这里加延时解决
			if (UartFlag.RecvSta == true)//如果已经开启接收
			{
				byte[] RecBuf = null;//接收缓冲区
				try
				{
					RecBuf = new byte[ComPort.BytesToRead];				//接收数据缓存大小
					ComPort.Read(RecBuf, 0, RecBuf.Length);				//读取数据
				}
				catch
				{
					Dispatcher.BeginInvoke((ThreadStart)delegate ()
					{
						if (ComPort.IsOpen == false)//如果ComPort.IsOpen == false，说明串口已丢失
						{
							SetComLose();//串口丢失后相关设置
						}
						else
						{
							MessageBox.Show("无法接收数据，原因未知！");
						}
					}, null);
				}

				String RecData = Encoding.Default.GetString(RecBuf);//转码
				Dispatcher.BeginInvoke((ThreadStart)delegate ()
				{
					if (wpf_RecvHex.IsChecked == false)//接收模式为ASCII文本模式
					{
						wpf_RecvBox.Text += RecData;//加显到接收区
					}
					else
					{
						StringBuilder recBuffer16 = new StringBuilder();//定义16进制接收缓存
						for (int i = 0; i < RecBuf.Length; i++)
						{
							recBuffer16.AppendFormat("{0:X2}" + " ", RecBuf[i]);//X2表示十六进制格式（大写），域宽2位，不足的左边填0。
						}
						wpf_RecvBox.Text += recBuffer16.ToString();//加显到接收区
					}
					wpf_RecvCnt.Text = (Convert.ToInt32(wpf_RecvCnt.Text) + RecBuf.Length).ToString();//接收数据字节数
					wpf_RecvScroll.ScrollToBottom();//接收文本框滚动至底部
				});

			}
			else//暂停接收
			{
				ComPort.DiscardInBuffer();//清接收缓存
			}
		}
		/****************************************************************************
		* 功能：串口丢失或者关闭后的设置
		* 描述：重启打开相关控件使能
		* 参数：
		* 返回：
		***************************************************************************/
		private void SetAfterClose()
		{

			wpf_openCom.Content = "打开串口";						//按钮显示为“打开串口”
			OpenImage.Source = new BitmapImage(new Uri("Assets\\Off.png", UriKind.Relative));
			UartFlag.IsOpen = false;//串口状态设置为关闭状态
			wpf_Send.IsEnabled = false;
			wpf_reset.IsEnabled = true;
			wpf_port.IsEnabled = true;
			wpf_baudrate.IsEnabled = true;
			wpf_parity.IsEnabled = true;
			wpf_databit.IsEnabled = true;
			wpf_stopbit.IsEnabled = true;
		}
		private void SetComLose()
		{

			AutoSendTick.Stop();//串口丢失后要关闭自动发送
			wpf_AutoSend.IsChecked = false;//自动发送改为未选中
#if false
			WaitClose = true;//;//激活正在关闭状态字，用于在串口接收方法的invoke里判断是否正在关闭串口
			while (Listening)//判断invoke是否结束
			{
				DispatcherHelper.DoEvents(); //循环时，仍进行等待事件中的进程，该方法为winform中的方法，WPF里面没有，这里在后面自己实现
			}
#endif
			MessageBox.Show("串口已丢失");
			//WaitClose = false;//关闭正在关闭状态字，用于在串口接收方法的invoke里判断是否正在关闭串口
			GetPort();//刷新可用串口
			SetAfterClose();//成功关闭串口或串口丢失后的设置
		}

		/****************************************************************************
		* 功能：当指针位于该元素上，并按下任意按键时发生
		* 描述：刷新串口
		* 参数：
		* 返回：
		***************************************************************************/
		private void wpf_port_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			GetPort();
		}

		/****************************************************************************
		 * 功能：当下拉框选择的内容改变时，发生该事件
		 * 描述：
		 * 参数：
		 * 返回：
		 ***************************************************************************/
		private void wpf_port_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}
		/****************************************************************************
		 * 功能：文本框内容更改时发生
		 * 描述：
		 * 参数：
		 * 返回：
		 ***************************************************************************/
		private void wpf_Recv_TextChanged(object sender, TextChangedEventArgs e)
		{

		}
		/****************************************************************************
		 * 功能：下列均是，鼠标点击时，发生该事件
		 * 描述：
		 * 参数：
		 * 返回：
		 ***************************************************************************/
		private void wpf_reset_Click(object sender, RoutedEventArgs e)
		{
			wpf_baudrate.SelectedIndex = 3;
			wpf_parity.SelectedIndex = 0;
			wpf_databit.SelectedIndex = 0;
			wpf_stopbit.SelectedIndex = 0;
		}
		/****************************************************************************
		 * 打开串口/关闭串口
		 ***************************************************************************/
		private void wpf_openCom_Click(object sender, RoutedEventArgs e)
		{
			if (wpf_port.SelectedValue == null)//先判断是否有可用串口
			{
				MessageBox.Show("无可用串口，无法打开!");
				return;//没有串口，提示后直接返回
			}
#region 打开串口
			if (UartFlag.IsOpen == false)	//当前串口逻辑关闭，按钮功能为打开
			{

				try//尝试打开串口
				{
					ComPort.PortName = wpf_port.SelectedValue.ToString();					//串口,类型string
					ComPort.BaudRate = Convert.ToInt32(wpf_baudrate.SelectedValue);			//波特率，类型Int32
					ComPort.Parity = (Parity)Convert.ToInt32(wpf_parity.SelectedValue);		//校验位，类型Parity
					ComPort.DataBits = Convert.ToInt32(wpf_databit.SelectedValue);			//数据位,类型Int32
					ComPort.StopBits = (StopBits)Convert.ToDouble(wpf_stopbit.SelectedValue);//停止位，类型Stopbit                    
					ComPort.Open();			

				}
				catch//如果串口被其他占用，则无法打开
				{
					MessageBox.Show("无法打开串口,请检测此串口是否有效或被其他占用！");
					GetPort();														//刷新当前可用串口
					return;															//无法打开串口，提示后直接返回
				}

				//↓↓↓↓↓↓↓↓↓成功打开串口后的设置↓↓↓↓↓↓↓↓↓
				wpf_openCom.Content = "关闭串口";												//按钮显示改为“关闭按钮”
				OpenImage.Source = new BitmapImage(new Uri("Assets\\On.png", UriKind.Relative));//开关状态图片切换为ON
				UartFlag.IsOpen = true;                                                         //串口打开状态字改为true
				//	WaitClose = false;																//等待关闭串口状态改为false                
				//关闭某些按钮使能
				wpf_Send.IsEnabled = true;				//使能“发送数据”按钮
				wpf_reset.IsEnabled = false;//打开串口后失能重置功能
				wpf_port.IsEnabled = false;
				wpf_baudrate.IsEnabled = false;
				wpf_parity.IsEnabled = false;
				wpf_stopbit.IsEnabled = false;
				wpf_databit.IsEnabled = false;
				//↑↑↑↑↑↑↑↑↑成功打开串口后的设置↑↑↑↑↑↑↑↑↑


				if (wpf_AutoSend.IsChecked == true)//如果打开前，自动发送控件就被选中，则打开串口后自动开始发送数据
				{
					AutoSendTick.Interval = TimeSpan.FromMilliseconds(Convert.ToInt32(wpf_Time.Text));//设置自动发送间隔
					AutoSendTick.Start();//开启自动发送
				}
			}
#endregion
#region 关闭串口
			else//ComPortIsOpen == true,当前串口为打开状态，按钮事件为关闭串口
			{
				try//尝试关闭串口
				{
					//autoSendTick.Stop();//停止自动发送
					//autoSendCheck.IsChecked = false;//停止自动发送控件改为未选中状态
					ComPort.DiscardOutBuffer();											//清发送缓存
					ComPort.DiscardInBuffer();                                          //清接收缓存
#if false
					UartFlag.WaitClose = true;//激活正在关闭状态字，用于在串口接收方法的invoke里判断是否正在关闭串口
					while (Listening)//判断invoke是否结束
					{
						DispatcherHelper.DoEvents(); //循环时，仍进行等待事件中的进程，该方法为winform中的方法，WPF里面没有，这里在后面自己实现
					}
#endif
					ComPort.Close();//关闭串口
					//WaitClose = false;//关闭正在关闭状态字，用于在串口接收方法的invoke里判断是否正在关闭串口
					SetAfterClose();//成功关闭串口或串口丢失后的设置
				}

				catch//如果在未关闭串口前，串口就已丢失，这时关闭串口会出现异常
				{
					if (ComPort.IsOpen == false)//判断当前串口状态，如果ComPort.IsOpen==false，说明串口已丢失
					{
						SetComLose();
					}
					else//未知原因，无法关闭串口
					{
						MessageBox.Show("无法关闭串口，原因未知！");
						return;//无法关闭串口，提示后直接返回
					}
				}
			}
#endregion
		}
		/****************************************************************************
		 * 清除接收器
		 ***************************************************************************/
		private void wpf_clrRecv_Click(object sender, RoutedEventArgs e)
		{
			wpf_RecvBox.Clear();
		}
		/****************************************************************************
		 * 暂停接收,数据暂停。实际上只是改变了透明度？？？
		 * 暂时不明白
		 ***************************************************************************/
		private void wpf_stopRecv_Click(object sender, RoutedEventArgs e)
		{
			if (UartFlag.RecvSta == true)//当前为开启接收状态
			{
				UartFlag.RecvSta = false;//暂停接收
				wpf_stopRecv.Content = "开启接收";//按钮显示为开启接收
				recPrompt.Visibility = Visibility.Visible;//显示已暂停接收提示
				wpf_RecvBorder.Opacity = 0;//接收区透明度改为0
			}
			else//当前状态为关闭接收状态
			{
				UartFlag.RecvSta = true;//开启接收
				wpf_stopRecv.Content = "暂停接收";//按钮显示状态改为暂停接收
				recPrompt.Visibility = Visibility.Hidden;//隐藏已暂停接收提示
				wpf_RecvBorder.Opacity = 0.4;////接收区透明度改为0.4
			}
		}
		/****************************************************************************
		 * 发送数据，跨线程获取参数时，可以使用带参数线程传递数据，也可使用线程自己访问数据
		 * 这里需要传递数据，但线程的运行不是即时的，生成线程时的数据和线程自己访问的数据
		 * 有可能不一致。不过这个时间应该很短。不要在发送的同时立刻改变发送数据，问题就不大。
		 * 这里还是采用了带参数的线程
		 ***************************************************************************/
		private void wpf_Send_Click(object sender, RoutedEventArgs e)
		{
			send();
		}
		private void send()
		{
			if (UartFlag.Sending == true) return;                         //如果当前正在发送，则取消本次发送，本句注释后，可能阻塞在ComSend的lock处
			SendArgv_t SendArgv = new SendArgv_t();
			SendArgv.data = wpf_SendBox.Text;
			SendArgv.hex = (bool)wpf_SendHex.IsChecked;
			_ComSend = new Thread(new ParameterizedThreadStart(ComSend));         //new发送线程,不带参数
			_ComSend.Start(SendArgv);
		}

		//参数必须是object
		private void ComSend(object obj)
		{
			lock (this)//由于send()中的if (Sending == true) return，所以这里不会产生阻塞，如果没有那句，多次启动该线程，会在此处排队
			{
				SendArgv_t argv = (SendArgv_t)obj;          //转换类型，用于提取
				UartFlag.Sending = true;
				byte[] buf = null;                  //发送数据缓冲区
				string data = argv.data;                            //获取发送框中的数据
				bool hex = argv.hex;                        //发送模式，true表示16进制发送

				if (hex == true)     //16进制发送
				{
					try                 //尝试将发送的数据转为16进制Hex
					{
						data = data.Replace(" ", "");//去除16进制数据中所有空格
						data = data.Replace("\r", "");//去除16进制数据中所有换行
						data = data.Replace("\n", "");//去除16进制数据中所有换行
						if (data.Length == 1)//数据长度为1的时候，在数据前补0
						{
							data = "0" + data;
						}
						else if (data.Length % 2 != 0)//数据长度为奇数位时，去除最后一位数据
						{
							data = data.Remove(data.Length - 1, 1);
						}

						List<string> sendData16 = new List<string>();//将发送的数据，2个合为1个，然后放在该缓存里 如：123456→12,34,56
						for (int i = 0; i < data.Length; i += 2)
						{
							sendData16.Add(data.Substring(i, 2));
						}
						buf = new byte[sendData16.Count];//sendBuffer的长度设置为：发送的数据2合1后的字节数
						for (int i = 0; i < sendData16.Count; i++)
						{
							buf[i] = (byte)(Convert.ToInt32(sendData16[i], 16));//发送数据改为16进制
						}
					}
					catch //无法转为16进制时，出现异常
					{

						//跨线程访问数据，异步模式
						Dispatcher.BeginInvoke((ThreadStart)delegate ()
					   {
						   MessageBox.Show("请输入正确的16进制数据");
					   }, null);

						UartFlag.Sending = false;//关闭正在发送状态
						_ComSend.Abort();//终止本线程
						return;//输入的16进制数据错误，无法发送，提示后返回  
					}
				}
				else //ASCII码文本发送
				{
					buf = Encoding.Default.GetBytes(data);//转码,ascii转16进制？？？
					//buf = Convert.ToChar(data)
				}
				try                                             //尝试发送数据
				{                                               //如果发送字节数大于1000，则每1000字节发送一次
					int SendTimes = (buf.Length / 1000);//发送次数

					for (int i = 0; i < SendTimes; i++)//每次发送1000Bytes
					{
						ComPort.Write(buf, i * 1000, 1000);//发送sendBuffer中从第i * 1000字节开始的1000Bytes
														   //跨线程访问数据，异步模式
						Dispatcher.BeginInvoke((ThreadStart)delegate ()
						{
							wpf_SendCnt.Text = (Convert.ToInt32(wpf_SendCnt.Text) + 1000).ToString();//刷新发送字节数,一次+1000
						}, null);
					}
					if (buf.Length % 1000 != 0)         //发送字节小于1000Bytes或上面发送剩余的数据
					{
						ComPort.Write(buf, SendTimes * 1000, buf.Length % 1000);
						Dispatcher.BeginInvoke((ThreadStart)delegate ()
						{
							wpf_SendCnt.Text = (Convert.ToInt32(wpf_SendCnt.Text) + buf.Length % 1000).ToString();//刷新发送字节数
						}, null);
					}
				}
				catch//如果无法发送，产生异常
				{
					Dispatcher.BeginInvoke((ThreadStart)delegate ()
					{
						if (ComPort.IsOpen == false)//如果ComPort.IsOpen == false，说明串口已丢失
						{
							SetComLose();//串口丢失后的设置
						}
						else
						{
							MessageBox.Show("无法发送数据，原因未知！");
						}
					}, null);
				}
				//sendScrol.ScrollToBottom();//发送数据区滚动到底部
				UartFlag.Sending = false;//关闭正在发送状态
				_ComSend.Abort();//终止本线程
			}
		}
		/****************************************************************************
		 * 清除发送
		 ***************************************************************************/
		private void wpf_ClrSend_Click(object sender, RoutedEventArgs e)
		{
			wpf_SendBox.Clear();
		}

		/****************************************************************************
		 * 清除计数值
		 ***************************************************************************/
		private void wpf_ClrCount_Click(object sender, RoutedEventArgs e)
		{
			wpf_SendCnt.Text = "0";
			wpf_RecvCnt.Text = "0";
		}

		private void wpf_SaveNew_Click(object sender, RoutedEventArgs e)
		{
			if (wpf_RecvBox.Text == string.Empty)//接收区数据为空
			{
				MessageBox.Show("接收区为空，无法保存！");
			}
			else
			{
				SaveFileDialog Save_fd = new SaveFileDialog();//调用系统保存文件窗口
				Save_fd.Filter = "TXT文本|*.txt";//文件过滤器
				if (Save_fd.ShowDialog() == true)//选择了文件
				{
					File.WriteAllText(Save_fd.FileName, wpf_RecvBox.Text);//写入新的数据
					File.AppendAllText(Save_fd.FileName, "\r\n------" + DateTime.Now.ToString() + "\r\n");//数据后面写入时间戳
					MessageBox.Show("保存成功！");
				}

			}
		}

		private void wpf_SaveOld_Click(object sender, RoutedEventArgs e)
		{
			if (wpf_RecvBox.Text == string.Empty)//接收区数据为空
			{
				MessageBox.Show("接收区为空，无法保存！");
			}
			else
			{
				OpenFileDialog Open_fd = new OpenFileDialog();//调用系统保存文件窗口
				Open_fd.Filter = "TXT文本|*.txt";//文件过滤器
				if (Open_fd.ShowDialog() == true)//选择了文件
				{
					File.AppendAllText(Open_fd.FileName, wpf_RecvBox.Text);//在打开文件末尾写入数据
					File.AppendAllText(Open_fd.FileName, "\r\n------" + DateTime.Now.ToString() + "\r\n");//数据后面写入时间戳
					MessageBox.Show("添加成功！");
				}
			}
		}

		private void wpf_Info_Click(object sender, RoutedEventArgs e)
		{
			InfoWindow info = new InfoWindow();//new关于窗口
			info.Owner = this;//赋予主窗口，子窗口打开后，再次点击主窗口，子窗口闪烁
			info.Show();//ShowDialog方式打开关于窗口
		}

		private void wpf_FeedBack_Click(object sender, RoutedEventArgs e)
		{
			FeedBackWindow feedBack = new FeedBackWindow();//new反馈窗口
			feedBack.Owner = this;//赋予主窗口，子窗口打开后，再次点击主窗口，子窗口闪烁
			feedBack.ShowDialog();//ShowDialog方式打开反馈窗口
		}

		private void wpf_AutoSend_Click(object sender, RoutedEventArgs e)
		{
			if (wpf_AutoSend.IsChecked == true && ComPort.IsOpen == true)//如果当前状态为开启自动发送且串口已打开，则开始自动发送
			{
				AutoSendTick.Interval = TimeSpan.FromMilliseconds(Convert.ToInt32(wpf_Time.Text));//设置自动发送间隔
				AutoSendTick.Start();//开始自动发送定时器
			}
			else//点击之前为开启自动发送状态，点击后关闭自动发送
			{
				AutoSendTick.Stop();//关闭自动发送定时器
			}
		}

		private void wpf_Time_LostFocus(object sender, RoutedEventArgs e)
		{

		}

		private void wpf_Time_KeyDown(object sender, KeyEventArgs e)
		{

		}

		private void wpf_Time_TextChanged(object sender, TextChangedEventArgs e)
		{

		}

		private void FileOpen(object sender, ExecutedRoutedEventArgs e)
		{
			OpenFileDialog open_fd = new OpenFileDialog();			//调用系统打开文件窗口
			open_fd.Filter = "TXT文本|*.txt";						//文件过滤器
			if (open_fd.ShowDialog() == true)						//选择了文件
			{
				wpf_SendBox.Text = File.ReadAllText(open_fd.FileName);//读TXT方法1 简单，快捷，为StreamReader的封装
				//StreamReader sr = new StreamReader(open_fd.FileName);//读TXT方法2 复杂，功能强大
				//sendTBox.Text = sr.ReadToEnd();//调用ReadToEnd方法读取选中文件的全部内容
				//sr.Close();//关闭当前文件读取流
			}
		}

		private void FileSave(object sender, ExecutedRoutedEventArgs e)
		{

		}

		private void Window_Closed(object sender, ExecutedRoutedEventArgs e)
		{
			
		}
	}
}
