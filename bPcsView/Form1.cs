using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using DxLibDLL;
using System.Collections.Concurrent;

namespace bPcsView
{
    public partial class Form1 : Form
    {
        public const string APP_TITLE = "bPcsView ver. 0.2019.03.08.01";
        const int SIZE_WIDTH = 1600, SIZE_HEIGHT = 900;
        const int INTERVAL_TIME = 8333; // 16666 8333
        bool bOK = false;
        long lNowTime, lNextTime;
        uint DXLIBCOLOR_WHITE = DX.GetColor(255, 255, 255);
        uint DXLIBCOLOR_BLACK = DX.GetColor(0, 0, 0);
        uint DXLIBCOLOR_RED = DX.GetColor(255, 0, 0);
        uint DXLIBCOLOR_GREEN = DX.GetColor(0, 255, 0);
        uint DXLIBCOLOR_CYAN = DX.GetColor(0, 255, 255);
        uint DXLIBCOLOR_YELLOW = DX.GetColor(255, 255, 0);

        bool[] bSilenceCh = new bool[9]; // 08...Ch01, 00...Ch1x, ...
        bool bDisplayNum = true;
        public bool bMainClose = false;

        CXBMS bms = null;
        Form3 frm3 = null;
        public ConcurrentQueue<QueueData> cque = new ConcurrentQueue<QueueData>();
        bool bRandomPlayMode = false;

        // フレームレート
        public int FrameCount = 0, FrameCount0 = 0;
        public double FrameRate=0.0;
        public uint FrameTime=0, FrameTime0=0;
        int nCodePage = -1;
        int nSFont = -1;
        List<string> listMRU = new List<string>();
        int nYSizeOfs = 0;

        public Form1()
        {
            InitializeComponent();
            this.Text = APP_TITLE;
            string sFile = CDxCommon.GetAppPath() + @"\MRUList.txt";
            if (File.Exists(sFile) == true)
            {
                string[] sFiles = File.ReadAllLines(sFile, Encoding.UTF8);
                listMRU = new List<string>(sFiles);
                UpdateMenu();
            }

            MENU_CP932.Checked = true; // 日本語
            MENU_CP949.Checked = false; // 韓国語
            MENU_CP65001.Checked = false; // UTF-8

            nCodePage = Encoding.GetEncoding(0).CodePage;
            UpdateCPDisplay();

            for (int i = 0; i < bSilenceCh.Length; i++)
                bSilenceCh[i] = false;
            bSilenceCh[2] = bSilenceCh[3] = true;

            nYSizeOfs = fileToolStripMenuItem.Size.Height + 4;
            this.ClientSize = new Size(SIZE_WIDTH, SIZE_HEIGHT + nYSizeOfs);
            MaximizeBox = false;
            DX.SetUserWindow(this.Handle);
            DX.ChangeWindowMode(DX.TRUE);
            DX.SetWindowSize(SIZE_WIDTH, SIZE_HEIGHT + nYSizeOfs);
            DX.SetAlwaysRunFlag(DX.TRUE);
            DX.SetWaitVSyncFlag(DX.FALSE);
            DX.SetMultiThreadFlag(DX.TRUE);
            DX.SetZBufferBitDepth(24);
            DX.SetCreateDrawValidGraphZBufferBitDepth(24);
            Point pt = Cursor.Position;
            DX.SetGraphMode(SIZE_WIDTH, SIZE_HEIGHT + nYSizeOfs, 32);
            DX.SetFullSceneAntiAliasingMode(4, 2);
            DX.SetDrawValidMultiSample(4, 2);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            DX.SetCreateSoundIgnoreLoopAreaInfo(DX.TRUE);

            bOK = (DX.DxLib_Init() == 0);
        }

        private void UpdateCPDisplay()
        {
            MENU_CP932.Checked = false; // 日本語
            MENU_CP949.Checked = false; // 韓国語
            MENU_CP65001.Checked = false; // UTF-8
            switch (nCodePage)
            {
                case 932:
                    MENU_CP932.Checked = true;
                    break;
                case 949:
                    MENU_CP949.Checked = true;
                    break;
                case 65001:
                    MENU_CP65001.Checked = true;
                    break;
            }
        }

