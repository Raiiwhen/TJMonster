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
    public partial class Form1 : Form
    {
        private SerialPort obj;
        public List<byte> RX_Buffer;

        public Form1()
        {
            InitializeComponent();
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            RX_Buffer = new List<byte>(1024);
            comboBox1.Items.Add(115200);
            comboBox1.Items.Add(9600);
            comboBox1.Items.Add(230400);
            comboBox1.SelectedIndex = 0;
            scan_valid_serial();
            open_serial();
            status_ask();
        }

        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = obj;
            int bytes2read = sp.BytesToRead;
            byte[] buffer = new byte[bytes2read];

            sp.Read(buffer,0,bytes2read);
            RX_Buffer.AddRange(buffer);

            textBox1.Text += BitConverter.ToString(buffer);
        }

        private void scan_valid_serial()
        {
            string[] com_list = new string[5];
            string buffer;
            int valid_cnt = 0;
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    buffer = "COM" + i.ToString();
                    SerialPort list_port = new SerialPort();
                    list_port.PortName = buffer;
                    list_port.Open();
                    list_port.Close();
                    Console.WriteLine(buffer);
                    if (valid_cnt < 5)
                    {
                        com_list[valid_cnt++] = buffer;
                    }
                }
                catch
                {
                    continue;
                }
            }
            radioButton1.Text = com_list[0]; radioButton1.Visible = com_list[0] == null ? false : true;
            radioButton2.Text = com_list[1]; radioButton2.Visible = com_list[1] == null ? false : true;
            radioButton3.Text = com_list[2]; radioButton3.Visible = com_list[2] == null ? false : true;
            radioButton4.Text = com_list[3]; radioButton4.Visible = com_list[3] == null ? false : true;
            radioButton5.Text = com_list[4]; radioButton5.Visible = com_list[4] == null ? false : true;
            if(com_list[0] == null)
            {
                label1.Visible = true;
            }
            else
            {
                label1.Visible = false;
            }

        }

        private void open_serial()
        {
            string current_port = "COM3";

            /*串口属性选择*/
            if (radioButton1.Checked) current_port = radioButton1.Text;
            if (radioButton2.Checked) current_port = radioButton2.Text;
            if (radioButton3.Checked) current_port = radioButton3.Text;
            if (radioButton4.Checked) current_port = radioButton4.Text;
            if (radioButton5.Checked) current_port = radioButton5.Text;
            try
            {
                obj = new SerialPort(current_port);

                obj.BaudRate = int.Parse(comboBox1.Text);
                obj.Parity = Parity.None;
                obj.StopBits = StopBits.One;
                obj.DataBits = 8;
                obj.Handshake = Handshake.None;
                obj.RtsEnable = true;
                /*注册串口事件*/
                obj.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
                /*开串口*/
                obj.Open();
                button_OpenCOM.BackColor = Color.PaleTurquoise;
                button_OpenCOM.Text = "Close";
            }
            catch
            {
                button_OpenCOM.BackColor = Color.Lime;
                button_OpenCOM.Text = "Error";
                Console.WriteLine("Serial Error.");
            }
        }

        private void close_serial()
        {
            try
            {
                obj.Close();
                button_OpenCOM.BackColor = Color.LightGray;
                button_OpenCOM.Text = "Open";
            }
            catch
            {
                button_OpenCOM.BackColor = Color.Lime;
                button_OpenCOM.Text = "Error";
                Console.WriteLine("Serial Error.");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (button_OpenCOM.Text == "Close")
            {
                close_serial();
                scan_valid_serial();
            }
            else
            {
                scan_valid_serial();
                open_serial();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (status_update())
            {
                button_Sync.BackColor = Color.PaleTurquoise;
            }
            else
            {
                button_Sync.BackColor = Color.Lime;
            }
            status_ask();
        }

        private bool status_update()
        {
            string tmp;
            bool format_check = (RX_Buffer[0] == 0xb1) && (RX_Buffer[1] == 0xb1);
            if (format_check)
            {
                toolStripStatusLabel1.Text =
                    "20" + RX_Buffer[2].ToString() +
                    '/' + RX_Buffer[3].ToString() +
                    '/' + RX_Buffer[4].ToString() +
                    ' ' + RX_Buffer[5].ToString() +
                    ':' + RX_Buffer[6].ToString() +
                    " |";

                toolStripStatusLabel2.Text = (RX_Buffer[7] == 0x01 ? "MPU9250" : "IMU offline") + " |";

                switch (RX_Buffer[8])
                {
                    case 0x00: tmp = "NAND offline";break;
                    case 0x01: tmp = "SAMSUNG K9 2GB " + (RX_Buffer[9] / 256).ToString() + '%'; break;
                    case 0x02: tmp = "SAMSUNG K9 4GB " + (RX_Buffer[9] / 256).ToString() + '%'; break;
                    default: tmp = "NAND error";break;
                }
                toolStripStatusLabel3.Text = tmp + " |";

                switch (RX_Buffer[10])
                {
                    case 0x00: tmp = "SD offline"; break;
                    case 0x01: tmp = "SD Kingston" + (RX_Buffer[11] / 256).ToString() + '%'; break;
                    default: tmp = "SD error"; break;
                }
                toolStripStatusLabel4.Text = tmp + " |";

                switch (RX_Buffer[12])
                {
                    case 0x00: tmp = "W25 offline"; break;
                    case 0x01: tmp = "W25M02GV " + (RX_Buffer[13] / 256).ToString() + '%'; break;
                    default: tmp = "W25 error"; break;
                }
                toolStripStatusLabel5.Text = tmp;
            }
            RX_Buffer.Clear();
            return format_check;
        }

        private void status_ask()
        {
            byte[] mst_echo = new byte[3] { 0x08, 0x0a, 0x0d };
            obj.Write(mst_echo, 0, 3);
            textBox2.AppendText("echo_cmd\r\n");
        }

        private void data_stream()
        {
            byte[] mst_echo = new byte[3] { 0x09, 0x0a, 0x0d };
            obj.Write(mst_echo, 0, 3);
            textBox2.AppendText("data_stream\r\n");
        }



        private void button3_Click(object sender, EventArgs e)
        {
            data_stream();
        }

        private void button4_Click(object sender, EventArgs e)
        {

        }
    }
}
