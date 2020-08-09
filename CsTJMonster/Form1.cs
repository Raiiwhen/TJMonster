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

/*指令集
 08 0a 0d 同步状态信息
 09 0a 0d 请求数据流(定长无头尾128bytes)
 0a 0a 0d 子窗体，开启连续发送(实时，无误码)
 0b 0a 0d 子窗体，关闭连续发送
 0c 0a 0d 清零积分指令
 0e ..... 128B 数据交换
 0f 0a 0d 下载PID 结构体
 10 0a 0d 上传PID 结构体
     */



namespace CsTJMonster
{
    public partial class Form1 : Form
    {
        /*变量区*/
        private SerialPort obj;//串口对象
        public List<byte> RX_Buffer;//接收缓存，在串口接收函数中赋值，在解析函数中重置。解析时先拷贝。
        public Series RX_stream;//chart数据的容器
        Color color_disable = Color.LightGray;//几种颜色定义
        Color color_enable = Color.PaleTurquoise;
        Color color_error = Color.Lime;
        int RX_RATE_CNTER;//定时器2使用的变量，用于统计数据上下行速率
        bool Stream_EN = false;//使能定时器1
        int err_cnt;
        byte[] PKG;
        Bitmap Stream_Vec;//绘图区的bitmap位图
        Graphics Stream_VecG;//上述bitmap位图的绘图板
        Form2 form_FFT;//子窗体，显示数据的傅里叶值
        Form3 form_PID;//子窗体，用于整定PID数值

        private enum frame { none, Status, Stream, pkg};
        private frame slv_frame;

        public Form1()
        {
            InitializeComponent();
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;
        }
        
        private void Form1_Load(object sender, EventArgs e)
        {
            /*变量初始化*/
            RX_Buffer = new List<byte>(1024);
            Series RX_stream = new Series("RX_stream");
            slv_frame = frame.none;
            err_cnt = 0;
            PKG = new byte[128];
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
            RX_stream.IsVisibleInLegend = false;
            RX_stream.Points.AddXY("raw", "0"); 
            chart1.Series.Add(RX_stream);
            chart1.ChartAreas[0].AxisY.Maximum = 32768;
            chart1.ChartAreas[0].AxisY.Minimum = -32768;
            /*绘图imagine初始化*/
            Stream_Vec = new Bitmap(pictureBox1.Width,pictureBox1.Height);
            Stream_VecG = Graphics.FromImage(Stream_Vec);
            Stream_VecG.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            /*串口扫描和初始化*/
            int valid_COM_cnt = COM_scan();
            groupBox2.Text = valid_COM_cnt.ToString() + " COMs";
            COM_open();
            if (obj.IsOpen)
            {
                /*开始工作*/
                timer1.Enabled = true;
                button_OpenCOM.BackColor = color_enable;
                button_OpenCOM.Text = "Close";
                button3.BackColor = color_disable;
                button3.Text = "数据流\r\n";
                Stream_EN = false;
            }
            else
            {
                timer1.Enabled = false;
                button_OpenCOM.BackColor = color_disable;
                button_OpenCOM.Text = "Open";
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
                /*分流处理*/
                if (obj.IsOpen && (RX_Buffer.Count() == 16) && (RX_Buffer[0] == 0xb1) && (RX_Buffer[1] == 0xb1))
                {
                    slv_frame = frame.Status;
                }
                else if (obj.IsOpen && (RX_Buffer.Count() == 128) && (RX_Buffer[0] == 0x0c) && (RX_Buffer[1] == 0x0c))
                {
                    slv_frame = frame.pkg;
                    PKG = RX_Buffer.ToArray();
                    RX_Buffer.Clear();
                }
                else if (obj.IsOpen && (RX_Buffer.Count() > 128))
                {
                    /*出现误码，删除0x0c 0x0c前的元素*/
                    int length = RX_Buffer.Count();
                    int frame_pos = 0;
                    for(int i = 0; i < length-1; i++)
                    {
                        if (RX_Buffer[i] == 0x0c && RX_Buffer[i + 1] == 0x0c)
                        {
                            frame_pos = i;
                        }
                    }
                    RX_Buffer.RemoveRange(0,frame_pos);
                    err_cnt++;
                }
            }
            catch
            {

            }
        }

        private void cput(string str)
        {
            if (textBox1.Text.Length != 0 && textBox1.Text[textBox1.Text.Length - 1] != '\n')
            {
                textBox1.AppendText("\r\n");
            }
            textBox1.AppendText(str);
        }

