using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UartGui
{
	/// <summary>
	/// FeedBackWindow.xaml 的交互逻辑
	/// </summary>
	public partial class FeedBackWindow : Window
	{
		public FeedBackWindow()
		{
			InitializeComponent();
		}

		private void Image_PreviewMouseDown(object sender, MouseButtonEventArgs e)//QQ图标点击事件
		{
			try
			{
				Process.Start("tencent://Message/?Uin=45213212");//调用QQ，打开临时会话
			}
			catch
			{
				MessageBox.Show("无法调用腾讯QQ！");
			}
		}

		private void Image_PreviewMouseDown_1(object sender, MouseButtonEventArgs e)//新浪微博图标点击事件
		{
			try//调用默认浏览器打开网页
			{
				//System.Diagnostics.Process.Start("explorer.exe", "http://weibo.com/lincolne");//使用资源管理器应该会自动转到默认浏览器
				RegistryKey key = Registry.ClassesRoot.OpenSubKey("http\\shell\\open\\command", false);
				String path = key.GetValue("").ToString();
				if (path.Contains("\""))
				{
					path = path.TrimStart('"');
					path = path.Substring(0, path.IndexOf('"'));
				}
				key.Close();
				Process.Start(path, "weibo.com/lincolne");
			}
			catch
			{
				MessageBox.Show("无法调用浏览器！");
			}

		}

		private void qq_MouseEnter(object sender, MouseEventArgs e)//QQ图标鼠标进入事件
		{
			qq.Margin = new Thickness(0, 0, 50, 25);//图标上升25px
			qq.Cursor = Cursors.Hand;//鼠标变为手型
		}

		private void qq_MouseLeave(object sender, MouseEventArgs e)
		{
			qq.Margin = new Thickness(0, 0, 50, 0);//图标下降25px
		}


		private void Image_MouseEnter(object sender, MouseEventArgs e)
		{
			weibo.Margin = new Thickness(0, 0, 50, 25);//图标上升25px
			weibo.Cursor = Cursors.Hand;//鼠标变为手型
		}

		private void Image_MouseLeave(object sender, MouseEventArgs e)
		{
			weibo.Margin = new Thickness(0, 0, 50, 0);//图标下降25px
		}

	}
}
