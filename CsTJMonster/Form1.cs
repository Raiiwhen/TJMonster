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
using System.Windows.Forms.DataVisualization.Charting;



namespace CsTJMonster
{
    public partial class Form1 : Form
    {
        private SerialPort obj;
        public List<byte> RX_Buffer;
        public Series RX_stream;
        Color color_disable = Color.LightGray;
        Color color_enable = Color.PaleTurquoise;
        Color color_error = Color.Lime;

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
            /*变量初始化*/
            RX_Buffer = new List<byte>(1024);
            Series RX_stream = new Series("RX_stream");
            /*波特率多选框comboBox初始话*/
            comboBox1.Items.Add(115200);
            comboBox1.Items.Add(9600);
            comboBox1.Items.Add(230400);
            comboBox1.SelectedIndex = 0;
            /*缓冲buffer区显示初始化*/
            label1.Text = new string(' ',128*3);
            /*数据表格初始化*/
            chart1.Series.Clear();
            RX_stream.ChartType = SeriesChartType.Column;
            RX_stream.Points.AddXY("accx", "65535");
            RX_stream.Points.AddXY("accy", "0");
            RX_stream.Points.AddXY("accz", "32768");
            RX_stream.Points.AddXY("gyrox", "225");
            RX_stream.Points.AddXY("gyroy", "8987");
            RX_stream.Points.AddXY("gyroz", "33443");
            RX_stream.Points.AddXY("T", "13221");
            chart1.Series.Add(RX_stream);
            /*串口扫描和初始化*/
            scan_valid_serial();
            open_serial();
            if (obj.IsOpen)
            {
                timer1.Enabled = true;
                button_OpenCOM.BackColor = color_enable;
                button_OpenCOM.Text = "Close";
                button5.BackColor = color_enable;
                status_ask();
            }
        }

        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = obj;
            int bytes2read = sp.BytesToRead;
            byte[] buffer = new byte[bytes2read];

