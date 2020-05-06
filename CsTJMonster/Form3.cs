using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;

namespace CsTJMonster
{
    public partial class Form3 : Form
    {
        private SerialPort obj;
        private List<byte> RX_Buffer;
        private List<byte> TX_Buffer;
        private int RX_RATE_CNTER, err_cnt;
        private byte[] PKG;
        public Form3(SerialPort father_port)
        {
            InitializeComponent();

            obj = new SerialPort();
            obj.PortName = father_port.PortName;
            obj.BaudRate = father_port.BaudRate;
            obj.Parity = father_port.Parity;
            obj.StopBits = father_port.StopBits;
            obj.DataBits = father_port.DataBits;
            obj.Handshake = father_port.Handshake;
            obj.RtsEnable = father_port.RtsEnable;
            /*注册串口事件*/
            obj.DataReceived += new SerialDataReceivedEventHandler(COM_RxHandler);
        }

        private void Form3_Load(object sender, EventArgs e)
        {
            PKG = new byte[128];
            obj.Open();
        }


        /*串口操作与数据收发函数*/
        private void COM_RxHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = obj;
            byte[] buffer = new byte[sp.BytesToRead];
            try
            {
                sp.Read(buffer, 0, sp.BytesToRead);
                RX_Buffer.AddRange(buffer);
                RX_RATE_CNTER += buffer.Length;
                /*分流处理*/
                if (obj.IsOpen && (RX_Buffer.Count() == 128) && (RX_Buffer[0] == 0x0c) && (RX_Buffer[1] == 0x0c))
                {
                    PKG = RX_Buffer.ToArray();
                    RX_Buffer.Clear();
                }
                else if (obj.IsOpen && (RX_Buffer.Count() > 128))
                {
                    /*出现误码，删除0x0c 0x0c前的元素*/
                    int length = RX_Buffer.Count();
                    int frame_pos = 0;
                    for (int i = 0; i < length - 1; i++)
                    {
                        if (RX_Buffer[i] == 0x0c && RX_Buffer[i + 1] == 0x0c)
                        {
                            frame_pos = i;
                        }
                    }
                    RX_Buffer.RemoveRange(0, frame_pos);
                    err_cnt++;
                }
            }
            catch
            {

            }
        }
    }
}
