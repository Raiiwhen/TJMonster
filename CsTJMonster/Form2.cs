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
using System.Threading;

namespace CsTJMonster
{
    public partial class Form2 : Form
    {
        SerialPort obj;
        /*数据分析*/
        List<byte> RX_Buffer;//串口缓存
        List<byte> sample_pkgs;//串口缓存->数据解析的中间容器
        List<float> fft_raw;//实时滑动的数据存储，包括最近4096个数据
        List<float> data_raw;//全体采样数据容器，随时间加宽
        int err_count;
        byte[] pkg;
        int RX_RATE_CNTER;
        /*UI绘图*/
        Bitmap draw_zone;
        Graphics g_rt;
        Plot rt;
        Pen p;
        Pen p_axis;
        Pen p_grid;
        Color color_disable = Color.LightGray;//几种颜色定义
        Color color_enable = Color.PaleTurquoise;
        Color color_error = Color.Lime;

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
            /*在2s内产生的数据*/
            RX_Buffer = new List<byte>(8192);
            sample_pkgs = new List<byte>(8192);
            fft_raw = new List<float>(8192);
            pkg = new byte[104];
            try
            {
                obj.Open();
                obj.DiscardInBuffer();
                obj.DiscardOutBuffer();
                RX_Buffer.Clear();
                byte[] shutdown = { 0x0b, 0x0a, 0x0d };
                obj.Write(shutdown, 0, shutdown.Length);
                Thread.Sleep(10);
                
                byte[] cmd = { 0x0a, 0x0a, 0x0d };
                obj.Write(cmd, 0, cmd.Length);
                timer1.Enabled = true;
                button1.Text = "停止采样";
                button1.BackColor = color_enable;
            }
            catch
            {
                button1.Text = "串口错误";
                button1.BackColor = color_error;
            }
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                /*关闭连续发送模式*/
                byte[] cmd = { 0x0b, 0x0a, 0x0d };
                obj.Write(cmd, 0, cmd.Length);
                obj.Close();
            }
            catch
            {

            }
        }

        private void Form2_Paint(object sender, PaintEventArgs e)
        {
            draw_zone = new Bitmap(pictureBox1.Width, pictureBox1.Height);
            g_rt = Graphics.FromImage(draw_zone);
            g_rt.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            rt = new Plot(new Point(10,10), new Size(512,256),32677,-32678);
            p = new Pen(Color.RoyalBlue,(float)0.3);
            p_grid = new Pen(Color.OrangeRed, 1);
            p_axis = new Pen(Color.Maroon, p_grid.Width+1);
            g_rt_frame(rt);
        }

        /*串口操作与数据收发函数*/
        private void COM_RxHandler(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort sp = obj;
                byte[] buffer = new byte[sp.BytesToRead];
                int bytes_rdy = 0;
                sp.Read(buffer, 0, sp.BytesToRead);
                RX_Buffer.AddRange(buffer);
                bytes_rdy = RX_Buffer.Count() - RX_Buffer.Count() % 104;
                sample_pkgs.AddRange(RX_Buffer.GetRange(0,bytes_rdy));
                RX_Buffer.RemoveRange(0,bytes_rdy);
                
                label3.Text = ((float)sample_pkgs.Count()/128).ToString("F2");

                //速率显示
                RX_RATE_CNTER += buffer.Length;
            }
            catch
            {
                Console.WriteLine("RX Error!");
            }
        }
        
        /*控件更新函数*/
        private void RX_Buffer_Update()
        {
            string str = "00 ";

            try
            {
                int cnt = pkg.Length;
                label2.Text = "";
                for (int i = 0; i < 128; i++)
                {
                    if (i < cnt)
                    {
                        str = BitConverter.ToString(BitConverter.GetBytes(pkg[i]));
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
                Console.WriteLine("buffer update error.");
            }
        }

        private void g_rt_frame(Plot obj)
        {
            int length = (int)(draw_zone.Size.Width - p_grid.Width);
            int height = (int)(draw_zone.Size.Height - p_grid.Width);
            g_rt.DrawLine(p_grid, 0,        0,          length,     0           );
            g_rt.DrawLine(p_grid, length,   0,          length,     height      );
            g_rt.DrawLine(p_axis, 1,        0,          1,          height-1    );
            g_rt.DrawLine(p_axis, 1,        height-1,   length,     height-1    );
            g_rt.DrawLine(p_grid, length,   0,          0,          height      );
            g_rt.DrawLine(p_grid, 0,        0,          length,     height      );
        }

        private void g_rt_data()
        {
            try
            {
                /*前一个点的y坐标*/
                int y_last = 0;
                /*确定画图的数据窗口*/
                int points_cnt = draw_zone.Width - 2;
                int index_start = fft_raw.Count() - points_cnt - 1;
                if (index_start < 0) index_start = 0;
                /*取数据*/
                List<float> tmp = fft_raw.GetRange(index_start, points_cnt);
                /*确定坐标轴的信息*/
                int y0 = (int)(draw_zone.Height / 2);
                float k = (float)draw_zone.Height / 4;
                /*清空重画*/
                g_rt.Clear(Color.LightGray);
                /*画图*/
                for (int x = 0; x < points_cnt; x++)
                {
                    int y = (int)(y0 + k * tmp[x]);
                    g_rt.DrawLine(p, x, (y_last == y ? y - 1 : y_last), x, y);
                    y_last = y;
                }
            }
            catch
            {

            }
        }

        /*时基与按键驱动逻辑*/
        private void button1_Click(object sender, EventArgs e)
        {
            if ( !obj.IsOpen )
                return;
            if (timer1.Enabled)
            {
                /*关闭下位机*/
                obj.DiscardInBuffer();
                byte[] cmd = { 0x0a, 0x0a, 0x0d };
                obj.Write(cmd, 0, cmd.Length);
                timer1.Enabled = false;
                timer3.Enabled = false;
                button1.Text = "开始采样";
                button1.BackColor = color_disable;
            }
            else
            {
                /*开启下位机*/
                RX_Buffer.Clear();
                sample_pkgs.Clear();
                fft_raw.Clear();
                Array.Clear(pkg, 0, 104);
                err_count = 0;
                label4.Text = "0";
                byte[] cmd = { 0x0a, 0x0a, 0x0d };
                obj.Write(cmd, 0, cmd.Length);
                timer1.Enabled = true;
                button1.Text = "停止采样";
                button1.BackColor = color_enable;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            timer3.Enabled = !timer3.Enabled;
            button2.Text = "绘图" + (timer3.Enabled?"开":"关");
        }

        /*数据解析 40Hz*/
        private void timer1_Tick(object sender, EventArgs e)
        {
            List<float> tmp = new List<float>(1024);
            float data;
            /*解析数据包，显示处理结果*/
            try
            {
                for (int cnt = 0; cnt<sample_pkgs.Count()/128; cnt++)
                {
                    pkg = sample_pkgs.GetRange(cnt * 128, 128).ToArray();
                    /*和校验*/
                    int RCC = 0;
                    for (int chk = 4; chk < 104; chk++) RCC += pkg[chk];
                    RCC %= 256;
                    if (RCC != pkg[2])
                    {
                        err_count++;
                        continue;
                    }
                    /*连续性校验*/
                    /*解码*/
                    for (int i= 0; i < 50; i++)
                    {
                        data = (float)(pkg[i * 2 + 4] * 256 + pkg[i * 2 + 5]);
                        if (data > 32768) data = data - 65536;
                        data /= 12384;
                        tmp.Add(data);
                    }
                    fft_raw.AddRange(tmp);
                    /*固定长度*/
                    if (fft_raw.Count() > 4096)
                    {
                        fft_raw.RemoveRange(0, fft_raw.Count()-4096);
                    }
                }
                sample_pkgs.RemoveRange(0,sample_pkgs.Count() - sample_pkgs.Count()%128);
            }
            catch
            {

            }

        }
    
        /*UI更新 20Hz*/
        private void timer2_Tick(object sender, EventArgs e)
        {
            int sample_cnt = fft_raw.Count();
            /*chart 更新*/

            /*buffer 显示*/
            RX_Buffer_Update();

            /*label 显示*/
            label13.Text = (sample_pkgs.Count()/128).ToString("F2");
            label4.Text = err_count.ToString();
            toolStripProgressBar1.Value = (int)(fft_raw.Count() / 40.96);
            if(sample_cnt>0)
            {
                label1.Text = fft_raw.Count().ToString();
                label5.Text = fft_raw.ToArray()[fft_raw.Count() - 1].ToString();
            }
            textBox1.Text = "";
            for(int i = 0; i < 10 && fft_raw.Count()==4096; i++)
            {
                textBox1.Text += fft_raw[4086+i].ToString("F2");
                textBox1.Text += " ";
            }

            /*IO速度*/
            if (RX_RATE_CNTER * 10 < 16)
            {
                toolStripStatusLabel10.Text = (RX_RATE_CNTER * 10 * 8).ToString() + " bps";
            }
            else if (RX_RATE_CNTER * 10 < 1024)
            {
                toolStripStatusLabel10.Text = (RX_RATE_CNTER * 10).ToString() + " Bps";
            }
            else
            {
                float rate_kBps = (float)RX_RATE_CNTER * 10 / 1024;
                toolStripStatusLabel10.Text = rate_kBps.ToString("F2") + " kBps";
            }
            RX_RATE_CNTER = 0;
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            g_rt_data();
            pictureBox1.Image = draw_zone;
        }
    }

    public class Plot
    {
        /*用于完成数据和边框的映射*/
        private int g_Ox,g_Oy;          //画布原点
        private int g_x, g_y;           //画布尺寸
        public int data_Ox, data_Oy;   //画图原点
        private int data_x, data_y;     //画图尺寸
        private float max, min;         //数据范围
        private float k;                //数据缩放尺寸

        public int length;

        public Plot(Point O, Size obj, float data_max, float data_min)
        {
            g_Ox = O.X;
            g_Oy = O.Y + obj.Height;
            g_x = obj.Width;
            g_y = obj.Height;
            this.max = data_max;
            this.min = data_min;

            int gap_x = 20;
            int gap_y = 20;
            data_Ox = g_Ox + gap_x;
            data_Oy = g_Oy - gap_y;
            data_x = g_x - gap_x;
            data_y = g_y - gap_y;
            k = g_y / (max - min);

            length = data_x;
        }

        public void plot_reLocate(Point O)
        {
        }

        public void plot_reSize(Size obj)
        {
        }

        public void plot_reScale(float max, float min)
        {
        }

        public Point dataO()
        {
            Point p = new Point(data_Ox, data_Oy);
            return p;
        }
        public Point dataX()
        {
            Point p = new Point(data_Ox + data_x, data_Oy);
            return p;
        }
        public Point dataY()
        {
            Point p = new Point(data_Ox, data_Oy-data_y);
            return p;
        }
        public Point dataXY()
        {
            Point p = new Point(data_Ox + data_x, data_Oy-data_y);
            return p;
        }
        public int Y()
        {
            return data_Oy;
        }

        public int val2y(float val)
        {
            int tmp;

            if (val > max) val = max;
            if (val < min) val = min;

            tmp = (int)(k * val);

            return tmp;
        }
    }

}