            sp.Read(buffer,0,bytes2read);
            RX_Buffer.AddRange(buffer);
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
                    SerialPort scan_COM = new SerialPort();
                    scan_COM.PortName = buffer;
                    scan_COM.Open();
                    scan_COM.Close();
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
            radioButton1.Text = com_list[0]; radioButton1.Enabled = com_list[0] == null ? false : true;
            radioButton2.Text = com_list[1]; radioButton2.Enabled = com_list[1] == null ? false : true;
            radioButton3.Text = com_list[2]; radioButton3.Enabled = com_list[2] == null ? false : true;
            radioButton4.Text = com_list[3]; radioButton4.Enabled = com_list[3] == null ? false : true;
            radioButton5.Text = com_list[4]; radioButton5.Enabled = com_list[4] == null ? false : true;
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
            }
            catch
            {
                Console.WriteLine("Serial Open Error.");
            }
        }

        private void close_serial()
        {
            try
            {
                obj.Close();
            }
            catch
            {
                Console.WriteLine("Serial Close Error.");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (obj.IsOpen)
            {
                close_serial();
                scan_valid_serial();
                button_OpenCOM.BackColor = color_disable;
                button_OpenCOM.Text = "Open";
                button5.BackColor = color_disable;
                timer1.Enabled = false;
            }
            else
            {
                scan_valid_serial();
                open_serial();
                button_OpenCOM.BackColor = color_enable;
                button_OpenCOM.Text = "Close";
                button5.BackColor = color_enable;
                timer1.Enabled = true;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (status_update())
            {
                button_Sync.BackColor = color_enable;
            }
            else
            {
                button_Sync.BackColor = color_disable;
            }
            status_ask();
        }

        private void cput(string str)
        {
            if (textBox1.Text.Length!=0 && textBox1.Text[textBox1.Text.Length-1] != '\n')
            {
                textBox1.AppendText("\r\n");
            }
            textBox1.AppendText(str);
        }

        private bool status_update()
        {
            string tmp;
            List<byte> status_buffer = new List<byte>(RX_Buffer.ToArray());
            bool format_check = obj.IsOpen && (status_buffer.Count==16) && (status_buffer[0] == 0xb1) && (status_buffer[1] == 0xb1);

            /*检查通过，清除串口缓存，开始分析和更新状态栏*/
            RX_Buffer.Clear();
            if (format_check)
            {
                toolStripStatusLabel1.Text =
                    "20" + status_buffer[2].ToString() +
                    '/' + status_buffer[3].ToString() +
                    '/' + status_buffer[4].ToString() +
                    ' ' + status_buffer[5].ToString() +
                    ':' + status_buffer[6].ToString() +
                    " |";
                toolStripStatusLabel2.Text = (status_buffer[7] == 0x01 ? "MPU9250" : "IMU offline") + " |";

                switch (status_buffer[8])
                {
                    case 0x00: tmp = "NAND offline";break;
                    case 0x01: tmp = "SAMSUNG K9 2GB " + (status_buffer[9] / 256).ToString() + '%'; break;
                    case 0x02: tmp = "SAMSUNG K9 4GB " + (status_buffer[9] / 256).ToString() + '%'; break;
                    default: tmp = "NAND error";break;
                }
                toolStripStatusLabel3.Text = tmp + " |";

                switch (status_buffer[10])
                {
                    case 0x00: tmp = "SD offline"; break;
                    case 0x01: tmp = "SD Kingston" + (status_buffer[11] / 256).ToString() + '%'; break;
                    default: tmp = "SD error"; break;
                }
                toolStripStatusLabel4.Text = tmp + " |";

                switch (status_buffer[12])
                {
                    case 0x00: tmp = "W25 offline"; break;
                    case 0x01: tmp = "W25M02GV " + (status_buffer[13] / 256).ToString() + '%'; break;
                    default: tmp = "W25 error"; break;
                }
                toolStripStatusLabel5.Text = tmp;
            }
            return format_check;
        }

        private void status_ask()
        {
            byte[] mst_echo = new byte[3] { 0x08, 0x0a, 0x0d };
            if (obj.IsOpen)
            {
                obj.Write(mst_echo, 0, 3);
                cput("> echo_cmd\r\n");
            }
            else
            {
                cput("> Echo error. Serial Closed.\r\n");
            }
        }

        private void data_stream()
        {
            byte[] mst_echo = new byte[3] { 0x09, 0x0a, 0x0d };
            if (obj.IsOpen)
            {
                RX_Buffer.Clear();
                obj.Write(mst_echo, 0, 3);
                cput("> data_stream\r\n");
            }
            else
            {
                cput("> stream error. Serial Closed.\r\n");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            data_stream();
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            chart_update();
        }

        private void chart_update()
        {
            chart1.Series[0].Points.Clear();
            Series RX_stream = new Series("RX_stream");
            byte[] data = RX_Buffer.ToArray();

            if (data.Length!=0)
            {
                int[] tmp = new int[7] ;
                for(int i = 0; i < 7; i++)
                {
                    tmp[i] = (data[i*2] * 256 + data[i*2+1]);
                    tmp[i] = (tmp[i] < 32768) ? (tmp[i]) : (tmp[i] - 65536);
                }
                
                chart1.Series.Clear();
                RX_stream.ChartType = SeriesChartType.Column;
                RX_stream.Points.AddXY("accx", tmp[0].ToString());
                RX_stream.Points.AddXY("accy", tmp[1].ToString());
                RX_stream.Points.AddXY("accz", tmp[2].ToString());
                RX_stream.Points.AddXY("gyrox", tmp[3].ToString());
                RX_stream.Points.AddXY("gyroy", tmp[4].ToString());
                RX_stream.Points.AddXY("gyroz", tmp[5].ToString());
                RX_stream.Points.AddXY("T", tmp[6].ToString());
                RX_stream.Points.AddXY("max", "32768");
                RX_stream.Points.AddXY("min", "-32768");
                chart1.Series.Add(RX_stream);
            }
        }

        private void chart1_Click(object sender, EventArgs e)
        {
            chart1.BackColor = Color.Beige;
        }

        private void update_data_labels()
        {
            byte[] watch_buffer = RX_Buffer.ToArray();
            int cnt = watch_buffer.Length;
            string str = "00 ";

            label1.Text = "";
            for (int i= 0; i<128; i++)
            {
                if (i < cnt)
                {
                    str = BitConverter.ToString(BitConverter.GetBytes(watch_buffer[i]));
                    label1.Text += str.Substring(0, 2);
                    label1.Text += ' ';
                }
                else
                {
                    label1.Text += "00 ";
                }
            }
        }
        
        private void timer1_Tick(object sender, EventArgs e)
        {
            chart_update();
            update_data_labels();
            status_update();
            data_stream();
            if (textBox1.Text.Length > 256)
            {
                textBox1.Text = "";
            }
        }
        
    }
}
