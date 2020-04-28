using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace fftTest
{
    public partial class Form1 : Form
    {
        double[] data_r;
        double[] data_i;
        double[] fft_r;
        double[] fft_i;
        public Form1()
        {
            InitializeComponent();
        }

        private double GetMax(ref double[] data)
        {
            double max = 0;
            foreach (double x in data)
                if (Math.Abs(x) > max)
                    max = Math.Abs(x);
            return max;
        }

        private void PicPaint(ref double[] data, ref PictureBox picbox)
        {
            int pointnum=picbox.Width;
            int xsamplerate = data.Length / pointnum;
            double yscale = picbox.Height / GetMax(ref data)/3;
            PointF[] points = new PointF[pointnum];
            for (int i = 0; i < pointnum; i++)
            {
                points[i].X = i;
                points[i].Y = (float)(picbox.Height / 2 - data[i * xsamplerate] * yscale);
            }
            Bitmap bmp = new Bitmap(picbox.Width,picbox.Height);
            Graphics gr = Graphics.FromImage((Image)bmp);
            Pen mypen = new Pen(Color.Yellow);
            gr.DrawLines(mypen, points);
            picbox.Image = bmp;
        }

        //迭代法求FFT
        private void FFT(ref double[] DataIn_r, ref double[] DataIn_i, ref double[] DataOut_r, ref double[] DataOut_i)
        {
            int len = DataIn_r.Length;
            if (len == 1)
            {
                DataOut_r = DataIn_r;
                DataOut_i = DataIn_i;
                return;
            }
            len = len / 2;
            //
            double[] evenData_r = new double[len];
            double[] evenData_i = new double[len];
            double[] oddData_r = new double[len];
            double[] oddData_i = new double[len];
            //
            double[] evenResult_r = new double[len];
            double[] evenResult_i = new double[len];
            double[] oddResult_r = new double[len];
            double[] oddResult_i = new double[len];
            //
            for (int i = 0; i < len ; i++)
            {
                evenData_r[i] = DataIn_r[i * 2];
                evenData_i[i] = DataIn_i[i * 2];
                oddData_r[i] = DataIn_r[i * 2 + 1];
                oddData_i[i] = DataIn_i[i * 2 + 1];
            }
            FFT(ref evenData_r, ref evenData_i, ref evenResult_r, ref evenResult_i);
            FFT(ref oddData_r, ref oddData_i, ref oddResult_r, ref oddResult_i);
            //
            double WN_r,WN_i;
            for (int i = 0; i < len; i++)
            {
                WN_r = Math.Cos(2 * Math.PI / (2 * len) * i);
                WN_i = -Math.Sin(2 * Math.PI / (2 * len) * i);
                DataOut_r[i] = evenResult_r[i] + oddResult_r[i] * WN_r - oddResult_i[i] * WN_i;
                DataOut_i[i] = evenResult_i[i] + oddResult_i[i] * WN_r + oddResult_r[i] * WN_i;
                DataOut_r[i + len] = evenResult_r[i] - oddResult_r[i] * WN_r + oddResult_i[i] * WN_i;
                DataOut_i[i + len] = evenResult_i[i] - oddResult_i[i] * WN_r - oddResult_r[i] * WN_i;

            }
            evenData_r = evenData_i = evenResult_r = evenResult_i = null;
            oddData_i = oddData_r = oddResult_i = oddResult_r = null;
            GC.Collect();
        }

        //旋转因子法求FFT
        //对原数据组进行重排
        private void DataSort(ref double[] data_r, ref double[] data_i)
        {
            if (data_r.Length == 0 || data_i.Length == 0 || data_r.Length != data_i.Length)
                return;
            int len = data_r.Length;
            int[] count = new int[len];
            int M = (int)(Math.Log(len) / Math.Log(2));
            double[] temp_r = new double[len];
            double[] temp_i = new double[len];

            for (int i = 0; i < len; i++)
            {
                temp_r[i] = data_r[i];
                temp_i[i] = data_i[i];
            }
            for (int l = 0; l < M; l++)
            {
                int space = (int)Math.Pow(2, l);
                int add = (int)Math.Pow(2, M - l - 1);
                for (int i = 0; i < len; i++)
                {
                    if ((i / space) % 2 != 0)
                        count[i] += add;
                }
            }
            for (int i = 0; i < len; i++)
            {
                data_r[i] = temp_r[count[i]];
                data_i[i] = temp_i[count[i]];
            }
        }

        //FFT算法
        private void Dit2_FFT(ref double[] data_r, ref double[] data_i,ref double[] result_r,ref double[] result_i)
        {
            if (data_r.Length == 0 || data_i.Length == 0 || data_r.Length != data_i.Length)
                return;
            int len = data_r.Length;
            double[] X_r = new double[len];
            double[] X_i = new double[len];
            for (int i = 0; i < len; i++)//将源数据复制副本，避免影响源数据的安全性
            {
                X_r[i] = data_r[i];
                X_i[i] = data_i[i];
            }
            DataSort(ref X_r, ref X_i);//位置重排
            double WN_r,WN_i;//旋转因子
            int M = (int)(Math.Log(len) / Math.Log(2));//蝶形图级数
            for (int l = 0; l < M; l++)
            {
                int space = (int)Math.Pow(2, l);
                int num = space;//旋转因子个数
                double temp1_r, temp1_i, temp2_r, temp2_i;
                for (int i = 0; i < num; i++)
                {
                    int p = (int)Math.Pow(2, M - 1 - l);//同一旋转因子有p个蝶
                    WN_r = Math.Cos(2 * Math.PI / len * p * i);
                    WN_i = -Math.Sin(2 * Math.PI / len * p * i);
                    for (int j = 0, n = i; j < p; j++, n += (int)Math.Pow(2, l + 1))
                    {
                        temp1_r = X_r[n];
                        temp1_i = X_i[n];
                        temp2_r = X_r[n + space];
                        temp2_i = X_i[n + space];//为蝶形的两个输入数据作副本，对副本进行计算，避免数据被修改后参加下一次计算
                        X_r[n] = temp1_r + temp2_r * WN_r - temp2_i * WN_i;
                        X_i[n] = temp1_i + temp2_i * WN_r + temp2_r * WN_i;
                        X_r[n + space] = temp1_r - temp2_r * WN_r + temp2_i * WN_i;
                        X_i[n + space] = temp1_i - temp2_i * WN_r - temp2_r * WN_i;
                    }
                }
            }
            //for (int i = 0; i < len; i++)//将源数据复制副本，避免影响源数据的安全性
            //{
            //    result_r[i] = X_r[i];
            //    result_i[i] = X_i[i];
            //}
            result_r = X_r;
            result_i = X_i;
        }

        private void GetMod(ref double[] complex_r, ref double[] complex_i, ref double[] mod)
        {
            if (complex_r.Length == 0 || complex_i.Length == 0 || complex_r.Length != complex_i.Length)
                return;
            for (int i = 0; i < complex_r.Length; i++)
                mod[i] = Math.Sqrt(complex_r[i] * complex_r[i] + complex_i[i] * complex_i[i]);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            data_r = new double[1024];
            data_i = new double[1024];
            for (int i = 0; i < data_r.Length; i++)
            {
                //正弦函数发生
                //data_r[i] = Math.Sin(2 * Math.PI * i / 128);
                //方波
                //if ((i / 128) % 2 == 0)
                //    data_r[i] = 1;
                //else
                //    data_r[i] = 0;
                //门函数
                if (i < 64)
                    data_r[i] = 1;
                else
                    data_r[i] = 0;
                //data_i[i] = 0;
            }
            PicPaint(ref data_r, ref pictureBox1);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if(data_r.Length==0||data_i.Length==0||data_r.Length!=data_i.Length)
                return;
            fft_r=new double[data_r.Length];
            fft_i=new double[data_r.Length];
            double[] result = new double[data_r.Length];
            FFT(ref data_r, ref data_i, ref fft_r, ref fft_i);
            GetMod(ref fft_r, ref fft_i, ref result);
            PicPaint(ref result, ref pictureBox2);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (data_r.Length == 0 || data_i.Length == 0 || data_r.Length != data_i.Length)
                return;
            fft_r = new double[data_r.Length];
            fft_i = new double[data_r.Length];
            double[] result = new double[data_r.Length];
            Dit2_FFT(ref data_r, ref data_i,ref fft_r,ref fft_i);
            GetMod(ref fft_r, ref fft_i, ref result);
            PicPaint(ref result, ref pictureBox2);
        }
    }
}
