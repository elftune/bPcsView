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
using DxLibDLL;

namespace bPcsView
{
    public partial class Form1 : Form
    {
        public const int NOTE_HEIGHT = 8;
        int nLoadingLetter = 0;
        
        private bool MainLoop()
        {
            // Queue処理
            QueueData qdata = null;
            if (cque.TryDequeue(out qdata) == true)
            {
                if (qdata.mode == QueueData.MODE.MODE_LOAD)
                {
                    if (bms != null)
                    {
                        bms.Dispose();
                        bms = null;
                    }
                    bms = new CXBMS();
                    bms.AutoPlay = true;
                    bms.MakeRNDExpandFile = toolStripMenuItem2.Checked;
                    CDxCommon.OUTPUTLOG(qdata.FileName);
                    if (qdata.CodePage == -1) qdata.CodePage = nCodePage;
                    bms.Open(qdata.FileName, true, qdata.CodePage);
                    bRandomPlayMode = false;
                }

                if (qdata.mode == QueueData.MODE.MODE_RANDOMPLAY)
                {
                    if (bms != null)
                    {
                        bms.Dispose();
                        bms = null;
                    }
                    bms = new CXBMS();
                    bms.AutoPlay = true;
                    bms.MakeRNDExpandFile = toolStripMenuItem2.Checked;
                    CDxCommon.OUTPUTLOG(qdata.FileName);
                    if (qdata.CodePage == -1) qdata.CodePage = nCodePage;
                    bms.Open(qdata.FileName, true, qdata.CodePage);
                    bRandomPlayMode = true;
                }

                if (qdata.mode == QueueData.MODE.MODE_STOP)
                {
                    if (bms != null)
                    {
                        bms.Stop();
                        bms.Dispose();
                        bms = null;
                        bRandomPlayMode = false;
                    }
                }

                if (qdata.mode == QueueData.MODE.MODE_RELOAD)
                {
                    if (bms != null)
                    {
                        bms.Stop();
                        bms.Init(false); // WAV, BMP はそのまま保持
                        bms.Open(qdata.FileName, true, qdata.CodePage, true);
                        bRandomPlayMode = false;
                    }
                }
            }
            
            if (bOK == false) return true;

            DX.ClearDrawScreen();

            if (bms != null && bms.mode == CXBMS.MODE.MODE_CHANGING) return true;

            if (bms == null || bms.mode == CXBMS.MODE.IDLE)
            {
                DX.DrawString(600, 360 + nYSizeOfs, "BMSファイルをドラッグ＆ドロップしてください。", DXLIBCOLOR_YELLOW);
                DX.DrawString(600, 400 + nYSizeOfs, "現在のCodePage設定は " + nCodePage + " です。\n\n変更する場合は File ---> CPxxx から設定してください。", DXLIBCOLOR_WHITE);
                goto EXIT_PROC;
            }

            if (bms.mode == CXBMS.MODE.LOAD_LOADING)
            {
                // Loading中に動きが欲しいので赤色で一文字動かし続ける
                string s = "Now Loading ... " + bms.BI_BMSFolder + bms.BI_BMSFile;
                int x = 0, waitnum = (int)(8.0 * 8333.0 /  (double)INTERVAL_TIME);
                if ((nLoadingLetter / waitnum) >= s.Length) nLoadingLetter = 0;
                DX.DrawString((SIZE_WIDTH - DX.GetDrawStringWidth(s, s.Length)) / 2, 550 + nYSizeOfs, s, DXLIBCOLOR_GREEN);
                bool b = false;
                for (int i = 0; i < s.Length; i++)
                {
                    
                    if (Char.IsSurrogate(s, i) == false)
                    {
                        if (i == (nLoadingLetter / waitnum))
                        {
                            DX.DrawString((SIZE_WIDTH - DX.GetDrawStringWidth(s, s.Length)) / 2 + x, 550 + nYSizeOfs, s.Substring(nLoadingLetter / waitnum, 1), DXLIBCOLOR_RED);
                            b = true;
                        }
                        x += DX.GetDrawStringWidth(s.Substring(i, 1), 1);
                    }
                    else
                    {
                        if (i == (nLoadingLetter / waitnum))
                        {
                            DX.DrawString((SIZE_WIDTH - DX.GetDrawStringWidth(s, s.Length)) / 2 + x, 550 + nYSizeOfs, s.Substring(nLoadingLetter / waitnum, 2), DXLIBCOLOR_RED);
                            b = true;
                        }
                        x += DX.GetDrawStringWidth(s.Substring(i, 2), 2);
                        i++;
                    }
                    if (b == true) break;
                }
                if (++nLoadingLetter >= s.Length * waitnum) nLoadingLetter = 0;
            }

            if (bms.mode == CXBMS.MODE.LOAD_COMPLETE)
            {
                nLoadingLetter = 0;
                for(int i=0; i<listMRU.Count; i++)
                {
                    if (listMRU[i] == bms.BI_BMSFolder + bms.BI_BMSFile + "," + nCodePage)
                    {
                        listMRU.RemoveAt(i); // いったん消す
                        break;
                    }
                }
                listMRU.Insert(0, bms.BI_BMSFolder + bms.BI_BMSFile + "," + nCodePage);

                SaveMRUList();
                UpdateMenu();
                bms.AutoPlay = true;
                bms.Play(true);
            }

            bool bForce = true;
            if (bms.mode == CXBMS.MODE.PLAY_COMPLETE || bms.mode == CXBMS.MODE.LOAD_ABORT || bms.mode == CXBMS.MODE.PLAY_STOP)
            {
                if (bms.mode == CXBMS.MODE.PLAY_COMPLETE)
                {
                    // 譜面は終わっても、音が鳴っている or 動画が続いていれば継続する
                    bForce = bms.FullComplete;
                }
                if (bForce == true)
                {
                    bms.Dispose();
                    bms = null;
                    if (bRandomPlayMode == true)
                    {
                        frm3.RandomPlay();
                    }
                }
            }

            int[] anChs = {
                    0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09,

                    0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19,
                    0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29,
                    0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
                    0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
                    0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
                    0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,

                    0xD1, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9,
                    0xE1, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9,

                    257,
                    258,  259,  260,  261,  262,  263,  264,  265,  266,
                    267,  268,  269,  270,  271,  272,  273,  274,  275,
                    276,  277,  278,  279,  280,  281,  282,  283,  284,
                    285,  286,  287,  288,  289,  290,  291,  292,  293,
                    294,  295,  296,  297,  298,  299,  300,  301,  302,
                    303,  304
                };

            if ((bForce == false) || (bms != null && bms.mode == CXBMS.MODE.PLAY_PLAYING))
            {
                int wx, wy;
                double dAspect;
                int nCurrentNOTE;
                double dShorten;
                double dNoteLengthTime;
                double dNoteElapsedTime;
                CDxLibGraph GRAPH_4, GRAPH_7;
                double dWait;
                lock (bms.lockObj)
                {
                    nCurrentNOTE = bms.CurrentNote;
                    dShorten = bms.ShortenVal;
                    dNoteLengthTime = bms.NoteLengthTime;
                    dNoteElapsedTime = bms.NoteElapsedTime;
                    GRAPH_4 = bms.BGA_CH04;
                    GRAPH_7 = bms.BGA_CH07;
                    dWait = bms.StopLength;

                    bms.SilenceCh = bSilenceCh;
                }

                if (GRAPH_4 != null)
                {
                    DX.GetGraphSize(GRAPH_4.Handle, out wx, out wy);
                    dAspect = (double)wx / (double)wy;
                    wx = (int)(dAspect * 360.0);
                    DX.DrawExtendGraph((640 - wx)/2 + 32, SIZE_HEIGHT / 2 + 45 + nYSizeOfs, (640 - wx) / 2 + 32 + wx, SIZE_HEIGHT / 2 + 360 + 45 + nYSizeOfs, GRAPH_4.Handle, DX.FALSE);
                }
                if (GRAPH_7 != null)
                {
                    DX.GetGraphSize(GRAPH_7.Handle, out wx, out wy);
                    dAspect = (double)wx / (double)wy;
                    wx = (int)(dAspect * 360.0);
                    DX.DrawExtendGraph((640 - wx) / 2 + 32, SIZE_HEIGHT / 2 + 45 + nYSizeOfs, (640 - wx) / 2 + 32 + wx, SIZE_HEIGHT / 2 + 360 + 45 + nYSizeOfs, GRAPH_7.Handle, DX.TRUE);
                }

                int ix = 800, iy = SIZE_HEIGHT / 2 + 45 + nYSizeOfs, iy_delta = 16, ix2 = 128;

                DX.DrawString(ix, iy, "TITLE:", DXLIBCOLOR_YELLOW);
                DX.DrawString(ix + ix2, iy, bms.BI_TITLE, DXLIBCOLOR_YELLOW); iy += iy_delta;

                DX.DrawString(ix, iy, "SUBTITLE:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, bms.BI_SUBTITLE, DXLIBCOLOR_WHITE); iy += iy_delta;

                DX.DrawString(ix, iy, "GENRE:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, bms.BI_GENRE, DXLIBCOLOR_WHITE); iy += iy_delta;

                DX.DrawString(ix, iy, "ARTIST:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, bms.BI_ARTIST, DXLIBCOLOR_WHITE); iy += iy_delta;

                DX.DrawString(ix, iy, "SUBARTIST:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, bms.BI_SUBARTIST, DXLIBCOLOR_WHITE); iy += iy_delta;

                DX.DrawString(ix, iy, "PLAYER:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, bms.BI_PLAYER + "", DXLIBCOLOR_WHITE); iy += iy_delta;

                DX.DrawString(ix, iy, "PLAYLEVEL:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, bms.BI_PLAYLEVEL + "", DXLIBCOLOR_WHITE); iy += iy_delta;

                DX.DrawString(ix, iy, "RANK:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, bms.BI_RANK + "", DXLIBCOLOR_WHITE); iy += iy_delta;

                DX.DrawString(ix, iy, "TOTAL:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, bms.BI_TOTAL + "", DXLIBCOLOR_WHITE); iy += iy_delta;

                iy += iy_delta;
                DX.DrawString(ix, iy, "BPM:", DXLIBCOLOR_CYAN);
                DX.DrawString(ix + ix2, iy, bms.BPM + "", DXLIBCOLOR_CYAN); iy += iy_delta;

                DX.DrawString(ix, iy, "小節の数:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, bms.NumNOTES + "", DXLIBCOLOR_WHITE); iy += iy_delta;

                DX.DrawString(ix, iy, "現在の小節:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, nCurrentNOTE + "", DXLIBCOLOR_WHITE); iy += iy_delta;

                DX.DrawString(ix, iy, "小節の短縮値:", DXLIBCOLOR_CYAN);
                DX.DrawString(ix + ix2, iy, dShorten + "", DXLIBCOLOR_CYAN); iy += iy_delta;

                DX.DrawString(ix, iy, "再生経過時間:", DXLIBCOLOR_WHITE);
                DX.DrawString(ix + ix2, iy, ((double)bms.ElapsedTimeLL / 1000000.0).ToString("0.000") + "[sec]", DXLIBCOLOR_WHITE); iy += iy_delta;

                DX.DrawString(ix, iy, "停止時間:", DXLIBCOLOR_CYAN);
                DX.DrawString(ix + ix2, iy, (dWait / 1000000.0).ToString("0.000") + "[sec]", DXLIBCOLOR_CYAN); iy += iy_delta;

                if ((nCurrentNOTE >= 0) && (nCurrentNOTE <= bms.NumNOTES) && (dNoteLengthTime > 0.0))
                {
                    int x = 0;
                    double y = 0.0;
                    int nCNote = nCurrentNOTE;
                    dShorten = bms.GetShortenData(nCNote);
                    double yy = ((double)SIZE_HEIGHT / 2.0 - 40.0) + ((double)((SIZE_HEIGHT / 2.0 - 40.0) - 0.0) / dNoteLengthTime * dNoteElapsedTime * dShorten) + (double)nYSizeOfs;
                    DX.SetDrawArea(0, 0 + nYSizeOfs, SIZE_WIDTH, SIZE_HEIGHT / 2 - 40 + nYSizeOfs);

                DRAW_LOOP:
                    x = 32;
                    for (int i = 0; i < anChs.Length; i++)
                    {
                        int nVal = 255;
                        if ((anChs[i] >= 0x11 && anChs[i] <= 0x1F) && (bSilenceCh[0] == true)) nVal = 128;
                        if ((anChs[i] >= 0x21 && anChs[i] <= 0x2F) && (bSilenceCh[1] == true)) nVal = 128;
                        if ((anChs[i] >= 0x31 && anChs[i] <= 0x3F) && (bSilenceCh[2] == true)) nVal = 128;
                        if ((anChs[i] >= 0x41 && anChs[i] <= 0x4F) && (bSilenceCh[3] == true)) nVal = 128;
                        if ((anChs[i] >= 0x51 && anChs[i] <= 0x5F) && (bSilenceCh[4] == true)) nVal = 128;
                        if ((anChs[i] >= 0x61 && anChs[i] <= 0x6F) && (bSilenceCh[5] == true)) nVal = 128;
                        if ((anChs[i] >= 0xD1 && anChs[i] <= 0xDF) && (bSilenceCh[6] == true)) nVal = 128;
                        if ((anChs[i] >= 0xE1 && anChs[i] <= 0xEF) && (bSilenceCh[7] == true)) nVal = 128;
                        if ((anChs[i] >= 257) && (bSilenceCh[8] == true)) nVal = 128;
                        List<uint>[] NoteData = bms.GetNoteData(nCNote);
                        dShorten = bms.GetShortenData(nCNote);
                        for (int j = 0; j < NoteData[anChs[i]].Count; j++)
                        {
                            y = yy - (((double)(((double)SIZE_HEIGHT / 2.0 - 40.0) - 0.0) / (double)NoteData[anChs[i]].Count * dShorten) * (double)j);
                            uint ui = NoteData[anChs[i]][j];
                            if (ui != 0)
                            {
                                if (ui < (1 << 16))
                                {
                                    DX.DrawBox(x, (int)y, x + 12, (int)(y - (double)NOTE_HEIGHT), DX.GetColor(nVal, 0, 0), DX.TRUE);
                                    if (bDisplayNum == true)
                                    {
                                        switch(anChs[i])
                                        {
                                            case 2: // 小数
                                                DX.DrawStringToHandle(x, (int)y, dShorten.ToString(), DXLIBCOLOR_WHITE, nSFont);
                                                break;
                                            case 3: // 自然数
                                                DX.DrawStringToHandle(x, (int)y, ui.ToString(), DXLIBCOLOR_WHITE, nSFont);
                                                break;
                                            default: // Ch 04, 05, 06, 07, 08, 09
                                                DX.DrawStringToHandle(x, (int)y, CDxCommon.IntTo36((int)ui), DXLIBCOLOR_WHITE, nSFont);
                                                break;
                                        }
                                    }
                                }
                                else
                                {
                                    // ロングノート
                                    // このクラスでは 最初に (1 << 16)、途中の00に (1 << 17)、ラストに (1 <<18) を OR している
                                    int t = j;
                                    while(t < NoteData[anChs[i]].Count)
                                    {
                                        if ((NoteData[anChs[i]][t] < (1 << 16)) || (NoteData[anChs[i]][t] > (1 << 17))) break;
                                        t++;
                                    }
                                    double tt = ((double)((double)SIZE_HEIGHT / 2.0 - 40.0) * dShorten * ((double)(t - j) / (double)NoteData[anChs[i]].Count));
                                    DX.DrawBox(x, (int)y, x + 12, (int)(y - tt), DX.GetColor(nVal, nVal, 0), DX.TRUE);
                                    if (bDisplayNum == true)
                                    {
                                        if ((NoteData[anChs[i]][j] & (1 << 16)) > 0)
                                        {
                                            DX.DrawStringToHandle(x, (int)y, CDxCommon.IntTo36((int)(0xFFFF & NoteData[anChs[i]][j])), DXLIBCOLOR_WHITE, nSFont);
                                        }
                                    }

                                }
                            }
                        }
                        x += 12;
                    }
                    y = yy - ((double)(((double)SIZE_HEIGHT / 2.0 - 40.0) - 0.0) * dShorten);
                    DX.DrawLine(0, (int)y, SIZE_WIDTH, (int)y, DX.GetColor(192, 192, 192));
                    DX.DrawString(4, (int)y, nCNote.ToString("000"), DXLIBCOLOR_YELLOW);
                    DX.DrawString(4, (int)(y - 18.0), (nCNote + 1).ToString("000"), DXLIBCOLOR_YELLOW);
                    DX.DrawString(SIZE_WIDTH - 28, (int)y, nCNote.ToString("000"), DXLIBCOLOR_YELLOW);
                    DX.DrawString(SIZE_WIDTH - 28, (int)(y - 18.0), (nCNote + 1).ToString("000"), DXLIBCOLOR_YELLOW);
                    yy = y;
                    if (yy >= 0.0)
                    {
                        nCNote++;
                        if (nCNote <= bms.NumNOTES)
                        {
                            goto DRAW_LOOP;
                        }
                    }
                }
            }

            // Channel描画
            DX.SetDrawArea(0, 0 + nYSizeOfs, SIZE_WIDTH, SIZE_HEIGHT + nYSizeOfs);
            DX.DrawBox(32, SIZE_HEIGHT / 2 - 40 + nYSizeOfs, SIZE_WIDTH - 28-2, SIZE_HEIGHT / 2 - 6 + nYSizeOfs, DX.GetColor(0, 255, 255), DX.TRUE);
            for (int i = 0; i <= anChs.Length * 12; i += 12)
            {
                switch(i)
                {
                    case 0:
                    case 8 * 12:
                    case 17 * 12:
                    case 26 * 12:
                    case 35 * 12:
                    case 44 * 12:
                    case 53 * 12:
                    case 62 * 12:
                    case 71 * 12:
                    case 80 * 12:
                    case (80 + 48) * 12:
                        DX.DrawLine(32 + i, 0 + nYSizeOfs, 32 + i, SIZE_HEIGHT / 2 - 7 + nYSizeOfs, DX.GetColor(255, 255, 255));
                        break;
                    default:
                        DX.DrawLine(32 + i, 0 + nYSizeOfs, 32 + i, SIZE_HEIGHT / 2 - 7 + nYSizeOfs, DX.GetColor(128, 128, 128));
                        break;
                }
            }
            int x1 = 32+2;
            int y0 = SIZE_HEIGHT / 2 - 38 + nYSizeOfs;

            string sLabel1 = "00000000111111111222222222333333333444444444555555555666666666DDDDDDDDDEEEEEEEEE000000000111111111122222222223333333333444444444";
            string sLabel2 = "23456789123456789123456789123456789123456789123456789123456789123456789123456789123456789012345678901234567890123456789012345678";
            for(int i=0; i < sLabel1.Length; i++)
            {
                string s1 = sLabel1.Substring(i,1);
                string s2 = sLabel2.Substring(i, 1);
                DX.DrawString(x1, y0, s1, DXLIBCOLOR_BLACK);
                DX.DrawString(x1, y0 + 16, s2, DXLIBCOLOR_BLACK);
                x1 += 12;
            }
            DX.DrawString(1190, SIZE_HEIGHT / 2 + 2 + nYSizeOfs, "Channel 01 (Nth Lane)", DXLIBCOLOR_WHITE);

        EXIT_PROC:
            DX.DrawBox(952, 790, 952 + 558, 790 + 75, DXLIBCOLOR_WHITE, DX.TRUE);
            DX.DrawString(32, SIZE_HEIGHT / 2 + nYSizeOfs, FrameRate.ToString("0.00") + "FPS", DXLIBCOLOR_WHITE);

            return (DX.ScreenFlip() == 0);
        }
    }
}
