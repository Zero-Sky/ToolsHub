using System;
using System.Collections.Generic;
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
	/// SaveModWindow.xaml 的交互逻辑
	/// </summary>
	public partial class SaveModWindow : Window
	{
		public SaveModWindow()
		{
			InitializeComponent();
		}
		public string mode { get; set; }//保存模式

		private void saveNewBtn(object sender, RoutedEventArgs e)//保存为新文件按钮click事件
		{
			mode = "new";//传递到主窗口
			DialogResult = true;//关闭该窗口
		}

		private void saveOldBtn(object sender, RoutedEventArgs e)//保存到已有文件按钮click事件
		{
			mode = "old";//传递到主窗口
			DialogResult = true;//关闭该窗口
		}

		private void Cancel(object sender, RoutedEventArgs e)//取消按钮click事件
		{
			mode = "cancle";//传递到主窗口
			DialogResult = true;//关闭该窗口
		}
	}
}
