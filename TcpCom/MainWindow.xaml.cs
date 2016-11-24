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
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;
using System.Threading;
using System.Windows.Threading;

namespace TcpCom
{
    // デリゲート関数
    public delegate void writeTextDelegate(string str);
    public delegate void writeImageDelegate(Bitmap bitmap);
    public delegate string readTextDelegate();

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // TCP待ち受けスレッド
            Thread t = new Thread(new ThreadStart(ListenData));
            t.IsBackground = true;
            t.Start();
        }

        public string readPortNum()
        {
            return textBox1.Text;
        }

        public void writeTextData(string str)
        {
            richTextBox.AppendText(str);
            richTextBox.ScrollToEnd();
        }

        public void writeImageData(Bitmap bitmap)
        {
            Clipboard.Clear();
            Clipboard.SetDataObject(bitmap);
            richTextBox.CaretPosition = richTextBox.Document.ContentEnd;
            richTextBox.Paste();
            richTextBox.ScrollToEnd();
        }

        public void ListenData()
        {
            // 待ち受けアドレス、ポートの設定
            string localhost = Dns.GetHostName();
            string str_ipad = null;
            IPAddress[] adrList = Dns.GetHostAddresses(localhost);
            foreach (IPAddress address in adrList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    str_ipad = address.ToString();
                    break;
                }
            }
            IPAddress ipad = IPAddress.Parse(str_ipad);
            string str_port = (string)textBox1.Dispatcher.Invoke(new readTextDelegate(readPortNum));
            Int32 port = Int32.Parse(str_port);
            TcpListener tl = new TcpListener(ipad, port);
            tl.Start();

            // メッセージの処理
            while (true)
            {
                TcpClient tc = tl.AcceptTcpClient();
                NetworkStream ns = tc.GetStream();

                // 受信したデータがなくなるまで繰り返す
                var typ = new byte[1];
                var len = new byte[4];
                while (ns.Read(typ, 0, typ.Length) != 0)
                {
                    ns.Read(len, 0, len.Length);
                    int num = BitConverter.ToInt32(len, 0);
                    byte[] data;

                    switch (typ[0])
                    {
                        case 0: // テキストデータの処理
                            data = new byte[num];
                            ns.Read(data, 0, data.Length);
                            var str = Encoding.Default.GetString(data);
                            Dispatcher.Invoke(new writeTextDelegate(writeTextData), new object[] { str });
                            break;
                        case 1: // 画像データの処理
                            int readsize = 0;
                            data = new byte[num];
                            while (readsize < num)
                            {
                                readsize += ns.Read(data, readsize, num - readsize);
                            }
                            BitmapImage bitmapImage = LoadImage(data);
                            Bitmap bitmap = BitmapImage2Bitmap(bitmapImage);
                            bitmap.Save("image.jpg", ImageFormat.Jpeg);
                            Dispatcher.Invoke(new writeImageDelegate(writeImageData), new object[] { bitmap });
                            break;
                        default:
                            break;
                    }
                }

                tc.Close();
            }

            tl.Stop();
        }

        private static BitmapImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        private Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                Bitmap bitmap = new Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Text Files|*.txt|Image Files|*.bmp;*.jpeg;*.jpg;*.gif;*.png";
            ofd.ShowDialog();
            string str = ofd.FileName;
            textBox3.Text = str.ToString();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string HostName = textBox.Text;
            int port = Int32.Parse(textBox1.Text);

            TcpClient tc = new TcpClient(HostName, port);
            NetworkStream ns = tc.GetStream();

            // Messageの送信
            if (textBox4.Text != "")
            {
                // 送信データのタイプ
                var typ = new byte[1];
                typ[0] = 0x0000;

                // 送信するデータ
                string str = textBox2.Text + " > " + textBox4.Text + "\n";
                var mesg = Encoding.ASCII.GetBytes(str);

                // データの長さ
                var len = BitConverter.GetBytes(mesg.Length);

                // タイプとデータ本体を結合して送信
                var bary = typ.Concat(len).Concat(mesg).ToArray();
                ns.Write(bary, 0, bary.Length);

                // テキストボックスに書き出し
                richTextBox.AppendText(str);
                richTextBox.ScrollToEnd();
            }

            // Fileの送信
            if (textBox3.Text != "")
            {
                if (IsTextFile(textBox3.Text))
                {
                    // 送信データのタイプ
                    var typ = new byte[1];
                    typ[0] = 0x0000;

                    // テキストファイルの内容をコピー
                    StreamReader fs = new StreamReader(textBox3.Text);
                    string str = fs.ReadToEnd();
                    var data = Encoding.ASCII.GetBytes(str);

                    // データの長さ
                    var len = BitConverter.GetBytes(data.Length);

                    // タイプとデータ本体を結合して送信
                    var bary = typ.Concat(len).Concat(data).ToArray();
                    ns.Write(bary, 0, bary.Length);

                    // テキストボックスに書き出し
                    richTextBox.AppendText(str);
                    richTextBox.ScrollToEnd();

                    fs.Close();
                }
                else
                {
                    // 送信データのタイプ
                    var typ = new byte[1];
                    typ[0] = 0x0001;

                    // 画像データの内容をコピー
                    FileStream fs = File.Open(textBox3.Text, FileMode.Open);
                    Bitmap bitmap = new Bitmap(System.Drawing.Image.FromStream(fs));
                    MemoryStream ms = new MemoryStream();
                    bitmap.Save(ms, ImageFormat.Bmp);
                    var img = ms.GetBuffer();

                    // データの長さ
                    var len = BitConverter.GetBytes(img.Length);

                    // タイプと長さとデータ本体を連結して送信
                    var bary = typ.Concat(len).Concat(img).ToArray();
                    ns.Write(bary, 0, bary.Length);

                    // テキストボックスに書き出し
                    Clipboard.Clear();
                    Clipboard.SetDataObject(bitmap);
                    richTextBox.CaretPosition = richTextBox.Document.ContentEnd;
                    richTextBox.Paste();
                    richTextBox.ScrollToEnd();

                    fs.Close();
                }
                
            }

            ns.Close();
            tc.Close();
        }

        public bool IsTextFile(string filePath)
        {
            FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            byte[] byteData = new byte[1];
            while (file.Read(byteData, 0, byteData.Length) > 0)
            {
                if (byteData[0] == 0)
                {
                    file.Close();
                    return false;
                }
            }
            file.Close();
            return true;
        }
    }
}