        private void UpdateMenu()
        {
            if (listMRU.Count > 0)
            {
                toolMRU.DropDownItems.Clear();
                for (int i = 0; i < listMRU.Count; i++)
                {
                    ToolStripMenuItem sSubMenu = new ToolStripMenuItem();
                    string sFile = listMRU[i].Split(',')[0];
                    if (File.Exists(sFile) == true)
                    {
                        sSubMenu.Text = listMRU[i];
                        sSubMenu.Click += new System.EventHandler(menuMRUItemsClick);
                        toolMRU.DropDownItems.Add(sSubMenu);
                        if (i >= 30) break;
                    }
                }
            }
        }

        private void menuMRUItemsClick(object sender, EventArgs e)
        {
            string[] sVals = ((ToolStripMenuItem)sender).Text.Split(',');
            nCodePage = Int32.Parse(sVals[1]);

            QueueData data = new QueueData();
            data.mode = QueueData.MODE.MODE_LOAD;
            data.CodePage = nCodePage;
            data.FileName = sVals[0];
            cque.Enqueue(data);
            UpdateCPDisplay();
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] sFiles= (string[])e.Data.GetData(DataFormats.FileDrop, false);
            string sFile = sFiles[0]; // 先頭のファイルだけ処理（手抜き

            QueueData data = new QueueData();
            data.mode = QueueData.MODE.MODE_LOAD;
            data.CodePage = nCodePage;
            data.FileName = sFile;
            cque.Enqueue(data);
        }

        public delegate void ChangeBMSDelegate(QueueData data);
        public void ChangeBMS(QueueData data)
        {
            ChangeBMS_Sub(data);
        }

