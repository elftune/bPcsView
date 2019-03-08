using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Threading;
using System.Text;
using DxLibDLL;

namespace bPcsView
{
    public class BMS_INFO
    {
        public string strBMSFolder;
        public string strBMSFile;
        public DateTime? dtBMSFileTimestamp;
        public int nPLAYER;
        public string strGENRE;
        public string strTITLE;
        public string strARTIST;
        public string strSUBTITLE;
        public string strSUBARTIST;
        public int nPLAYLEVEL;
        public float fBPM;
        public int nRANK;
        public float fTOTAL;
        public string strSTAGEFILE;
        public int nVOLWAV;

        public string strPreviewFile;
        public int nCodePage;
        public int nKEYS;
        public string strBANNERFILE;

        public BMS_INFO()
        {
            strBMSFolder = "";
            strBMSFile = "";
            dtBMSFileTimestamp = null;
            nPLAYER = 0;
            strGENRE = "";
            strTITLE = "";
            strARTIST = "";
            strSUBTITLE = "";
            strSUBARTIST = "";
            nPLAYLEVEL = 0;
            fBPM = 0.0F;
            nRANK = 0;
            fTOTAL = 0.0F;
            strSTAGEFILE = "";
            strBANNERFILE = "";
            nVOLWAV = 0;

            strPreviewFile = "";
            nCodePage = 0;
            nKEYS = 0;
        }
    }

    public class NOTE_STRUCT : IDisposable
    {
        public List<uint>[] vctData;
        public double dShorten;
        public int[] nPtr = new int[CXBMS.BMS_MAX_PART];
        public int nNumBC;
        public int nMaxCount;

        public NOTE_STRUCT()
        {
            vctData = new List<uint>[CXBMS.BMS_MAX_PART];
            dShorten = -1.0;

            for (int i = 0; i < CXBMS.BMS_MAX_PART; i++)
            {
                vctData[i] = new List<uint>();
                nPtr[i] = 0;
            }
            nNumBC = 0;
            nMaxCount = 0;

        }

        public void Dispose()
        {
            for (int i = 0; i < CXBMS.BMS_MAX_PART; i++)
            {
                vctData[i].Clear();
            }
            nPtr = null;
        }
    };

    public class CXBMS : IDisposable
    {
        // private:
        MODE m_nMode = MODE.IDLE;
        long llOrg, llNow;
        CDxLibSound[] pSound = new CDxLibSound[MAX_WAVES];
        float[] afBPMs = new float[MAX_WAVES];
        int[] anSTOP = new int[MAX_WAVES];
        CDxLibGraph[] pBGA = new CDxLibGraph[MAX_WAVES];
        NOTE_STRUCT[] pNote = new NOTE_STRUCT[MAX_NOTES];
        List<int>[] anChannels = new List<int>[MAX_NOTES];
        int[] nMaxCountAtNote = new int[MAX_NOTES]; // ノートの中で最もデータ(分割)の多い数
        int nNOTES, nNOTES_true;
        float fBPM;
        int nCurrentBAR;
        int nCurrentBeat16;
        bool m_bBGM;
        bool bEXIT;
        double m_dShortenVal; // 小節の短縮化。通常は1.0
        long m_llFirstTime;
        CDxLibGraph m_pSpr_Part4; // Part:4
        CDxLibGraph m_pSpr_Part7; // Part:7(Layer)
        Thread thread = null;

        int BMS_BGM_VOL = 255;
        int BMS_VOLUME_DEFAULT = 255;
        int m_nPoorNum;
        BMS_INFO bi = null;
        int m_nCurrentNote;
        double m_dShortenVal2;
        double m_dStopLength;
        double m_dVRIntervalBAR, m_dVRPositionBAR;
        bool[] bSilenceCh = new bool[8];
        int nTypeLongNote = 0;
        int nLongNote = -1;
        bool bMakeRNDExpandFile = false;

        // public:
        public object lockObj = new object(); // lock用
        public const int MAX_WAVES = 36 * 36;
        public const int BMS_MAX_PART = (256 + 1 + 50);
        public const int CHANNEL_16BEAT = 256;
        public const int CHANNEL_BC = 257;
        public const int MAX_NOTES = 1000;      // 0 - 999
        public bool MakeRNDExpandFile { get { return bMakeRNDExpandFile; } set { bMakeRNDExpandFile = value; } }

        public List<uint>[] GetNoteData(int nNote) { return pNote[nNote].vctData; }
        public double GetShortenData(int nNote) { return (pNote[nNote].dShorten < 0) ? 1.0 : pNote[nNote].dShorten; }
        public enum MODE { IDLE, LOAD_LOADING, LOAD_COMPLETE, LOAD_ABORT, MODE_CHANGING, PLAY_PLAYING, PLAY_STOP, PLAY_COMPLETE };
        public MODE mode { get { return m_nMode; } } // 現在、CXBMSオブジェクトはどの状態か
        public int NumNOTES { get { return nNOTES_true; } } // 何小節あるか

        public long ElapsedTimeLL // 再生開始して何マイクロ秒経過したか(0小節からなので注意)
        {
            get
            {
                if (m_llFirstTime < 0)
                    return 0;
                else
                    return DX.GetNowHiPerformanceCount() - m_llFirstTime;
            }
        }
        public bool Playing { get { return m_nMode == MODE.PLAY_PLAYING; } } // 再生中か？
        public bool AutoPlay { set { m_bBGM = value; } get { return m_bBGM; } } // BGMモードか
        public int BackVolume
        {
            set
            {
                int n = value;
                if (n < 0 || n > 255) n = BMS_VOLUME_DEFAULT;
                BMS_BGM_VOL = n;
            }
        } // Ch01の音のボリューム (0～255)
        public float BPM { get { return fBPM; } } // BPM取得（デフォルトは130）
        public bool IsAllPlayStop // 音が全て鳴り終わったか
        {
            get
            {
                for (int i = 0; i < MAX_WAVES; i++)
                {
                    if (pSound[i] != null)
                    {
                        if (pSound[i].Playing == true)
                            return false;
                    }
                }
                return true;
            }
        }
        public CDxLibGraph BGA_CH04 { get { return m_pSpr_Part4; } } // BGA Ch04
        public CDxLibGraph BGA_CH07 { get { return m_pSpr_Part7; } } // BGA Ch07
        public int PoorBGANum { get { return m_nPoorNum; } set { m_nPoorNum = value; } }
        public bool[] SilenceCh { get { return bSilenceCh; } set { bSilenceCh = value; } }
        public int LongNoteType { get { return nTypeLongNote; } }

        // lockして呼び出そう
        public int CurrentNote { get { return m_nCurrentNote; } }
        public double ShortenVal { get { return m_dShortenVal2; } }
        public double NoteLengthTime { get { return m_dVRIntervalBAR; } }
        public double NoteElapsedTime { get { return m_dVRPositionBAR; } }
        public double StopLength { get { return m_dStopLength; }  }

        public string BI_BMSFolder { get { return bi.strBMSFolder; } }
        public string BI_BMSFile { get { return bi.strBMSFile; } }
        public DateTime? BI_BMSFileTimestamp { get { return bi.dtBMSFileTimestamp; } }
        public int BI_PLAYER { get { return bi.nPLAYER; } }
        public string BI_GENRE { get { return bi.strGENRE; } }
        public string BI_TITLE { get { return bi.strTITLE; } }
        public string BI_ARTIST { get { return bi.strARTIST; } }
        public string BI_SUBTITLE { get { return bi.strSUBTITLE; } }
        public string BI_SUBARTIST { get { return bi.strSUBARTIST; } }
        public int BI_PLAYLEVEL { get { return bi.nPLAYLEVEL; } }
        public float BI_BPM { get { return bi.fBPM; } }
        public int BI_RANK { get { return bi.nRANK; } }
        public float BI_TOTAL { get { return bi.fTOTAL; } }
        public string BI_STAGEFILE { get { return bi.strSTAGEFILE; } }
        public int BI_VOLWAV { get { return bi.nVOLWAV; } }
        public string BI_PREVIEW { get { return bi.strPreviewFile; } }
        public int BI_CODEPAGE { get { return bi.nCodePage; } }
        public int BI_KEYS { get { return bi.nKEYS; } }
        public string BI_BANNERFILE { get { return bi.strBANNERFILE; } }
        public bool FullComplete
        {
            get
            {
                bool bComplete = true;
                if (IsAllPlayStop == false) bComplete = false;
                if (BGA_CH04 != null)
                {
                    int nLen = DX.GetMovieTotalFrameToGraph(BGA_CH04.Handle);
                    if (DX.TellMovieToGraphToFrame(BGA_CH04.Handle) < nLen - 1) bComplete = false;
                }
                if (BGA_CH07 != null)
                {
                    int nLen = DX.GetMovieTotalFrameToGraph(BGA_CH07.Handle);
                    if (DX.TellMovieToGraphToFrame(BGA_CH07.Handle) < nLen - 1) bComplete = false;
                }
                return bComplete;
            }
        }