        private int COM_scan()
        {
            string[] com_list = new string[5] { "-", "-", "-", "-", "-", };

            string[] str = SerialPort.GetPortNames();
            Array.Sort(str);
            for (int i = 0; i < str.Count() && i < com_list.Count(); i++)
            {
                com_list[i] = str[i];
            }
            radioButton1.Text = com_list[0]; radioButton1.Enabled = com_list[0] == "-" ? false : true;
            radioButton2.Text = com_list[1]; radioButton2.Enabled = com_list[1] == "-" ? false : true;
            radioButton3.Text = com_list[2]; radioButton3.Enabled = com_list[2] == "-" ? false : true;
            radioButton4.Text = com_list[3]; radioButton4.Enabled = com_list[3] == "-" ? false : true;

            return str.Count();
        }

        private bool COM_open()
        {
            string current_port = "COM3";

            /*串口属性选择*/
            if (radioButton1.Checked) current_port = radioButton1.Text;
            if (radioButton2.Checked) current_port = radioButton2.Text;
            if (radioButton3.Checked) current_port = radioButton3.Text;
            if (radioButton4.Checked) current_port = radioButton4.Text;
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
                obj.DataReceived += new SerialDataReceivedEventHandler(COM_RxHandler);
                /*开串口*/
                obj.Open();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool COM_close()
        {
            try
            {
                obj.Close();

                return true;
            }
            catch
            {
                Console.WriteLine("Serial Close Error.");

                return false;
            }
        }

        /*显示RX_Buffer*/
        private void Buffer_Update()
        {
            string str = "00 ";

            try
            {
                byte[] watch_buffer = PKG;
                
                int cnt = watch_buffer.Length;
                /*label1显示全体RX_Buffer*/
                label1.Text = "";
                for (int i = 0; i < 128; i++)
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
                /*文本框显示对齐的内存区*/
                label10.Text = "";
                for (int i = 12; i < 108; i++)
                {
                    if (i < cnt)
                    {
                        str = BitConverter.ToString(BitConverter.GetBytes(watch_buffer[i]));
                        label10.Text += str.Substring(0, 2);
                        label10.Text += ' ';
                    }
                    else
                    {
                        label10.Text += "00 ";
                    }
                }
            }
            catch
            {

            }
        }

        /*429状态 0x08*/
        private void Status_cmd()
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

        private bool Status_update_label()
        {
            string tmp;
            List<byte> status_buffer = new List<byte>(RX_Buffer.ToArray());
            bool format_check = obj.IsOpen && (status_buffer.Count == 16) && (status_buffer[0] == 0xb1) && (status_buffer[1] == 0xb1);

            /*检查通过，清除串口缓存，开始分析和更新状态栏*/
            //RX_Buffer.Clear();
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
                    case 0x00: tmp = "NAND offline"; break;
                    case 0x01: tmp = "SAMSUNG K9 2GB " + (status_buffer[9] / 256).ToString() + '%'; break;
                    case 0x02: tmp = "SAMSUNG K9 4GB " + (status_buffer[9] / 256).ToString() + '%'; break;
                    default: tmp = "NAND error"; break;
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

        /*流交换 0x09*/
        private void Stream_cmd()
        {
            byte[] mst_echo = new byte[3] { 0x09, 0x0a, 0x0d };
            if (obj.IsOpen)
            {
                //RX_Buffer.Clear();
                obj.Write(mst_echo, 0, 3);
                cput("> data_stream\r\n");
            }
            else
            {
                cput("> stream error. Serial Closed.\r\n");
            }
        }

        private void Stream_update_chart()
        {
            Series RX_stream = new Series("RX_stream");
            /*不检查帧错误*/
            try
            {
                /*解码*/
                List<byte> chart_buffer = new List<byte>(RX_Buffer.ToArray());
                byte[] data = chart_buffer.ToArray();
                int[] tmp = new int[7];
                for (int i = 0; i < 7; i++)
                {
                    tmp[i] = (data[i * 2] * 256 + data[i * 2 + 1]);
                    tmp[i] = (tmp[i] < 32768) ? (tmp[i]) : (tmp[i] - 65536);
                }
                /*绘图*/
                chart1.Series[0].Points.Clear();
                chart1.Series.Clear();
                RX_stream.ChartType = SeriesChartType.Column;
                RX_stream.Points.AddXY("accx", tmp[0].ToString());
                RX_stream.Points.AddXY("accy", tmp[1].ToString());
                RX_stream.Points.AddXY("accz", tmp[2].ToString());
                RX_stream.Points.AddXY("gyrox", tmp[4].ToString());
                RX_stream.Points.AddXY("gyroy", tmp[5].ToString());
                RX_stream.Points.AddXY("gyroz", tmp[6].ToString());
                RX_stream.Points.AddXY("T", tmp[3].ToString());
                RX_stream.IsVisibleInLegend = false;
                chart1.Series.Add(RX_stream);
            }
            catch
            {
                Console.WriteLine("chart update error.");
            }
        }

        private void Stream_update_vectorBox()
        {
            try
            {
                /*未检查帧错误*/
                /*解码*/
                List<byte> vector_buffer = new List<byte>(RX_Buffer.ToArray());
                byte[] data = vector_buffer.ToArray();
                int[] raw = new int[3];
                double[] acc = new double[] { 0, 0, 0 };
                double deg = 0, abs = 0;

                for (int i = 0; i < 3; i++)
                {
                    raw[i] = data[2 * i] * 256 + data[2 * i + 1];
                    raw[i] = raw[i] > 32678 ? raw[i] - 65536 : raw[i];
                    acc[i] = Convert.ToDouble(raw[i]) / 56756 * Stream_Vec.Width;//矢量模长，归一化到画布高度的1/5
                }
                deg = System.Math.Atan(acc[0] / acc[1]);
                abs = System.Math.Sqrt(System.Math.Pow(acc[0], 2) + System.Math.Pow(acc[1], 2));//加速度的模（单位: 2.828 g）

                /*绘图*/
                Point sp, ep;
                Pen p = new Pen(Color.Red, 2);
                p.StartCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                p.EndCap = System.Drawing.Drawing2D.LineCap.RoundAnchor;
                sp = new Point(Stream_Vec.Width / 2, Stream_Vec.Height / 2);
                ep = sp + new Size(Convert.ToInt32(abs * System.Math.Cos(deg)), Convert.ToInt32(abs * System.Math.Sin(deg)));
                Stream_VecG.Clear(color_enable);
                Stream_VecG.DrawLine(p, ep, sp);

                pictureBox1.Image = Stream_Vec;
                groupBox3.Text = (abs * 2.828 * 9.8 / Stream_Vec.Width).ToString("F2");
            }
            catch
            {

            }
        }

        /*清零指令 0xc*/
        private void RST_cmd()
        {
            byte[] mst_echo = new byte[3] { 0x0c, 0x0a, 0x0d };
            if (obj.IsOpen)
            {
                obj.Write(mst_echo, 0, 3);
                cput("> 重设积分\r\n");
            }
            else
            {
                cput("> 重设失败，Serial Closed.\r\n");
            }
        }
        
        /*包交换 0x0e*/
        private bool PKG_cmd()
        {
            byte[] mst_echo = new byte[3] { 0x0c, 0x0a, 0x0d };
            if (obj.IsOpen)
            {
                obj.Write(mst_echo, 0, 3);
                return true;
            }
            else
            {
                cput("> Echo error. Serial Closed.\r\n");
                return false;
            }
        }

        private bool PKG_decode()
        {
            float temp = 0;
            int M2006_size = 24;
            uint id;
            short rpm;
            ushort mom, cmd, deg;
            byte[] M2006_struct = PKG.Skip(12).Take(M2006_size*4).ToArray();
            slv_frame = frame.none;

            temp = (float)(PKG[4] + PKG[5] * 256) / 100.0f;
            toolStripStatusLabel9.Text = temp.ToString("F1") + " ℃";
            temp = (float)(PKG[8] + PKG[9] * 256) / 100.0f;
            toolStripStatusLabel7.Text = temp.ToString("F1") + " V";

            id = M2006_struct[0];
            cmd = (ushort)(M2006_struct[2] << 8 | M2006_struct[1]);
            rpm = (short)(M2006_struct[4] << 8 | M2006_struct[3]);
            deg = (ushort)(M2006_struct[6] << 8 | M2006_struct[5]);
            mom = (ushort)(M2006_struct[8] << 8 | M2006_struct[7]);
            groupBox4.Text = "LF ID:";
            groupBox4.Text += id.ToString();
            label6.Text = cmd.ToString();
            label9.Text = rpm.ToString();
            label8.Text = deg.ToString();
            label7.Text = mom.ToString();


            groupBox6.Text = "LF ID:";
            groupBox6.Text += M2006_struct[M2006_size].ToString();


            groupBox7.Text = "LF ID:";
            groupBox7.Text += M2006_struct[M2006_size*2].ToString();


            groupBox8.Text = "LF ID:";
            groupBox8.Text += M2006_struct[M2006_size*3].ToString();



            return true;
        }

        private bool PKG_encode()
        {
            return true;
        }


        /*时基驱动逻辑*/
        private void timer1_Tick(object sender, EventArgs e)
        {
            /*流更新*/
            if (slv_frame == frame.Stream)
            {
                //Stream_update_chart();
                //Stream_update_vectorBox();
                /*绘制轨迹*/
                pictureBox1.Refresh();
            }
            /*更新状态栏*/
            if (slv_frame == frame.Status)
            {
                //Status_update_label();
            }
            /*包更新*/
            if (slv_frame == frame.pkg)
            {
                PKG_decode();


                /*更新缓冲区*/
                Buffer_Update();
                //RX_Buffer.Clear();
                toolStripStatusLabel13.Text = err_cnt.ToString();
            }

            /*发起数据请求*/
            if (Stream_EN)
            {
                Stream_cmd();
            }
            if (slv_frame == frame.none) PKG_cmd();
            /*清空发送区*/
            if (textBox1.Text.Length > 256)
            {
                textBox1.Text = "";
            }
        }

        private void rate_speed_Tick(object sender, EventArgs e)
        {

            RX_RATE_CNTER *= 10;
            if (RX_RATE_CNTER < 16)
            {
                toolStripStatusLabel11.Text = (RX_RATE_CNTER * 8).ToString() + " bps";
            }
            else if (RX_RATE_CNTER < 1024)
            {
                toolStripStatusLabel11.Text = (RX_RATE_CNTER).ToString() + " Bps";
            }
            else if (RX_RATE_CNTER < 1024 * 1024)
            {
                float rate_kBps = (float)RX_RATE_CNTER / 1024;
                toolStripStatusLabel11.Text = rate_kBps.ToString("F2") + " kBps";
            }
            RX_RATE_CNTER = 0;
        }

        /*按键驱动逻辑*/
        private void button_Scan_Click(object sender, EventArgs e)
        {
            int valid_COM_cnt = 0;
            valid_COM_cnt = COM_scan();
            groupBox2.Text = valid_COM_cnt.ToString() + " COMs";
        }
        
        private void button4_Click(object sender, EventArgs e)
        {
            PKG_cmd();
        }
        
        private void button3_Click(object sender, EventArgs e)
        {
            if (Stream_EN)
            {
                Stream_EN = false;
                button3.Text = "数据流";
                button3.BackColor = color_disable;
            }
            else
            {
                Stream_EN = true;
                button3.Text = "数据流\r\ning";
                button3.BackColor = color_enable;
            }
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (obj.IsOpen)
            {
                COM_close();
                button_OpenCOM.BackColor = color_disable;
                button_OpenCOM.Text = "Open";
                timer1.Enabled = false;
            }
            else
            {
                COM_open();
                if (obj.IsOpen)
                {
                    button_OpenCOM.BackColor = color_enable;
                    button_OpenCOM.Text = "Close";
                    timer1.Enabled = true;
                }
                else
                {
                    button_OpenCOM.BackColor = color_disable;
                    button_OpenCOM.Text = "Open Failed\r\nScan";
                    button_Scan.Enabled = true;
                    timer1.Enabled = false;
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (Status_update_label())
            {
                button_Sync.BackColor = color_enable;
            }
            else
            {
                button_Sync.BackColor = color_disable;
            }
            Status_cmd();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            RST_cmd();
        }
        
        public float get_data()
        {
            return 100;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            COM_close();
            button_OpenCOM.BackColor = color_disable;
            button_OpenCOM.Text = "Open";
            timer1.Enabled = false;

            form_FFT = new Form2(obj);
            form_FFT.ShowDialog();
        }
        private void button7_Click(object sender, EventArgs e)
        {
            COM_close();
            button_OpenCOM.BackColor = color_disable;
            button_OpenCOM.Text = "Open";
            timer1.Enabled = false;

            form_PID = new Form3(obj);
            form_PID.ShowDialog();
        }
    }
}