        public void ChangeBMS_Sub(QueueData data)
        {
            if (InvokeRequired)
            {
                Invoke(new ChangeBMSDelegate(ChangeBMS_Sub), data);
                return;
            }
            cque.Enqueue(data);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void MENU_CP932_Click(object sender, EventArgs e)
        {
            nCodePage = 932;
            MENU_CP932.Checked = true; // 日本語
            MENU_CP949.Checked = false; // 韓国語
            MENU_CP65001.Checked = false; // UTF-8
        }

        private void MENU_CP949_Click(object sender, EventArgs e)
        {
            nCodePage = 949;
            MENU_CP932.Checked = false; // 日本語
            MENU_CP949.Checked = true; // 韓国語
            MENU_CP65001.Checked = false; // UTF-8
        }

        private void MENU_CP65001_Click(object sender, EventArgs e)
        {
            nCodePage = 65001;
            MENU_CP932.Checked = false; // 日本語
            MENU_CP949.Checked = false; // 韓国語
            MENU_CP65001.Checked = true; // UTF-8
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Form2 frm2 = new Form2();
            frm2.Value = nCodePage;
            if (frm2.ShowDialog() == DialogResult.OK)
            {

                MENU_CP932.Checked = false;
                MENU_CP949.Checked = false;
                MENU_CP65001.Checked = false;

                nCodePage = frm2.Value;

                if (nCodePage == 932) MENU_CP932.Checked = true;
                if (nCodePage == 949) MENU_CP949.Checked = true;
                if (nCodePage == 65001) MENU_CP65001.Checked = true;
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            bSilenceCh[0] = checkBox1.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            bSilenceCh[1] = checkBox2.Checked;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            bSilenceCh[2] = checkBox3.Checked;
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            bSilenceCh[3] = checkBox4.Checked;
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            bSilenceCh[4] = checkBox5.Checked;
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            bSilenceCh[5] = checkBox6.Checked;
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            bSilenceCh[6] = checkBox7.Checked;
        }

        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            bSilenceCh[7] = checkBox8.Checked;
        }

        private void checkBox9_CheckedChanged(object sender, EventArgs e)
        {
            bSilenceCh[8] = checkBox9.Checked;
        }

        private void checkBox10_CheckedChanged(object sender, EventArgs e)
        {
            bDisplayNum = checkBox10.Checked;
        }

        private void listToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (frm3 == null)
            {
                frm3 = new Form3(cque);
                frm3.frm1 = this;
            }

            Thread thread = new Thread(new ThreadStart(Form3_Start));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        void Form3_Start()
        {
            frm3.ShowDialog();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            toolStripMenuItem2.Checked = !toolStripMenuItem2.Checked;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string sFile = CDxCommon.GetAppPath() + "\\MRUList.txt";
            System.Diagnostics.Process.Start(sFile);
        }

        private void updateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string sFile = CDxCommon.GetAppPath() + @"\MRUList.txt";
            if (File.Exists(sFile) == true)
            {
                string[] sFiles = File.ReadAllLines(sFile, Encoding.UTF8);
                listMRU = new List<string>(sFiles);
                UpdateMenu();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (bOK == false)
            {
                MessageBox.Show("エラーが発生しました。終了します。", APP_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            else
            {
                DX.SRand(DX.GetNowCount());
                DX.SetTransColor(0, 0, 0);
                DX.SetBackgroundColor(0, 0, 75); // MIFESみたいな色
                DX.SetDrawMode(DX.DX_DRAWMODE_BILINEAR); // DxLib_Init()の前だと意味がない
                DX.SetFontCacheCharNum(3072);
                nSFont = DX.CreateFontToHandle("", 14, -1, DX.DX_FONTTYPE_NORMAL);
                DX.SetDrawScreen(DX.DX_SCREEN_BACK);
            }
        }

        private void reLoadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (bms != null && bms.mode == CXBMS.MODE.PLAY_PLAYING)
            {
                string sFile = bms.BI_BMSFolder + bms.BI_BMSFile;
                QueueData data = new QueueData();
                data.mode = QueueData.MODE.MODE_RELOAD;
                data.CodePage = nCodePage;
                data.FileName = sFile;
                cque.Enqueue(data);
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (bOK == false) Close();

            lNowTime = DX.GetNowHiPerformanceCount();
            lNextTime = lNowTime += INTERVAL_TIME;

            while (bOK == true)
            {
                lNowTime = DX.GetNowHiPerformanceCount();
                if (lNowTime >= lNextTime)
                {
                    if (MainLoop() == false)
                    {
                        bOK = false;
                        Close();
                    }
                    lNextTime += INTERVAL_TIME;
                    lNowTime = DX.GetNowHiPerformanceCount();
                    if (lNextTime < lNowTime) lNextTime = lNowTime + INTERVAL_TIME;

                    FrameCount++;
                    FrameTime = (uint)(DX.GetNowHiPerformanceCount() / 1000);
                    if (FrameTime - FrameTime0 > 1000)
                    {
                        FrameRate = (double)(FrameCount - FrameCount0) * 1000.0 / (double)(FrameTime - FrameTime0);
                        FrameTime0 = FrameTime;
                        FrameCount0 = FrameCount;
                    }

                }
                Application.DoEvents();
                if (DX.ProcessMessage() != 0)
                {
                    bOK = false;
                    Close();
                }
                lNowTime = DX.GetNowHiPerformanceCount();
                if (lNextTime - lNowTime > 3500)
                    System.Threading.Thread.Sleep(1);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }
        protected override void OnPaint(PaintEventArgs pevent) {}

        bool SaveMRUList()
        {
            if (listMRU.Count > 0)
            {
                string[] sFiles = listMRU.ToArray();
                string sFile = CDxCommon.GetAppPath() + @"\MRUList.txt";
                try
                {
                    File.WriteAllLines(sFile, sFiles, Encoding.UTF8);
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            bMainClose = true;
            if (frm3 != null)
                frm3.CloseEx();

            bOK = false;
            if (bms != null)
            {
                bms.Dispose();
                bms = null;
            }

            SaveMRUList();

            DX.InitFontToHandle();
            DX.InitSoundMem();
            DX.InitGraph();
            DX.DxLib_End();
        }
    }

    public class QueueData
    {
        public enum MODE { MODE_LOAD, MODE_RANDOMPLAY, MODE_STOP, MODE_RELOAD };
        public MODE mode;
        public int CodePage;
        public string FileName;
    }
}