        public CXBMS()
        {
            m_bBGM = false;
            Init();
        }

        public void Init(bool bFullInit = true)
        {
            llOrg = llNow = 0;
            nTypeLongNote = 0;

            int i;
            for (i = 0; i < MAX_WAVES; i++)
            {
                if (bFullInit == true)
                {
                    if (pSound[i] != null)
                    {
                        pSound[i].Dispose();
                        pSound[i] = null;
                    }
                    if (pBGA[i] != null)
                    {
                        pBGA[i].Dispose();
                        pBGA[i] = null;
                    }
                }
                afBPMs[i] = 0.0f;
                anSTOP[i] = 0;
            }

            for (i = 0; i < MAX_NOTES; i++)
            {
                if (pNote[i] != null)
                {
                    pNote[i].Dispose();
                    pNote[i] = null;
                }
            }

            nNOTES = nNOTES_true = 0;
            fBPM = 130.0f;
            nCurrentBAR = 0;
            nCurrentBeat16 = 0;
            bEXIT = false;
            m_dShortenVal = 1.0;
            m_llFirstTime = 0;
            m_pSpr_Part4 = m_pSpr_Part7 = null;
            thread = null;
            m_nPoorNum = 0;

            m_nMode = MODE.IDLE;
        }

        public void Dispose()
        {
            Stop();
            Init();
        }

        public void Stop()
        {
            m_nMode = MODE.PLAY_STOP;
            bEXIT = true;
            for (int i = 0; i < MAX_WAVES; i++)
            {
                if (pSound[i] != null)
                    pSound[i].Stop(true);
            }
            if (thread != null)
            {
                thread.Abort();
                thread.Join();
                thread = null;
            }
        }


        // "FF" ---> 255
        private static int TwoBytesHexToInt(string str)
        {
            int i = 0;
            if (str[0] >= 'A' && str[0] <= 'F') i = (str[0] - 'A' + 10) * 16;
            if (str[0] >= 'a' && str[0] <= 'f') i = (str[0] - 'a' + 10) * 16;
            if (str[0] >= '0' && str[0] <= '9') i = (str[0] - '0' + 0) * 16;
            if (str[1] >= 'A' && str[1] <= 'F') i += (str[1] - 'A' + 10);
            if (str[1] >= 'a' && str[1] <= 'f') i += (str[1] - 'a' + 10);
            if (str[1] >= '0' && str[1] <= '9') i += (str[1] - '0' + 0);
            return i;
        }

        // "ZZ" ---> 36*36-1
        private static int TwoBytesHex36ToInt(string str)
        {
            int i = 0;
            if (str[0] >= 'A' && str[0] <= 'Z') i = (str[0] - 'A' + 10) * 36;
            if (str[0] >= 'a' && str[0] <= 'z') i = (str[0] - 'a' + 10) * 36;
            if (str[0] >= '0' && str[0] <= '9') i = (str[0] - '0' + 0) * 36;
            if (str[1] >= 'A' && str[1] <= 'Z') i += (str[1] - 'A' + 10);
            if (str[1] >= 'a' && str[1] <= 'z') i += (str[1] - 'a' + 10);
            if (str[1] >= '0' && str[1] <= '9') i += (str[1] - '0' + 0);
            return i;
        }

        // "99" ---> 99
        private static int TwoBytesIntToInt(string str)
        {
            int i = 0;

            if (!(str[0] >= '0' && str[0] <= '9')) return -1;

            if (str[0] >= '0' && str[0] <= '9') i = (str[0] - '0' + 0) * 10;
            if (str[1] >= '0' && str[1] <= '9') i += (str[1] - '0' + 0);

            return i;
        }

        // "999" ---> 999
        private static int ThreeBytesIntToInt(string str)
        {
            int i = 0;

            if (str[0] >= '0' && str[0] <= '9') i = (str[0] - '0' + 0) * 100;
            if (str[1] >= '0' && str[1] <= '9') i += (str[1] - '0' + 0) * 10;
            if (str[2] >= '0' && str[2] <= '9') i += (str[2] - '0' + 0);

            return i;
        }

