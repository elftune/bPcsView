using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Collections.Concurrent;
using DxLibDLL;

namespace bPcsView
{
    public partial class Form3 : Form
    {
        public Form1 frm1 = null;
        ConcurrentQueue<QueueData> que = null;
        public Form3(ConcurrentQueue<QueueData> que)
        {
            InitializeComponent();
            this.que = que;
        }

        // 全てクリア
        private void button2_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count == 0) return;

            listBox1.Items.Clear();
        }

        // ランダム再生
        private void button1_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count == 0) return;
            if (button1.Text == "ランダム再生")
            {
                button1.Text = "次の曲へ";
            }
            else
            {
            }
            RandomPlay();
        }


        public delegate void RandomPlayDelegate();
        public void RandomPlay()
        {
            if (InvokeRequired)
            {
                Invoke(new RandomPlayDelegate(RandomPlay));
                return;
            }
            RandomPlaySub();
        }

        private void RandomPlaySub()
        {
            int n = DX.GetRand(listBox1.Items.Count - 1);
            string sFile = (string)listBox1.Items[n];
            listBox1.SelectedIndex = n;

            QueueData data = new QueueData();
            data.mode = QueueData.MODE.MODE_RANDOMPLAY;
            data.CodePage = -1;
            data.FileName = sFile;
            que.Enqueue(data);
        }

        // 再生停止
        private void button3_Click(object sender, EventArgs e)
        {
            QueueData data = new QueueData();
            data.mode = QueueData.MODE.MODE_STOP;
            data.CodePage = -1;
            data.FileName = "";
            button1.Text = "ランダム再生";
            que.Enqueue(data);
        }

        // 単発再生
        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listBox1.Items.Count == 0) return;

            QueueData data = new QueueData();
            data.mode = QueueData.MODE.MODE_LOAD;
            data.CodePage = -1;
            data.FileName = listBox1.Text;

            button1.Text = "ランダム再生";
            frm1.ChangeBMS(data);
        }


        private void listBox1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void listBox1_DragDrop(object sender, DragEventArgs e)
        {
            string[] fileName = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            string[] sExts = { ".bms", ".bml", ".bme", ".bmx", ".pms", ".pmx" };

            for (int j = 0; j < fileName.Length; j++)
            {
                string sFile = fileName[j];
                if (File.GetAttributes(sFile).HasFlag(FileAttributes.Directory) == true)
                {
                    UpdateList(sFile);
                }
                else
                {
                    // ファイルなら登録
                    for (int i = 0; i < sExts.Length; i++)
                    {
                        if (sFile.ToLower().EndsWith(sExts[i]) == true)
                        {
                            listBox1.Items.Add(sFile);
                            break;
                        }
                    }
                }
            }
        }

        private void UpdateList(string sRoot)
        {
            button1.Enabled = false;
            GetAllFiles(sRoot);
            button1.Enabled = true;
        }

        public void GetAllFiles(string folder)
        {
            string[] sExts = { ".bms", ".bml", ".bme", ".bmx", ".pms", ".pmx" };

            string[] fs = Directory.GetFiles(folder, "*");
            for (int j = 0; j < fs.Length; j++)
            {
                string sFile = fs[j];
                for (int i = 0; i < sExts.Length; i++)
                {
                    if (sFile.ToLower().EndsWith(sExts[i]) == true)
                    {
                        listBox1.Items.Add(sFile);
                        break;
                    }
                }
            }

            string[] ds = Directory.GetDirectories(folder);
            foreach (string d in ds)
            {
                GetAllFiles(d);
                Application.DoEvents();
                DX.ProcessMessage();
            }
        }

        private void listBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (listBox1.Items.Count == 0) return;

            if (e.KeyCode == Keys.Delete)
            {
                listBox1.Items.RemoveAt(listBox1.SelectedIndex);
            }
        }

        private void Form3_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (frm1.bMainClose == false)
            {
                e.Cancel = true;
                Hide();
            }
        }

        public delegate void CloseDelegate();
        public void CloseEx()
        {
            if (InvokeRequired)
            {
                Invoke(new CloseDelegate(CloseEx));
                return;
            }
            Close();
        }

    }
}
