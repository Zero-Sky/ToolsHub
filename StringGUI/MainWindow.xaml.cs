/****************************************************************
 * -------------字符串点阵烧写工具-----------------
 * 16高点阵中，需要显示中英文字符串，本项目会根据一定格式将字符hex编码
 * 下发的单片机中
 * *************************************************************/

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
using CmdHZK;

namespace StringGUI
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
		SerialPort ComPort = new SerialPort();					//串口基类
		List<customer> ComList = new List<customer>();			//可用串口列表，该列表不是一次性，会刷新
		Thread _ComSend = null;									//线程还是不要局部变量了
		DispatcherTimer AutoSendTick = new DispatcherTimer();   //定时发送线程
		private byte[] SendStream;                              //有效数据流，长度是													
		private int SendOffset = 0;                             //发送偏移
		private int SendAll = 0;								//总数据段数

		//各种标志位
		private struct Flag_t
		{
			public bool IsOpen;     //串口是否逻辑打开，注意和SerialPort.IsOpen的实际打开区别
			public bool WaitClose;  //invoke里判断是否正在关闭串口是否正在关闭串口，执行Application.DoEvents，并阻止再次invoke ,解决关闭串口时，程序假死，具体参见http://news.ccidnet.com/art/32859/20100524/2067861_4.html 仅在单线程收发使用，但是在公共代码区有相关设置，所以未用#define隔离
			public bool RecvSta;    //当前是否正在接收
			public bool Sending;    //当前线程是否正在发送中
		}
		private Flag_t UartFlag = new Flag_t();

		private struct SendArgv_t//发送数据线程传递参数的结构体格式
		{
			public string data;     //发送的数据
			public bool hex;        //发送模式,是否为16进制
		}

		/****************************************************************************
		 * 串口配置类，用于combobox的下拉控件。
		 * Combobox的显示(DisplayMemberPath)类型是String,真实值SelectedValue类型是object
		 * 经过测试，校验位的显示值(Odd,Even)不能直接传入SerialPort，必须使用对应的enum
		 * 停止位使用(1,2)这种可以直接传入SerialPort，使用(One,Two)则不行
		 ***************************************************************************/
		internal class customer
		{
			public string com { get; set; }         //可用串口
			public string BaudRate { get; set; }        //波特率
			public string Dbits { get; set; }
			public Parity PbitsValue { get; set; }
			public string Pbits { get; set; }
			public string Sbits { get; set; }
		}
		/****************************************************************************
		 * 功能：刷新当前可用串口，并添加到Combobox中
		 * 描述：
		 * 参数：
		 * 返回：存在串口返回true，不存在返回false
		 ***************************************************************************/
		private bool GetPort()
		{
			ComList.Clear();                            //若不移除，会重复
			string[] port = SerialPort.GetPortNames();  //获取可用串口，static方法
			if (port.Length > 0)
			{
				for (int i = 0; i < port.Length; i++)
				{
					ComList.Add(new customer() { com = port[i] });  //使用匿名方法添加串口列表
				}
				w_port.ItemsSource = ComList;         //资源路径
				w_port.DisplayMemberPath = "com";     //显示路径
				w_port.SelectedValuePath = "com";     //值路径
				w_port.SelectedIndex = 0;             //同上

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
			if (GetPort() == false)
			{
				MessageBox.Show("当前无可用串口！");
			}
			//↑↑↑↑↑↑↑↑↑可用串口下拉控件↑↑↑↑↑↑↑↑↑

			//↓↓↓↓↓↓↓↓↓其他默认设置↓↓↓↓↓↓↓↓↓
			ComPort.BaudRate = 9600;
			ComPort.DataBits = 8;
			ComPort.Parity = Parity.None;
			ComPort.StopBits = StopBits.Two;
			ComPort.ReadTimeout = 8000;         //读超时8s
			ComPort.WriteTimeout = 8000;
			ComPort.ReadBufferSize = 1024;      //读数据缓存
			ComPort.WriteBufferSize = 1024;
			w_BtnSend.IsEnabled = false;         //发送按钮默认不可用

			UartFlag.IsOpen = false;
			UartFlag.WaitClose = false;
			UartFlag.RecvSta = true;
			//↑↑↑↑↑↑↑↑↑其他默认设置↑↑↑↑↑↑↑↑↑
			ComPort.DataReceived += InterrputComRecvive;       //添加串口接收中断处理
			AutoSendTick.Tick += InterruptAutoSend;             //定时发送
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
				byte[] data = argv.data;                            //获取发送框中的数据
				try                 //尝试将发送的数据转为16进制Hex
				{
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

						//跨线程访问数据，同步模式
						Dispatcher.Invoke((ThreadStart)delegate ()
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
					RecBuf = new byte[ComPort.BytesToRead];             //接收数据缓存大小
					ComPort.Read(RecBuf, 0, RecBuf.Length);             //读取数据
				}
				catch
				{
					Thread.Sleep(100);		//休眠一段时间后再继续接收？？？
				}

				RecvDataDeal(RecBuf);			//接收数据处理

			}
			else//暂停接收
			{
				ComPort.DiscardInBuffer();//清接收缓存
			}
		}

		private void w_BtnSend_Click(object sender, RoutedEventArgs e)
		{
			#region 打开串口
			if (UartFlag.IsOpen == false)   //当前串口逻辑关闭，按钮功能为打开
			{

				try//尝试打开串口
				{
					ComPort.PortName = w_port.SelectedValue.ToString();                   //串口,类型string                   
					ComPort.Open();

				}
				catch//如果串口被其他占用，则无法打开
				{
					MessageBox.Show("无法打开串口,请检测此串口是否有效或被其他占用！");
					GetPort();                                                      //刷新当前可用串口
					return;                                                         //无法打开串口，提示后直接返回
				}

				//↓↓↓↓↓↓↓↓↓成功打开串口后的设置↓↓↓↓↓↓↓↓↓
				UartFlag.IsOpen = true;                                                         //串口打开状态字改为true											       
				//↑↑↑↑↑↑↑↑↑成功打开串口后的设置↑↑↑↑↑↑↑↑↑
			}
			#endregion

			HZK MyHzk = new HZK();

			string str = "舒华欢迎您";

			byte[] data = MyHzk.GetStringData(str);
			MyHzk.SaveTxtFile("12.txt", data);
			Console.ReadKey();
		}
	}
}
