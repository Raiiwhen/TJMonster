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

        float kP = 0, kP_gain = 1;
        float kI = 0, kI_gain = 1;
        float kD = 0, kD_gain = 1;
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
            numericUpDown5.Value = 50;
            numericUpDown6.Value = 50;
            numericUpDown11.Value = 50;
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            kP = kP_1.Value;    kI = kI_1.Value;    kD = kD_1.Value;
            kP *= 10;           kI *= 10;           kD *= 10;
            kP += kP_2.Value;   kI += kI_2.Value;   kD += kD_2.Value;
            kP *= 10;           kI *= 10;           kD *= 10;
            kP += kP_3.Value;   kI += kI_3.Value;   kD += kD_3.Value;
            kP *= 10;           kI *= 10;           kD *= 10;
            kP += kP_4.Value;   kI += kI_4.Value;   kD += kD_4.Value;
            kP *= kP_gain;      kI *= kI_gain;      kD *= kD_gain;

            groupBox1.Text = "kP = " + kP.ToString("G");
            groupBox3.Text = "kI = " + kI.ToString("G");
            groupBox4.Text = "kD = " + kD.ToString("G");
        }


        private void Form3_FormClosing(object sender, FormClosingEventArgs e)
        {
            obj.Close();
        }

        int last_data5 = 50;
        int last_data6 = 50;
        int last_data7 = 50;
        private void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDown5.Value > last_data5)
                kP_gain *= 10;
            else
                kP_gain /= 10;
            last_data5 = (int)numericUpDown5.Value;
        }
        private void numericUpDown6_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDown6.Value > last_data6)
                kI_gain *= 10;
            else
                kI_gain /= 10;
            last_data6 = (int)numericUpDown6.Value;
        }

        private void numericUpDown11_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDown11.Value > last_data7)
                kD_gain *= 10;
            else
                kD_gain /= 10;
            last_data7 = (int)numericUpDown11.Value;
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