        private BMS_INFO getBMS_Info(string sFile, int nCodePage = 932)
        {
            BMS_INFO bi = null;
            if (File.Exists(sFile) == false) goto EXIT_PROC;

            string s, s0;
            s0 = Directory.GetParent(sFile).ToString();
            if (s0.EndsWith(@"\") == false) s0 += @"\";

            string[] sLines = File.ReadAllLines(sFile, Encoding.GetEncoding(nCodePage));
            bi = new BMS_INFO();
            bi.strBMSFolder = s0;
            bi.strBMSFile = Path.GetFileName(sFile);
            bi.dtBMSFileTimestamp = File.GetLastWriteTime(sFile);

            // CodePage
            bi.nCodePage = nCodePage;

            try
            {
                for (int i = 0; i < sLines.Length; i++)
                {
                    s0 = "";
                    s = sLines[i];
                    string s2 = s.ToUpper();

                    s0 = "#0";
                    if (s2.StartsWith(s0))
                    {
                        // データ部は #000xxから始まるよね(笑)
                        int nPart = CXBMS.TwoBytesIntToInt(s2.Substring(4, 2));   // チャネルも00-99から00-FFへ (※多いと00-ZZらしいが)
                        if (nPart > 255) continue;
                        if (nPart <= 10) continue;
                        if (nPart > 25) continue;

                        switch (nPart)
                        {
                            case 11:
                            case 12:
                            case 13:
                            case 14:
                                bi.nKEYS = 1;
                                break;

                            case 15:
                                bi.nKEYS = 2;
                                break;

                            case 18:
                            case 19:
                                bi.nKEYS = 4;
                                break;

                            case 21:
                            case 22:
                            case 23:
                                bi.nKEYS = 8;
                                break;

                            case 24:
                            case 25:
                                bi.nKEYS = 16;
                                break;

                            default:
                                break;
                        }

                        continue;
                    }

                    s0 = "#PLAYER";
                    if (s2.StartsWith(s0))
                    {
                        try
                        {
                            bi.nPLAYER = Int32.Parse(s.Substring(s0.Length));
                        }
                        catch
                        {
                            bi.nPLAYER = 1;
                        }
                        continue;
                    }

                    s0 = "#GENRE";
                    if (s2.StartsWith(s0))
                    {
                        bi.strGENRE = s.Substring(s0.Length).Trim();
                        continue;
                    }

                    s0 = "#TITLE";
                    if (s2.StartsWith(s0))
                    {
                        bi.strTITLE = s.Substring(s0.Length).Trim();
                        continue;
                    }

                    s0 = "#ARTIST";
                    if (s2.StartsWith(s0))
                    {
                        bi.strARTIST = s.Substring(s0.Length).Trim();
                        continue;
                    }

                    s0 = "#SUBTITLE";
                    if (s2.StartsWith(s0))
                    {
                        bi.strSUBTITLE = s.Substring(s0.Length).Trim();
                        continue;
                    }

                    s0 = "#SUBARTIST";
                    if (s2.StartsWith(s0))
                    {
                        bi.strSUBARTIST = s.Substring(s0.Length).Trim();
                        continue;
                    }

                    s0 = "#PLAYLEVEL";
                    if (s2.StartsWith(s0))
                    {
                        // 空欄がよくあるのでチェック
                        try
                        {
                            bi.nPLAYLEVEL = Int32.Parse(s.Substring(s0.Length));
                        }
                        catch
                        {
                            bi.nPLAYLEVEL = 0;
                        }
                        continue;
                    }

                    s0 = "#BPM "; // #BMP01 などを除外するため、これは " " を含めておく
                    if (s2.StartsWith(s0))
                    {
                        try
                        {
                            bi.fBPM = float.Parse(s.Substring(s0.Length));
                        }
                        catch
                        {
                            bi.fBPM = 130.0F;
                        }
                        continue;
                    }

                    s0 = "#RANK";
                    if (s2.StartsWith(s0))
                    {
                        try
                        {
                            bi.nRANK = Int32.Parse(s.Substring(s0.Length));
                        }
                        catch
                        {
                            bi.nRANK = 1;
                        }
                        continue;
                    }

                    s0 = "#TOTAL";
                    if (s2.StartsWith(s0))
                    {
                        try
                        {
                            bi.fTOTAL = float.Parse(s.Substring(s0.Length));
                        }
                        catch
                        {
                            bi.fTOTAL = 1.0F;
                        }
                        continue;
                    }

                    s0 = "#STAGEFILE";
                    if (s2.StartsWith(s0))
                    {
                        bi.strSTAGEFILE = s.Substring(s0.Length).Trim();
                        continue;
                    }
                    if (bi.strSTAGEFILE == "")
                    {
                        s0 = "#BACKBMP";
                        if (s2.StartsWith(s0))
                        {
                            bi.strSTAGEFILE = s.Substring(s0.Length).Trim();
                            continue;
                        }
                    }

                    s0 = "#VOLWAV";
                    if (s2.StartsWith(s0))
                    {
                        try
                        {
                            bi.nVOLWAV = Int32.Parse(s.Substring(s0.Length));
                        }
                        catch
                        {
                            bi.nVOLWAV = 100;
                        }
                        continue;
                    }

                    s0 = "#PREVIEW";
                    if (s2.StartsWith(s0))
                    {
                        bi.strPreviewFile = s.Substring(s0.Length).Trim();
                        continue;
                    }

                    s0 = "#BANNER";
                    if (s2.StartsWith(s0))
                    {
                        bi.strBANNERFILE = s.Substring(s0.Length).Trim();
                        continue;
                    }
                }
            }
            catch
            {
                CDxCommon.OUTPUTLOG(sFile + "の読み込みでエラーが発生しました。");
                bi = null;
            }
            finally
            {

            }


        EXIT_PROC:
            return bi;
        }

        public bool Play(bool bAsync)
        {
            if (!(m_nMode == MODE.LOAD_COMPLETE || m_nMode == MODE.PLAY_COMPLETE || m_nMode == MODE.PLAY_STOP))
                return false;

            m_nMode = MODE.MODE_CHANGING;
            if (bAsync == true)
            {
                thread = new Thread(new ThreadStart(BMSPlayThread));
                thread.Priority = ThreadPriority.AboveNormal;
                thread.Start();
            }
            else
            {
                BMSPlayThread();
            }
            return true;
        }

        public void Open(string sBMSFullPathFile, bool bAsync, int nCodePage, bool bReLoad = false)
        {
            m_nMode = MODE.MODE_CHANGING;
            if (bReLoad == false) Dispose();

            object[] objs = new object[2];
            objs[0] = sBMSFullPathFile;
            objs[1] = nCodePage;

            bi = getBMS_Info(sBMSFullPathFile, nCodePage);
            if (bi == null)
            {
                Dispose();
                m_nMode = MODE.IDLE;
                return;
            }
            m_nMode = MODE.LOAD_LOADING;
            if (bAsync == true)
            {
                thread = new Thread(new ParameterizedThreadStart(BMSOpenThread));
                thread.Priority = ThreadPriority.AboveNormal;
                thread.Start(objs);
            }
            else
            {
                BMSOpenThread(objs);
            }
        }

        string[] PreParseLines(string[] sLines, out bool bChanged)
        {
            List<string> listLines = new List<string>();
            bChanged = false;

            int nMode = 0;
            // 0...探索中
            // 1...randomがあったのでifを探索中
            // 2...該当するifだったのでendifを探索中
            // 3...該当しないifだったのでendifを探索中

            int nRND_MAX = 0, nRND = 0, nRND_V = 0;
            int nLoop = 0;
            for (int i = 0; i < sLines.Length; i++)
            {
                string s = sLines[i]; // "#random xx"
                string s2 = s.ToLower();
                switch (nMode)
                {
                    case 0: // #random 探索中
                        if (s2.StartsWith("#random ") == true)
                        {
                            nRND_MAX = Int32.Parse(s2.Substring(8)); // "24" ---> 24 (1-24)
                            if (nRND_MAX <= 0) return null;
                            nRND = DX.GetRand(nRND_MAX - 1) + 1; // 0-23 ---> 1-24
                            listLines.Add("*--- RND = " + nRND);

                            bChanged = true;
                            nMode = 1;
                        }
                        else if (s2.StartsWith("#if ") == true)
                        {
                            // #random が無いのに #if が出てきたらおかしい
                            return null;
                        }
                        else if (s2.StartsWith("#endif") == true)
                        {
                            // #random が無いのに #endif が出てきたらおかしい
                            // DATAERR0R のラストに #ENDIF があるんだよね...
                            //                            return null;
                        }
                        else
                        {
                            if (s2.StartsWith("#endrandom") == false)
                                listLines.Add(s);
                        }
                        break;

                    case 1: // randomになったのでifを探索中
                        if (s2.StartsWith("#random ") == true)
                        {
                            // 新規探索！
                            int nLoop2 = Int32.Parse(s2.Substring(8));
                            int ii = i + 1;
                            while (nLoop2 > 0)
                            {
                                string s3 = sLines[ii].ToLower();
                                if (s3.StartsWith("#random ") == true) nLoop2 += Int32.Parse(s3.Substring(8));
                                if (s3.StartsWith("#endif") == true) nLoop2--;
                                ii++;
                            }
                            string[] sLines0 = new string[ii - i];
                            Array.Copy(sLines, i, sLines0, 0, ii - i);
                            string[] sResult = PreParseLines(sLines0, out bChanged);
                            if (sResult == null)
                                return null;
                            else
                            {
                                listLines.AddRange(sResult);
                            }
                            i = ii - 1;
                        }
                        else if (s2.StartsWith("#if ") == true)
                        {
                            nRND_V = Int32.Parse(s2.Substring(4));
                            if (nRND_V <= 0 || nRND_V > nRND_MAX) return null;
                            if (nRND_V == nRND)
                            {
                                // これだ。endifまで回収
                                nMode = 2;
                            }
                            else
                            {
                                nLoop = 1;
                                nMode = 3;
                            }
                        }
                        else if (s2.StartsWith("#endif") == true)
                        {
                            // これはあかん
                            return null;
                        }
                        else
                        {
                        }
                        break;

                    case 2: // 正解
                        if (s2.StartsWith("#random ") == true)
                        {
                            // 新規探索！
                            int nLoop2 = Int32.Parse(s2.Substring(8));
                            int ii = i + 1;
                            while(nLoop2 > 0)
                            {
                                string s3 = sLines[ii].ToLower();
                                if (s3.StartsWith("#random ") == true) nLoop2 += Int32.Parse(s3.Substring(8));
                                if (s3.StartsWith("#endif") == true) nLoop2--;
                                ii++;
                            }
                            string[] sLines0 = new string[ii - i];
                            Array.Copy(sLines, i, sLines0, 0, ii - i);
                            string[] sResult = PreParseLines(sLines0, out bChanged);
                            if (sResult == null)
                                return null;
                            else
                            {
                                listLines.AddRange(sResult);
                            }
                            i = ii - 1;
                        }
                        else if (s2.StartsWith("#if ") == true)
                        {
                            return null;
                        }
                        else if (s2.StartsWith("#endif") == true)
                        {
                            if (nRND_MAX == nRND_V)
                                nMode = 0;
                            else
                                nMode = 1;
                        }
                        else
                        {
                            listLines.Add(s);
                        }
                        break;

                    case 3: // 対象外
                        if (s2.StartsWith("#random ") == true)
                        {
                            // 新規探索！
                            nLoop += Int32.Parse(s2.Substring(8));
                        }
                        else if (s2.StartsWith("#if ") == true)
                        {
//                            return null;
                        }
                        else if (s2.StartsWith("#endif") == true)
                        {
                            if (--nLoop == 0)
                            {
                                if (nRND_MAX == nRND_V)
                                    nMode = 0;
                                else
                                    nMode = 1;
                            }
                        }
                        else
                        {
                        }
                        break;
                }
            }

            return listLines.ToArray();
        }

        private void BMSOpenThread(object args)
        {
            object[] argsTmp = (object[])args;
            string sBMSFullPathFile = (string)argsTmp[0];
            int nCodePage = (int)argsTmp[1];

            if (File.Exists(sBMSFullPathFile) == false)
            {
                goto _ERROR_TBMSPLAY_LOAD;
            }

            // 通常、BMSファイルとWAV/BMPは同じフォルダにある。のだが、差分ファイルなどはサブフォルダにあり、1つ上のフォルダを
            // 検索しなければならない
            string strFile = sBMSFullPathFile;
            if (strFile == "")
            {
                goto _ERROR_TBMSPLAY_LOAD;
            }

            string strFolder = Directory.GetParent(strFile).ToString();
            if (strFolder.EndsWith(@"\") == false) strFolder += @"\";
            string strFolder_PREV = Directory.GetParent(Directory.GetParent(strFile).ToString()).ToString();
            if (strFolder_PREV.EndsWith(@"\") == false) strFolder_PREV += @"\";

            string[] sLines = null;
            try
            {
                sLines = File.ReadAllLines(sBMSFullPathFile, Encoding.GetEncoding(nCodePage));
            }
            catch
            {
                goto _ERROR_TBMSPLAY_LOAD;
            }

            bool bChanged = false;
            sLines = PreParseLines(sLines, out bChanged);
            if (sLines == null)
            {
                goto _ERROR_TBMSPLAY_LOAD;
            }
            if (bChanged == true && bMakeRNDExpandFile == true)
            {
                // 変更があったファイル（#RANDOM あり)
                string sFld3 = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\" + DateTime.Now.ToString("RND_yyyyMMdd_hhmmss") + ".bms";
                File.WriteAllLines(sFld3, sLines, Encoding.GetEncoding(nCodePage));
            }

            string str = strFile.ToLower();
            if (str.EndsWith(".bms") == false && str.EndsWith(".bme") == false && str.EndsWith(".bml") == false && str.EndsWith(".bmx") == false && str.EndsWith(".pms") == false && str.EndsWith(".pmx") == false)
                goto _ERROR_TBMSPLAY_LOAD;

            int i = 0;

            // ファイル解析
            Random rnd = new Random(Environment.TickCount);
            string ss;

            int nNG_FILES = 0, nSKIP_FILES = 0, nTypeLongNote = 0;
            for (int lp = 0; lp < sLines.Length; lp++)
            {
                bool bSkip = true;
                string s, s0, buf, sBuf;
                sBuf = sLines[lp];
                int l = sBuf.Length;
                if (l < 5) goto NEXT_LINE; // 短けりゃ無視

                buf = sBuf.ToUpper(); // 大文字で処理

                // #WAVnn xxxx.wav の検索
                s = "#WAV";
                if (buf.IndexOf(s) >= 0)
                {
                    s0 = buf.Substring(7); // "yyy/xxxxxx.ogg"

                    int k = TwoBytesHex36ToInt(buf.Substring(4, 2)); // pSound番号
                    if (pSound[k] == null)
                    {
                        pSound[k] = new CDxLibSound();
                    }
                    string sd = strFolder + s0;

                    if (s0.IndexOf(".") < 0)
                    {
                        ss = s0 + "は拡張子が無いため、処理しません。";
                        goto NEXT_STEP_A;
                    }

                    // s0 がファイル名(BMS上の)
                    sd = sd.Substring(0, sd.LastIndexOf("."));
                    // Path.GetFileNameWithoutExtension() はダメ。s0 には フォルダ/ファイル の物もあるため

                    string[] sEXTz_W = { ".ogg", ".wav", ".oga", ".opus", ".wma", ".mp3" };

                    // まずはそのフォルダでチェック
                    foreach (string ff in sEXTz_W)
                    {
                        if (File.Exists(sd + ff) == true)
                            if (pSound[k].Open(sd + ff) >= 0) goto NEXT_STEP_A;
                    }

                    // 1つ上のフォルダでチェック
                    sd = strFolder_PREV + s0;
                    sd = sd.Substring(0, sd.LastIndexOf("."));

                    foreach (string ff in sEXTz_W)
                    {
                        if (File.Exists(sd + ff) == true)
                            if (pSound[k].Open(sd + ff) >= 0) goto NEXT_STEP_A;
                    }
                    bSkip = false;

                    ss = s0 + " は存在しないファイル(または読み込めないファイル)ですが、続行します。";
                    if (bSkip == true)
                    {
                        nSKIP_FILES++;
                    }
                    else
                    {
                        nNG_FILES++;
                    }
                    CDxCommon.OUTPUTLOG(ss);
                NEXT_STEP_A:
                    goto NEXT_LINE;
                }

                // #LNOBJ nn の検索
                s = "#LNTYPE 1";
                if (buf.IndexOf(s) == 0)
                {
                    nTypeLongNote |= 1;
                    goto NEXT_LINE;
                }
                s = "#LNOBJ";
                if (buf.IndexOf(s) == 0)
                {
                    int n = TwoBytesHex36ToInt(buf.Substring(7, 2));
                    nLongNote = n;
                    nTypeLongNote |= 2;
                    goto NEXT_LINE;
                }

                // #BPM nnn の検索
                s = "#BPM";
                if (buf.IndexOf(s) == 0)
                {
                    // 2通りのパターンがある
                    if (buf.IndexOf("#BPM ") == 0)
                    {
                        float n = float.Parse(buf.Substring(5));
                        fBPM = (float)n;
                    }
                    else
                    {
                        int n = TwoBytesHex36ToInt(buf.Substring(4, 2));
                        float v = float.Parse(buf.Substring(7));
                        afBPMs[n] = v;
                    }
                    goto NEXT_LINE;
                }

                // #STOP nnn の検索
                s = "#STOP";
                if (buf.IndexOf(s) == 0)
                {
                    int k = TwoBytesHex36ToInt(buf.Substring(5, 2));
                    int n = Int32.Parse(buf.Substring(8));
                    anSTOP[k] = n;
                    goto NEXT_LINE;
                }

                // #BMPnn xxx の検索
                s = "#BMP";
                if (buf.IndexOf(s) == 0)
                {
                    if (buf.Length > 7)
                        s0 = buf.Substring(7);  // "yyy/xxxxxx.m1v"
                    else
                        s0 = ""; // 譜面落つ とかで #BMPFFI だけのがあったりする

                    int k = TwoBytesHex36ToInt(buf.Substring(4, 2)); // pBGA番号
                    if (pBGA[k] == null)
                    {
                        pBGA[k] = new CDxLibGraph();
                    }

                    string sd = strFolder + s0; // sdはフルパス、sd2はファイル名のみ
                    string sd0 = sd; // もともとのファイル
                    string sd0A = strFolder_PREV + s0; // もともとのファイル

                    // PABAT 2016 seasons\Distorte\DistorteDBeginner.bms に #BMP00 0 なんてのがあった...
                    if (s0.IndexOf(".") < 0)
                    {
                        ss = s0 + "は拡張子が無いため、処理しません。";
                        goto NEXT_LINE;
                    }

                    sd = sd.Substring(0, sd.LastIndexOf(".")); // sd  = フルパスの拡張子寸前まで

                    // まずはそのフォルダでチェック
                    string[] sEXTz_SP = { ".ogv", ".ogx", ".mp4" };
                    string[] sEXTz_M = { ".mpg", ".mpeg", ".m1v", ".avi", ".wmv" };
                    string[] sEXTz_B = { ".png", ".jpg", ".jpeg", ".bmp" };

                    foreach (string ff in sEXTz_SP)
                    {
                        DX.SetUseASyncLoadFlag(DX.TRUE); // 非同期ON
                        if (File.Exists(sd + ff) == true)
                        {
                            if (pBGA[k].Load(sd + ff) >= 0)
                            {
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                // -1エラー　TRUE読み込み中　FALSE完了
                                while (DX.CheckHandleASyncLoad(pBGA[k].Handle) != DX.FALSE)
                                {
                                    Thread.Sleep(1);
                                    DX.ProcessMessage();
                                    if (bEXIT == true)
                                        goto NEXT_LINE;
                                }
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                goto NEXT_STEP_B;
                            }
                        }
                        DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                    }

                    // OGV/MP4ではないが読む（表示される可能性は低くなる）
                    foreach (string ff in sEXTz_M)
                    {
                        DX.SetUseASyncLoadFlag(DX.TRUE); // 非同期ON
                        if (File.Exists(sd + ff) == true)
                        {
                            if (pBGA[k].Load(sd + ff) >= 0)
                            {
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                // -1エラー　TRUE読み込み中　FALSE完了
                                while (DX.CheckHandleASyncLoad(pBGA[k].Handle) != DX.FALSE)
                                {
                                    Thread.Sleep(1);
                                    DX.ProcessMessage();
                                    if (bEXIT == true)
                                        goto NEXT_LINE;
                                }
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                goto NEXT_STEP_B;
                            }
                        }
                        DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                    }

                    foreach (string ff in sEXTz_B) // 画像
                    {
                        if (File.Exists(sd + ff) == true)
                        {
                            DX.SetUseASyncLoadFlag(DX.TRUE); // 非同期ON
                            if (pBGA[k].Load(sd + ff) >= 0)
                            {
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                // -1エラー　TRUE読み込み中　FALSE完了
                                while (DX.CheckHandleASyncLoad(pBGA[k].Handle) != DX.FALSE)
                                {
                                    Thread.Sleep(1);
                                    DX.ProcessMessage();
                                    if (bEXIT == true)
                                        goto NEXT_LINE;
                                }
                                if (pBGA[k].GraphType == CDxLibGraph.GRAPH_TYPE_MOVIE)
                                {
                                    if (pBGA[k].Load(sd + ff) >= 0) { goto NEXT_STEP_B; }
                                }
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                goto NEXT_STEP_B;
                            }
                            DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                        }
                    }

                    // 1つ上のフォルダでチェック
                    sd = strFolder_PREV + s0;
                    sd = sd.Substring(0, sd.LastIndexOf("."));

                    foreach (string ff in sEXTz_SP)
                    {
                        DX.SetUseASyncLoadFlag(DX.TRUE); // 非同期ON
                        if (File.Exists(sd + ff) == true)
                        {
                            if (pBGA[k].Load(sd + ff) >= 0)
                            {
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                // -1エラー　TRUE読み込み中　FALSE完了
                                while (DX.CheckHandleASyncLoad(pBGA[k].Handle) != DX.FALSE)
                                {
                                    Thread.Sleep(1);
                                    DX.ProcessMessage();
                                    if (bEXIT == true)
                                        goto NEXT_LINE;
                                }
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                goto NEXT_STEP_B;
                            }
                        }
                        DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                    }


                    // OGV/MP4ではないが読む（表示される可能性は低くなる）
                    foreach (string ff in sEXTz_M)
                    {
                        DX.SetUseASyncLoadFlag(DX.TRUE); // 非同期ON
                        if (File.Exists(sd + ff) == true)
                        {
                            if (pBGA[k].Load(sd + ff) >= 0)
                            {
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                // -1エラー　TRUE読み込み中　FALSE完了
                                while (DX.CheckHandleASyncLoad(pBGA[k].Handle) != DX.FALSE)
                                {
                                    Thread.Sleep(1);
                                    DX.ProcessMessage();
                                    if (bEXIT == true)
                                        goto NEXT_LINE;
                                }
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                goto NEXT_STEP_B;
                            }
                        }
                        DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                    }

                    foreach (string ff in sEXTz_B)
                    {
                        if (File.Exists(sd + ff) == true)
                        {
                            DX.SetUseASyncLoadFlag(DX.TRUE); // 非同期ON
                            if (pBGA[k].Load(sd + ff) >= 0)
                            {
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                // -1エラー　TRUE読み込み中　FALSE完了
                                while (DX.CheckHandleASyncLoad(pBGA[k].Handle) != DX.FALSE)
                                {
                                    Thread.Sleep(1);
                                    DX.ProcessMessage();
                                    if (bEXIT == true)
                                        goto NEXT_LINE;
                                }
                                if (pBGA[k].GraphType == CDxLibGraph.GRAPH_TYPE_MOVIE)
                                {
                                    if (pBGA[k].Load(sd + ff) >= 0) { goto NEXT_STEP_B; }
                                }
                                DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                                goto NEXT_STEP_B;
                            }
                            DX.SetUseASyncLoadFlag(DX.FALSE); // 非同期OFF
                        }
                    }

                    // ここに来たら「読めない」
                    while (DX.GetASyncLoadNum() > 0)
                    {
                        DX.ProcessMessage();
                        Thread.Sleep(1);
                    }
                    bSkip = false;

                    goto NEXT_LINE;

                NEXT_STEP_B:
                    if (pBGA[k] != null)
                    {
                        // 正常に読めた場合
                        if (DX.GetLastUpdateTimeMovieToGraph(pBGA[k].Handle) >= 0)
                        {
                            pBGA[k].GraphType = CDxLibGraph.GRAPH_TYPE_MOVIE;
                        }
                        else
                        {
                            pBGA[k].GraphType = CDxLibGraph.GRAPH_TYPE_BITMAP;
                        }

                        DX.SetMovieVolumeToGraph(0, pBGA[k].Handle);
                    }

                    goto NEXT_LINE;
                }


                // 楽譜データ
                // #nnnyy:x ～ xxxxxxxxxxxxxxxx
                string s000;
                if (buf[0] == '#' && (buf[1] >= '0' && buf[1] <= '9'))
                {
                    s000 = buf.Substring(1, 3);
                    int nNote = ThreeBytesIntToInt(s000); // "000" . 000
                    int nPart = TwoBytesHexToInt(buf.Substring(4, 2)); // Channelのこと
                    if (nPart > 255) goto NEXT_LINE;
                    if (nNote > nNOTES) nNOTES = nNote;
                    int j = 0;
                    l = buf.Substring(7).Length; // #00001:XX の XX以降

                    // データが無いときは次
                    if (l < 2 && nPart != 2)
                        continue;
                    if (l == 2 && (buf.Substring(7) == "00") && (nPart != 1))
                        continue;


                    // 音符作成
                    bool bOverWrite = false;
                    if (pNote[nNote] == null)
                    {
                        pNote[nNote] = new NOTE_STRUCT();
                    }

                    // バックコーラスの時
                    if (nPart == 0x01)
                    {
                        if (pNote[nNote].nNumBC < 50)
                            nPart = CHANNEL_BC + pNote[nNote].nNumBC++;
                    }

                    // "00"で2バイトなので個数は2で割って1
                    if (l > 1)
                        l /= 2;
                    if (nPart == 0x02)
                        l = 1;

                    if (pNote[nNote].vctData[nPart].Count > 0)
                        bOverWrite = true;

                    for (j = 0; j < l; j++)
                    {
                        if (nPart == 0x03)
                        {
                            int nTempo = TwoBytesHexToInt(buf.Substring(7 + j * 2, 2));
                            if (bOverWrite == false)
                                pNote[nNote].vctData[nPart].Add((uint)nTempo);
                            else
                                pNote[nNote].vctData[nPart][j] = (uint)nTempo;
                        }
                        else if (nPart == 0x02)
                        {
                            double dShort = float.Parse(buf.Substring(7 + j * 2));
                            if (bOverWrite == false) // 表示用にダミー値を入れる
                                pNote[nNote].vctData[nPart].Add((uint)1);
                            else
                                pNote[nNote].vctData[nPart][j] = (uint)1;
                            pNote[nNote].dShorten = dShort;
                        }
                        else if (nPart > 256)
                        {
                            string sc = buf.Substring(7 + j * 2, 2);
                            uint ui = (uint)TwoBytesHex36ToInt(sc);
                            pNote[nNote].vctData[nPart].Add(ui);
                            if (ui > 0)
                            {
                                if (pSound[ui] != null)
                                {
                                    pSound[ui].DuplicateChannel(nPart);
                                }
                            }
                        }
                        else
                        {
                            // Ch < 256 の時は 0 以外は上書き
                            string sc = buf.Substring(7 + j * 2, 2);
                            uint ui = (uint)TwoBytesHex36ToInt(sc);
                            if ((ui > 0) && (pSound[ui] != null))
                            {
                                pSound[ui].DuplicateChannel(nPart);
                            }

                            if (bOverWrite == false)
                            {
                                pNote[nNote].vctData[nPart].Add(ui);
                            }
                            else
                            {
                                int n1 = pNote[nNote].vctData[nPart].Count;
                                int n2 = l;
                                int n3 = CDxCommon.Lcm(n1, n2); // 最小公倍数

                                if ((n1 == n3) && (n2 == n3))
                                {
                                    if (ui > 0)
                                        pNote[nNote].vctData[nPart][j] = ui;
                                }
                                else
                                    if ((n1 < n3) && (n2 == n3))
                                {
                                    for (i = n1; i < n3; i++)
                                        pNote[nNote].vctData[nPart].Add(0);

                                    for (i = n1 - 1; i >= 0; i--)
                                    {
                                        uint ui2 = pNote[nNote].vctData[nPart][i];
                                        pNote[nNote].vctData[nPart][i] = 0;
                                        pNote[nNote].vctData[nPart][(i * (n3 / n1))] = ui2;
                                    }
                                    if (ui > 0)
                                        pNote[nNote].vctData[nPart][j] = ui;
                                }
                                else
                                        if ((n1 == n3) && (n2 < n3))
                                {
                                    if (ui > 0)
                                        pNote[nNote].vctData[nPart][j * (n3 / n2)] = ui;
                                }
                                else
                                {
                                    i = 0;
                                }
                            }
                        }
                    }
                }

            NEXT_LINE:;
                if (bEXIT == true)
                {
                    m_nMode = MODE.LOAD_ABORT;
                    return;
                }
            }

            // 999 まであっても意味のないものがあるのではじく
            i = nNOTES - 1;
            if (nNOTES >= 999)
            {
                while (i > 0)
                {
                    if (pNote[i] == null) goto NEXT_LP3;
                    if (pNote[i].dShorten > 0.0 && pNote[i].vctData[CHANNEL_BC].Count == 0 && pNote[i].vctData[0x11].Count == 0 && pNote[i].vctData[0x14].Count == 0)
                    {
                    }
                    else
                    {
                        if (nNOTES - i > 100)
                        {
                            CDxCommon.OUTPUTLOG("有効なデータではないと判断する部分（末尾）は削除します。");
                            nNOTES = i + 3;
                            goto NEXT_STEP_2;
                        }
                        goto NEXT_STEP_2;
                    }
                NEXT_LP3:
                    i--;
                }
            }

        NEXT_STEP_2:


            // 最後がぷちっと切れるとムービーの関係上面白くないので2小節ダミーを追加しよう
            nNOTES_true = nNOTES;
            if (nNOTES < 996)
            {
                int nAdd = 3;
                if ((nNOTES % 2) == 1) nAdd = 2;
                for (i = 0; i < nAdd; i++)
                {
                    pNote[++nNOTES] = new NOTE_STRUCT();
                }
                nNOTES++;
            }

            for (i = 0; i < nNOTES; i++)
            {
                if (pNote[i] == null)
                {
                    pNote[i] = new NOTE_STRUCT();
                }
                for (int j = 0; j < 16; j++)
                {
                    pNote[i].vctData[CHANNEL_16BEAT].Add(0);
                    pNote[i].nPtr[CHANNEL_16BEAT] = 0;
                }
                pNote[i].nMaxCount = 16;
                for (int j = 0; j < BMS_MAX_PART; j++)
                {
                    if (pNote[i].nMaxCount < pNote[i].vctData[j].Count)
                        pNote[i].nMaxCount = pNote[i].vctData[j].Count;
                }
                nMaxCountAtNote[i] = 1;
                for (int j = 0; j < pNote[i].vctData.Length; j++)
                {
                    if (pNote[i].vctData[j].Count > nMaxCountAtNote[i])
                    {
                        nMaxCountAtNote[i] = pNote[i].vctData[j].Count;
                    }
                }
            }

            // 各Noteで、Ch 0x11～0xFF, 257～306 までで使用されているChをリスト化（負荷軽減）
            bool bUseCh_5x6x = false;
            for (i = 0; i < nNOTES; i++)
            {
                anChannels[i] = new List<int>();
                for(int j=0x11; j<257+50; j++)
                {
                    if (j == 256) continue; // 16beat管理用の予約席
                    if (pNote[i].vctData[j].Count > 0)
                    {
                        if (j >= 0x51 && j <= 0x6F) bUseCh_5x6x = true;
                        if ((j > 256) && ((pNote[i].vctData[j].Count == 1) && (pNote[i].vctData[j][0] == 0)))
                        {}
                        else
                        {
                            anChannels[i].Add(j);
                        }
                    }
                }
            }

            // LongNote
            if (bUseCh_5x6x == true)
            {
                nTypeLongNote = 1;
                uint nStart = 0;
                int nBAR = -1, nDIV = -1;
                // #LNTYPE 1
                for (int j = 0x51; j < 0x6F; j++)
                {
                    for (i = 0; i < nNOTES; i++)
                    {
                        for (int k=0; k< pNote[i].vctData[j].Count; k++)
                        {
                            if (nStart == 0)
                            {
                                if (pNote[i].vctData[j][k] != 0)
                                {
                                    nStart = pNote[i].vctData[j][k]; // 最初の音符
                                    nBAR = i; nDIV = k; // 開始位置を控えておく
                                }
                            } else
                            {
                                if (pNote[i].vctData[j][k] != 0)
                                {
                                    if (pNote[i].vctData[j][k] == nStart)
                                    {
                                        // ロングノートのペアだったのでデータ変更
                                        pNote[nBAR].vctData[j][nDIV] |= (1 << 16); // 開始記号
                                        for(int v1=nBAR; v1<=i; v1++)
                                        {
                                            if (pNote[v1].vctData[j].Count == 0)
                                            {
                                                pNote[v1].vctData[j].Add((1 << 17));
                                                anChannels[v1].Add(j);
                                            }
                                            for(int v2=0; v2<pNote[v1].vctData[j].Count; v2++)
                                            {
                                                if ((v1 == nBAR) && (v2 <= nDIV)) continue;
                                                if ((v1 == i) && (v2 >= k)) continue;
                                                pNote[v1].vctData[j][v2] |= (1 << 17); // 中間記号
                                            }
                                        }
                                        pNote[i].vctData[j][k] |= (1 << 18); // 終端記号
                                        nStart = 0; // 探索終了
                                    }
                                    else
                                    {
                                        // 違う音だったのでロングノートは解消。ここから探索再開
                                        nStart = pNote[i].vctData[j][k]; // 最初の音符
                                        nBAR = i; nDIV = k; // 開始位置を控えておく
                                    }
                                }
                            }
                        }
                    }
                    if (nStart != 0)
                    {
                        // 終端記号が無いならラストまでロングノート
                        pNote[nBAR].vctData[j][nDIV] |= (1 << 16); // 開始記号
                        for (int v1 = nBAR; v1 <= i; v1++)
                        {
                            if (pNote[v1].vctData[j].Count == 0)
                            {
                                pNote[v1].vctData[j].Add((1 << 17));
                                anChannels[v1].Add(j);
                            }
                            for (int v2 = 0; v2 < pNote[v1].vctData[j].Count; v2++)
                            {
                                if ((v1 == nBAR) && (v2 < nDIV)) continue;
                                pNote[v1].vctData[j][v2] |= (1 << 17); // 中間記号
                            }
                        }
                        pNote[i].vctData[j][pNote[i].vctData[j].Count - 1] |= (1 << 18); // 終端記号
                        nStart = 0; // 探索終了
                    }
                }
            }
            else
            {
                // #LNOBJ zz
                // LongNoteチェック
                uint nStart = 0;
                int nBAR = -1, nDIV = -1;
                for (int j = 0x11; j < 0x2F; j++)
                {
                    for (i = nNOTES - 1; i >= 0 ; i--)
                    {
                        for (int k = pNote[i].vctData[j].Count - 1; k >= 0; k--)
                        {
                            if (nStart == 0)
                            {
                                if (pNote[i].vctData[j][k] == nLongNote) // ZZ発見！
                                {
                                    nStart = pNote[i].vctData[j][k]; // 最初の音符
                                    nBAR = i; nDIV = k; // 開始位置を控えておく
                                }
                            }
                            else
                            {
                                if (pNote[i].vctData[j][k] != 0)
                                {
                                    if (pNote[i].vctData[j][k] != nLongNote)
                                    {
                                        // ロングノートのペアだったのでデータ変更
                                        pNote[nBAR].vctData[j][nDIV] |= (1 << 18); // 終端記号
                                        for (int v1 = nBAR; v1 >= i; v1--)
                                        {
                                            if (pNote[v1].vctData[j].Count == 0)
                                            {
                                                pNote[v1].vctData[j].Add((1 << 17));
                                                anChannels[v1].Add(j);
                                            }
                                            for (int v2 = pNote[v1].vctData[j].Count - 1; v2 >= 0; v2--)
                                            {
                                                if ((v1 == nBAR) && (v2 >= nDIV)) continue;
                                                if ((v1 == i) && (v2 <= k)) continue;
                                                pNote[v1].vctData[j][v2] |= (1 << 17); // 中間記号
                                            }
                                        }
                                        pNote[i].vctData[j][k] |= (1 << 16); // 開始記号
                                        nStart = 0; // 探索終了
                                    }
                                    else
                                    {
                                        // ZZだったのでロングノートは解消。ここから探索再開
                                        nStart = pNote[i].vctData[j][k]; // 最初の音符
                                        nBAR = i; nDIV = k; // 開始位置を控えておく
                                    }
                                }
                            }
                        }
                    }
                    if (nStart != 0)
                    {
                        // 終端記号が無いなら終わり
                        nStart = 0; // 探索終了
                    }
                }
            }

            m_nMode = MODE.LOAD_COMPLETE;
            return;

        _ERROR_TBMSPLAY_LOAD:;
            m_nMode = MODE.LOAD_ABORT;
        }

        private void BMSPlayThread() // 再生ルーチン
        {
            int j;
            llNow = 0;
            llOrg = DX.GetNowHiPerformanceCount();
            m_dShortenVal = 1.0;
            int[] nLONGpNote = new int[BMS_MAX_PART];
            for (j = 0; j < BMS_MAX_PART; j++)
            {
                nLONGpNote[j] = -1;
            }

            double dWait = 0.0;
            double dMinInterval = 0.0;
            m_llFirstTime = -1;
            m_dVRIntervalBAR = 4.0 * 60000000.0 / (double)fBPM;
            m_dVRPositionBAR = 0.0;
            m_nCurrentNote = -1;
            m_dShortenVal2 = 1.0;
            m_dStopLength = 0;

            for (j = 0; j < nNOTES; j++)
            {
                for (int i = 0; i < BMS_MAX_PART; i++)
                {
                    pNote[j].nPtr[i] = 0;
                }
            }

            nCurrentBAR = -1;
            nCurrentBeat16 = 0;
            llNow = DX.GetNowHiPerformanceCount();
            llOrg = llNow;
            m_nMode = MODE.PLAY_PLAYING;

            for (j = -1; j < nNOTES; j++)
            {
                nCurrentBAR = j; // 小節用のカウンタ
                nCurrentBeat16 = 0; // 最初のビート16
                int nPart, i = 0;
                llNow = DX.GetNowHiPerformanceCount();
                llOrg = llNow;

                bool bTempoNoChange = true;

                double dVRIntervalBAR = 4.0 * 60000000.0 / (double)fBPM; // 1小節の"理論上"の長さ
                double dVRPositionBAR = 0.0; // 仮想バー時間のどこにいるか
                double dFirstCounter = dVRIntervalBAR / 16.0;
                if (j < 0)
                {
                    nCurrentBeat16 = i; // 0 - 15
                    goto NEXT_STEP;
                }
                if (j == 0)
                {
                    if (m_llFirstTime < 0)
                    {
                        m_llFirstTime = DX.GetNowHiPerformanceCount();
                    }
                }

                if (j > nNOTES_true)
                {
                    if (fBPM < 20.0F)
                        fBPM = 130.0F;
                    dVRIntervalBAR = 4.0 * 60000000.0 / (double)fBPM;
                }

                // 2ch 小節の短縮化(小節あたり1度だけ。最初のみ)
                nPart = 0x02;
                m_dShortenVal = 1.0;
                if (pNote[j].dShorten > 0.0 && pNote[j].nPtr[nPart] == 0)
                {
                    if (pNote[j].dShorten > 0.0)
                    {
                        bTempoNoChange = false;
                        m_dShortenVal = (double)pNote[j].dShorten; // 0.5とか
                        dVRIntervalBAR *= m_dShortenVal; // 一小節の時間を短縮 (LR2では短くなるだけだがbPでは高速化を意味する)
                    }
                }

            BAR_LOOP:
                dWait = 0.0;
                dMinInterval = 999999.0;
                if (j < 0) // 開始直後は -1
                {
                    if (dVRPositionBAR > dFirstCounter)
                    {
                        i++;
                        dFirstCounter += dVRIntervalBAR / 16.0;
                    }
                    nCurrentBeat16 = i; // 0 - 15
                    goto NEXT_STEP;
                }

                uint l = 0;
                long llTmp = 0;
                double dDelta = 0;

                // 3ch テンポ自然数変更(節の途中で何度も発生する)
                nPart = 0x03;
                if (pNote[j].vctData[nPart].Count > 0 && pNote[j].nPtr[nPart] >= 0)
                {
                    dDelta = dVRIntervalBAR / (double)pNote[j].vctData[nPart].Count;
                    if (dVRPositionBAR >= (double)pNote[j].nPtr[nPart] * dDelta)
                    {
                        if (pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])] > 0)
                        {
                            if ((float)(pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])]) > 0.0)
                            {
                                bTempoNoChange = false;
                                fBPM = (float)(pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])]);

                                dVRIntervalBAR = 4.0 * 60000000.0 / (double)fBPM * m_dShortenVal;
                                dDelta = dVRIntervalBAR / (double)pNote[j].vctData[nPart].Count;
                                dVRPositionBAR = dDelta * (double)pNote[j].nPtr[nPart];
                            }
                            else
                            {
                                CDxCommon.OUTPUTLOG("BPM変更値が異常であるため変更しませんでした。");
                            }
                            llOrg = llNow;
                        }
                        if (++pNote[j].nPtr[nPart] >= pNote[j].vctData[nPart].Count) pNote[j].nPtr[nPart] = -1;
                    }
                    if (pNote[j].nPtr[nPart] >= 0)
                    {
                        llTmp = (long)((double)pNote[j].nPtr[nPart] * dDelta - dVRPositionBAR);
                        if (dMinInterval > (double)llTmp) dMinInterval = (double)llTmp;
                    }
                }

