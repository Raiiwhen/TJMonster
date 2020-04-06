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
    public partial class Form2 : Form
    {
        SerialPort obj;
        List<byte> RX_Buffer;
        int RX_RATE_CNTER;

        public Form2(SerialPort father_COM)
        {
            InitializeComponent();
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;

            obj = new SerialPort();

            obj.PortName = father_COM.PortName;
            obj.BaudRate = father_COM.BaudRate;
            obj.Parity = Parity.None;
            obj.StopBits = StopBits.One;
            obj.DataBits = 8;
            obj.Handshake = Handshake.None;
            obj.RtsEnable = true;
            /*注册串口事件*/
            obj.DataReceived += new SerialDataReceivedEventHandler(COM_RxHandler);
        }
        
        private void Form2_Load(object sender, EventArgs e)
        {
            comboBox1.Items.Add("500");
            comboBox1.Items.Add("250");
            comboBox1.Items.Add("125");
            comboBox1.SelectedIndex = 0;
            RX_Buffer = new List<byte>(1024);
            try
            {
                obj.Open();
                if(obj.IsOpen)
                {
                    button1.Text = "停止";
                    comboBox1.Enabled = false;
                }
                else
                {
                    button1.Text = "错误";
                }
            }
            catch
            {
                button1.Text = "错误";
            }
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
            }
            catch
            {

            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            byte[] cmd = { 0x08,0x0a, 0x0d};
            obj.Write(cmd,0,cmd.Length);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            string str = "00 ";

            try
            {
                byte[] watch_buffer = RX_Buffer.ToArray();
                int cnt = watch_buffer.Length;
                label2.Text = "";
                for (int i = 0; i < 128; i++)
                {
                    if (i < cnt)
                    {
                        str = BitConverter.ToString(BitConverter.GetBytes(watch_buffer[i]));
                        label2.Text += str.Substring(0, 2);
                        label2.Text += ' ';
                    }
                    else
                    {
                        label2.Text += "00 ";
                    }
                }
            }
            catch
            {

            }

        }
    }
}
