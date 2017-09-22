using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;


namespace CmdHZK
{
	//点阵取模类
	public class HZK
	{
		#region 私有代码

		private byte[,] MapFont = new byte[16, 16];    //用于保存点阵地图数据
													  
		private void ClrMapFont()						//清除MapFont的所有数据
		{
			for (int i = 0; i < 16; i++)
			{
				for (int j = 0; j < 16; j++)
				{
					MapFont[i, j] = 0;
				}
			}
		}


		//将字符后的编码存入map地图。未来有可能就使用bitmap类看看
		//0,0-----0,16
		//
		//
		//16,0----16,16
		private void DataToMap(byte[] Old)
		{
			ClrMapFont();       //每次调用前必须清除旧的map数据

			//将HZK的数据依次填入16*16的数组map内
			//MapFont[x,y]，x为横坐标，y为纵坐标，左上角是原点
			for (int j = 0; j < 32; j++)
			{
				if (j % 2 == 0)      //j为0/2/4...30，x位于0~7，y位于0~15
				{
					for (int i = 0; i < 8; i++)
					{
						if ((Old[j] & 0x80) == 0x80)
						{
							MapFont[i, j / 2] = 1;
						}
						else
						{
							MapFont[i, j / 2] = 0;
						}
						Old[j] <<= 1;
					}
				}
				else if (j % 2 == 1)    //j为1/3/5...31，x位于8~15，y位于0~15
				{
					for (int i = 0; i < 8; i++)
					{
						if ((Old[j] & 0x80) == 0x80)
						{
							MapFont[i + 8, j / 2] = 1;
						}
						else
						{
							MapFont[i + 8, j / 2] = 0;
						}
						Old[j] <<= 1;
					}
				}
			}
		}


		//返回修改过的数据
		//参数，调用全局变量MapFont
		//返回，修改过的数据byte[]
		private byte[] GetSH5906()
		{
			int x = 0;      //横坐标，左上角是原点，向右边生长
			int y = 0;      //纵坐标，向下边生长
			byte[] NewFont = new byte[32];
			//当x位于0~7时
			//若y<8，则x和NewFONT对应
			//若y>=8 ，则x+8和NewFont对应
			for (x = 0; x < 8; x++)
			{
				for (y = 0; y < 8; y++)
				{
					if (MapFont[x, y] == 1)
					{
						NewFont[x] |= (byte)(1 << (7 - y));
					}
				}
				for (y = 8; y < 16; y++)
				{
					if (MapFont[x, y] == 1)
					{
						NewFont[x + 8] |= (byte)(1 << (15 - y));
					}
				}
			}

			//当x位于8~15时
			//若y<8，则x+8和NewFONT对应
			//若y>=8 ，则x+16和NewFont对应
			for (x = 8; x < 16; x++)
			{
				for (y = 0; y < 8; y++)
				{
					if (MapFont[x, y] == 1)
					{
						NewFont[x + 8] |= (byte)(1 << (7 - y));
					}
				}
				for (y = 8; y < 16; y++)
				{
					if (MapFont[x, y] == 1)
					{
						NewFont[x + 16] |= (byte)(1 << (15 - y));
					}
				}
			}
			return NewFont;
		}


		//取得修改过的数据
		//首先将数据全部存入Map地图
		//然后调用对应型号的函数，通过对应的规则，转换出新的数据
		//以后可以在这里添加新的规则
		private byte[] GetDataNew(byte[] Old)
		{
			DataToMap(Old);
			return GetSH5906();
		}


		//取单个字符的数据,数据存入OldFont,HZK中字模格式如下
		//[0~~~1]
		//[2~~~3]
		//
		//[14~15]
		private byte[] GetDataCN(byte ah, byte al)
		{
			byte[,] MapFont = new byte[16, 16];    //用于保存点阵地图数据
			byte[] OldFont = new byte[32];        //用于保存HZK提取的数据      
			byte[] NewFont = new byte[32];          //新生成的数据
			int k = ((ah - 161) * 94 + (al - 161)) * 32;//计算区位码

			FileStream fs = new FileStream("Hzk16", FileMode.Open, FileAccess.Read);
			// BinaryReader r = new BinaryReader(fs);
			fs.Seek(k, SeekOrigin.Begin);
			fs.Read(OldFont, 0, 32);                //读出HZK中的数据
			// r.Close();
			fs.Close();

			NewFont = GetDataNew(OldFont);          //转换成我们需要的数据

			return NewFont;
		}

		/// 返回对应单个英文或者数字的字模
		/// 数字或者英文对应的ASCII码。包括空格,暂不包括标点
		/// 返回对应的16字节字模
		/// 问题是每一个字符都要开关一次文件，效率有点影响
		private byte[] GetDataEN(byte arr)
		{
			byte[] Rdata = new byte[16];        //存储返回值

			FileStream fs = new FileStream("ASCII16", FileMode.Open, FileAccess.Read);
			//BinaryReader r = new BinaryReader(fs);
			fs.Seek(arr * 16, SeekOrigin.Begin);            //取得偏移量
			fs.Read(Rdata, 0, 16);                          //读取16字节数据
															//r.Close();
			fs.Close();
			return Rdata;
		}
		#endregion

		/// 对字符串取模。可同时对中英文数字联合字符串取模
		/// 参数：需要取模的字符串
		/// 返回：取模的数据
		//public byte[] GetStringData(byte[] array)
		public byte[] GetStringData(string str)
		{
			//生成对应的编码,同时也可以保证中文转成2字节，英文1字节
			byte[] array = Encoding.Default.GetBytes(str);

			byte[] ReturnData = new byte[array.Length * 16];  //用于存储得到的取模数据
			byte[] OneCNData = new byte[32];                //用于单个中文的数据
			byte[] OneENData = new byte[16];                //用于单个英文的数据
			int offset = 0;                     //Return当前存储位置的末尾，下一个数据从该偏移开始存储

			for (int i = 0; i < array.Length; i++)
			{
				if ((array[i] & 0x80) == 0x80)
				{
					OneCNData = GetDataCN(array[i], array[i + 1]);
					i++;        //这里也加一次i，总共加了2次
								//ReturnData.
					for (int j = 0; j < 32; j++)
					{
						ReturnData[offset] = OneCNData[j];
						offset++;       //添加一次，偏移量指向下一个
					}
				}
				else
				{
					OneENData = GetDataEN(array[i]);
					for (int j = 0; j < 16; j++)
					{
						ReturnData[offset] = OneENData[j];
						offset++;       //添加一次，偏移量指向下一个
					}
				}
			}
			return ReturnData;
		}

		///将数据保存到txt文件中
		public void SaveTxtFile(string name, byte[] data)
		{
			FileStream fs = new FileStream(name, FileMode.OpenOrCreate);
			StreamWriter sw = new StreamWriter(fs);
			for (int i = 0; i < data.Length; i++)
			{
				if (i % 8 == 0) //0/8/16等前面加{
					sw.Write("{");
				sw.Write("0x" + data[i].ToString("X2"));

				if (i % 8 == 7)
					sw.Write("},\r\n");
				else
					sw.Write(", ");
			}
			sw.Close();
			fs.Close();
		}
	}
}