                // 8ch テンポ変更
                nPart = 0x08;
                if (pNote[j].vctData[nPart].Count > 0 && pNote[j].nPtr[nPart] >= 0)
                {
                    dDelta = dVRIntervalBAR / (double)pNote[j].vctData[nPart].Count;
                    if (dVRPositionBAR >= (double)pNote[j].nPtr[nPart] * dDelta)
                    {
                        if (pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])] > 0)
                        {
                            if (afBPMs[pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])]] > 0.0)
                            {
                                bTempoNoChange = false;
                                fBPM = afBPMs[pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])]];
                                dVRIntervalBAR = 4.0 * 60000000.0 / (double)fBPM * m_dShortenVal;
                                dDelta = dVRIntervalBAR / (double)pNote[j].vctData[nPart].Count;
                                dVRPositionBAR = dDelta * (double)pNote[j].nPtr[nPart];
                            }
                            else
                            {
                                CDxCommon.OUTPUTLOG("BPM変更値が異常であるため変更しませんでした。");
                            }
                            llOrg = llNow;
                        }
                        if (++pNote[j].nPtr[nPart] >= pNote[j].vctData[nPart].Count) pNote[j].nPtr[nPart] = -1;
                    }
                    if (pNote[j].nPtr[nPart] >= 0)
                    {
                        llTmp = (long)((double)pNote[j].nPtr[nPart] * dDelta - dVRPositionBAR);
                        if (dMinInterval > (double)llTmp) dMinInterval = (double)llTmp;
                    }
                }

                // 16BEAT用にCHANNEL256を使用する
                // bPの場合はどんな長さであれ一小節を16分割で処理する
                nPart = CHANNEL_16BEAT;
                if (pNote[j].vctData[nPart].Count > 0 && pNote[j].nPtr[nPart] >= 0)
                {
                    dDelta = dVRIntervalBAR / (double)pNote[j].vctData[nPart].Count; // * m_dShortenVal; // bPは  * m_dShortenVal は不要
                    if (dVRPositionBAR >= (double)pNote[j].nPtr[nPart] * dDelta)
                    {
                        l = pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])];
                        i = pNote[j].nPtr[nPart]; // 16等分しているのでiが16BEATに一致する
                        if (++pNote[j].nPtr[nPart] >= pNote[j].vctData[nPart].Count) pNote[j].nPtr[nPart] = -1;
                    }
                    if (pNote[j].nPtr[nPart] >= 0)
                    {
                        llTmp = (long)((double)pNote[j].nPtr[nPart] * dDelta - dVRPositionBAR);
                        if (dMinInterval > (double)llTmp) dMinInterval = (double)llTmp;
                    }
                }
                nCurrentBeat16 = i;


                // 0x11 - FF, 257-306
                int nVol = -1;
                for (int t=0; t<anChannels[j].Count; t++)
                {
                    nPart = anChannels[j][t];
                    if (nPart >= 0x11 && nPart <= 0x1F) nVol = bSilenceCh[0] ? 0 : 255;
                    if (nPart >= 0x21 && nPart <= 0x2F) nVol = bSilenceCh[1] ? 0 : 255;
                    if (nPart >= 0x31 && nPart <= 0x3F) nVol = bSilenceCh[2] ? 0 : 255;
                    if (nPart >= 0x41 && nPart <= 0x4F) nVol = bSilenceCh[3] ? 0 : 255;
                    if (nPart >= 0x51 && nPart <= 0x5F) nVol = bSilenceCh[4] ? 0 : 255;
                    if (nPart >= 0x61 && nPart <= 0x6F) nVol = bSilenceCh[5] ? 0 : 255;
                    if (nPart >= 0xD1 && nPart <= 0xDF) nVol = bSilenceCh[6] ? 0 : 255;
                    if (nPart >= 0xE1 && nPart <= 0xEF) nVol = bSilenceCh[7] ? 0 : 255;
                    if (nPart >=257) nVol = bSilenceCh[8] ? 0 : BMS_BGM_VOL;
                    if (pNote[j].vctData[nPart].Count > 0 && pNote[j].nPtr[nPart] >= 0)
                    {
                        dDelta = dVRIntervalBAR / (double)pNote[j].vctData[nPart].Count;
                        if (dVRPositionBAR >= (double)pNote[j].nPtr[nPart] * dDelta)
                        {
                            l = pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])];
                            if (l > 0)
                            {
                                if (nPart >= 257) // バックコーラス(ここはlは36x36以下である)
                                {
                                    if (pSound[l] != null)
                                    {
                                        pSound[l].SetVolume(nVol);
                                        pSound[l].Play(nPart);
                                    }
                                }
                                else
                                { // 鳴らす音
                                    if (m_bBGM)
                                    {
//                                        if (pSound[l & 0xFFFF] != null)
                                        {
                                            // ロングノートか？
                                            if (nLONGpNote[nPart] == -1)
                                            {
                                                if (l >= (1 << 16)) // 64Kが開始、128KがLN中、256KがLN終了位置
                                                {
                                                    if ((l & ((1 << 17) | (1 << 18))) == 0)
                                                    {
                                                        // 始まり
                                                        l = l & 0xFFFF;
                                                        nLONGpNote[nPart] = (int)l;
                                                        if (pSound[l] != null)
                                                        {
                                                            pSound[l].SetVolume(nVol);
                                                            pSound[l].Play(nPart);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // 始まっていないのに途中 or 終了なら無効。単に鳴らす
                                                        nLONGpNote[nPart] = -1;
                                                        l = l & 0xFFFF;
                                                        if (pSound[l] != null)
                                                        {
                                                            pSound[l].SetVolume(nVol);
                                                            pSound[l].Play(nPart);
                                                        }
                                                    }
                                                } else
                                                {
                                                    // ロングノートではない (記述ミスも含め)
                                                    l = l & 0xFFFF;
                                                    nLONGpNote[nPart] = -1;
                                                    if (pSound[l] != null)
                                                    {
                                                        pSound[l].SetVolume(nVol);
                                                        pSound[l].Play(nPart);
                                                    }
                                                }
                                            } else
                                            {
                                                // LN中
                                                if ((l & (1 << 16)) != 0)
                                                {
                                                    // 始まっているのに始まりはダメ
                                                    nLONGpNote[nPart] = -1;
                                                    l = l & 0xFFFF;
                                                    if (pSound[l] != null)
                                                    {
                                                        pSound[l].SetVolume(nVol);
                                                        pSound[l].Play(nPart);
                                                    }
                                                }
                                                else
                                                if ((l & (1 << 17)) != 0)
                                                {
                                                    // 途中なら何もしない
                                                }else
                                                if ((l & (1 << 18)) != 0)
                                                {
                                                    // LN終わり
                                                    nLONGpNote[nPart] = -1;
                                                    // pSound[l].Stop(); // 別に止める必要はないか？
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (++pNote[j].nPtr[nPart] >= pNote[j].vctData[nPart].Count) pNote[j].nPtr[nPart] = -1;
                        }
                        if (pNote[j].nPtr[nPart] >= 0)
                        {
                            llTmp = (long)((double)pNote[j].nPtr[nPart] * dDelta - dVRPositionBAR);
                            if (dMinInterval > (double)llTmp) dMinInterval = (double)llTmp;
                        }
                    }
                }


                // 4ch BGA
                nPart = 0x04;
                if (pNote[j].vctData[nPart].Count > 0 && pNote[j].nPtr[nPart] >= 0)
                {
                    dDelta = dVRIntervalBAR / (double)pNote[j].vctData[nPart].Count;
                    if (dVRPositionBAR >= (double)pNote[j].nPtr[nPart] * dDelta)
                    {
                        if (pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])] > 0)
                        {
                            uint nm = pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])];
                            if (nm != 0)
                            {
                                // 静止画だろうが何だろうが止めてリセット
                                if (m_pSpr_Part4 != null)
                                {
                                    DX.PauseMovieToGraph(m_pSpr_Part4.Handle);
                                    DX.SeekMovieToGraph(m_pSpr_Part4.Handle, 0);
                                    m_pSpr_Part4 = null;
                                }
                                // 静止画だろうが何だろうが再生
                                if (pBGA[nm] != null)
                                {
                                    DX.PlayMovieToGraph(pBGA[nm].Handle);
                                    m_pSpr_Part4 = pBGA[nm];
                                }
                            }
                        }
                        if (++pNote[j].nPtr[nPart] >= pNote[j].vctData[nPart].Count) pNote[j].nPtr[nPart] = -1;
                    }
                    if (pNote[j].nPtr[nPart] >= 0)
                    {
                        llTmp = (long)((double)pNote[j].nPtr[nPart] * dDelta - dVRPositionBAR);
                        if (dMinInterval > (double)llTmp) dMinInterval = (double)llTmp;
                    }
                }

                // 6ch Poor BGA 変更
                nPart = 0x06;
                if (pNote[j].vctData[nPart].Count > 0 && pNote[j].nPtr[nPart] >= 0)
                {
                    dDelta = dVRIntervalBAR / (double)pNote[j].vctData[nPart].Count;
                    if (dVRPositionBAR >= (double)pNote[j].nPtr[nPart] * dDelta)
                    {
                        if (pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])] > 0)
                        {
                            int nm = (int)pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])];
                            if (nm != 0)
                            {
                                m_nPoorNum = nm;
                            }
                        }
                        if (++pNote[j].nPtr[nPart] >= pNote[j].vctData[nPart].Count) pNote[j].nPtr[nPart] = -1;
                    }
                    if (pNote[j].nPtr[nPart] >= 0)
                    {
                        llTmp = (long)((double)pNote[j].nPtr[nPart] * dDelta - dVRPositionBAR);
                        if (dMinInterval > (double)llTmp) dMinInterval = (double)llTmp;
                    }
                }

                // 7ch レイヤーBGA
                nPart = 0x07;
                if (pNote[j].vctData[nPart].Count > 0 && pNote[j].nPtr[nPart] >= 0)
                {
                    dDelta = dVRIntervalBAR / (double)pNote[j].vctData[nPart].Count;
                    if (dVRPositionBAR >= (double)pNote[j].nPtr[nPart] * dDelta)
                    {
                        if (pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])] > 0)
                        {
                            uint nm = pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])];
                            if (nm != 0)
                            {
                                // 静止画だろうが何だろうが止めてリセット
                                if (m_pSpr_Part7 != null)
                                {
                                    DX.PauseMovieToGraph(m_pSpr_Part7.Handle);
                                    DX.SeekMovieToGraph(m_pSpr_Part7.Handle, 0);
                                    m_pSpr_Part7 = null;
                                }
                                // 静止画だろうが何だろうが再生
                                if (pBGA[nm] != null)
                                {
                                    DX.PlayMovieToGraph(pBGA[nm].Handle);
                                    m_pSpr_Part7 = pBGA[nm];
                                }
                            }
                        }
                        if (++pNote[j].nPtr[nPart] >= pNote[j].vctData[nPart].Count) pNote[j].nPtr[nPart] = -1;
                    }
                    if (pNote[j].nPtr[nPart] >= 0)
                    {
                        llTmp = (long)((double)pNote[j].nPtr[nPart] * dDelta - dVRPositionBAR);
                        if (dMinInterval > (double)llTmp) dMinInterval = (double)llTmp;
                    }
                }

                nPart = 0x09; // 9ch #STOPnn　192分音符でどれだけ停止するか
                if (pNote[j].vctData[nPart].Count > 0 && pNote[j].nPtr[nPart] >= 0)
                {
                    bTempoNoChange = false;
                    dDelta = dVRIntervalBAR / (double)pNote[j].vctData[nPart].Count;
                    if (dVRPositionBAR >= (double)pNote[j].nPtr[nPart] * dDelta)
                    {
                        int nWait_STOP = anSTOP[pNote[j].vctData[nPart][(pNote[j].nPtr[nPart])]];
                        if (nWait_STOP < 0) nWait_STOP = 0;
                        bTempoNoChange = false;
                        dWait = (60000000.0 / (double)(fBPM * 48.0f)) * (double)nWait_STOP; // 192分音符何個かを指定
                        if (++pNote[j].nPtr[nPart] >= pNote[j].vctData[nPart].Count) pNote[j].nPtr[nPart] = -1;
                    }
                    if (pNote[j].nPtr[nPart] >= 0)
                    {
                        llTmp = (long)((double)pNote[j].nPtr[nPart] * dDelta - dVRPositionBAR);
                        if (dMinInterval > (double)llTmp) dMinInterval = (double)llTmp;
                    }
                }

                if (dWait > 0.0)
                {
                    long tw, twNX;
                    twNX = llNow + (long)dWait;
                    while ((tw = DX.GetNowHiPerformanceCount()) < twNX)
                    {
                        long ll = twNX - tw;
                        // CPU空回しを避けるためにSleepを実行するのだが、1000を指定しても2500とかかかったり)ので、
                        // 5000くらい余裕がないとスリープさせない
                        if (ll > 5000)
                            Thread.Sleep(1);
                        else
                            Thread.Sleep(0);
                        /*
                         * // この中は固定だからいいか
                        lock (lockObj)
                        {
                            m_nCurrentNote = nCurrentBAR;
                            m_dShortenVal2 = m_dShortenVal;
                            m_dVRIntervalBAR = dVRIntervalBAR;
                            m_dVRPositionBAR = dVRPositionBAR;
                            m_dStopLength = dWait;
                        }
                        */
                    }
                    llNow = DX.GetNowHiPerformanceCount();
                    llOrg = llNow;
                }

            NEXT_STEP:

                // このへんはテキトー (CPUをブン回したくない)
                if ((dMinInterval > 5000) && (bTempoNoChange == true) && (fBPM < 300.0) && (dWait == 0.0))
                {
                    Thread.Sleep(1);
                }

                if (bEXIT)
                {
                    m_nMode = MODE.PLAY_STOP;
                    goto _EXIT_PLAY;
                }

                llNow = DX.GetNowHiPerformanceCount();

                if ((dMinInterval < 0) && (i < 15) && (j >= 0))
                {
                    goto BAR_LOOP;
                }

                int nMCaN = (j >= 0) ? nMaxCountAtNote[j] : 16;
                if (dVRIntervalBAR / (double)nMCaN < 1.0)
                {
                    dVRPositionBAR += dVRIntervalBAR / (double)nMCaN; // BPM>10000000とかの場合こっち
                }
                else
                {
                    dVRPositionBAR += (double)(llNow - llOrg); // 普通はこっち
                }
                lock (lockObj)
                {
                    m_nCurrentNote = nCurrentBAR;
                    m_dShortenVal2 = m_dShortenVal;
                    m_dVRIntervalBAR = dVRIntervalBAR;
                    m_dVRPositionBAR = dVRPositionBAR;
                    m_dStopLength = dWait;
                }

                llOrg = llNow;
                if ((dVRPositionBAR >= dVRIntervalBAR) && (i == 15))
                    goto NEXT_BAR;
                else
                    goto BAR_LOOP;

                NEXT_BAR:;
            }

            m_nMode = MODE.PLAY_COMPLETE;
        _EXIT_PLAY:
            nCurrentBAR = 0;
            nCurrentBeat16 = 0;
        }
    }
}