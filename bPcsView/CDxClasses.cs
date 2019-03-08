using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using DxLibDLL;


public class CDxCommon
{
    static bool bFirst = true;
    static string m_strEXEFolder = "";
    static public object lockObject4 = new object(); // OUTPUTDEBUG

    // EXEファイルのパス (末尾が \ にはならないので注意)
    public static string GetAppPath()
    {
        return System.IO.Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location);
    }

    public static void Init()
    {
        m_strEXEFolder = System.IO.Directory.GetCurrentDirectory();
        if (m_strEXEFolder.EndsWith(@"\") == false)
        {
            m_strEXEFolder += @"\";
        }
    }

    public static string IntTo36(int n) // 0～1295を0～ZZに
    {
        string tbl = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return tbl.Substring(n / 36, 1) + tbl.Substring(n % 36, 1);
    }

    // https://qiita.com/gushwell/items/f08d0e71fa0480dbb396
    // 最小公倍数
    // var lcm = Lcm(28, 34);
    public static int Lcm(int a, int b)
    {
        return a * b / Gcd(a, b);
    }

    // ユークリッドの互除法 
    public static int Gcd(int a, int b)
    {
        if (a < b)
            return Gcd(b, a);
        while (b != 0)
        {
            var remainder = a % b;
            a = b;
            b = remainder;
        }
        return a;
    }
    
    public static void OUTPUTLOG(string text)
    {
        lock(lockObject4)
        {
            if (m_strEXEFolder == "")
                Init();

            string s = m_strEXEFolder;
            s += @"Log2.txt";

            StreamWriter writer = null;
            if (bFirst == true)
            {
                writer = new StreamWriter(s, false);
                bFirst = false;
            }
            else
            {
                try
                {
                    writer = new StreamWriter(s, true);
                }
                catch (Exception ee)
                {
                    MessageBox.Show("ログエラーです。" + ee.Message);
                }
            }

            writer.WriteLine(text);
            writer.Close();
        }
    }
}

public class CDxLibSound : IDisposable
{
    int m_nSoundHandle;
    int m_nVolume;
    int m_nChannel;
    string m_strFile;
    List<int> m_listChannel = null;
    List<int> m_listID = null;

    public CDxLibSound()
    {
	    m_nSoundHandle = -1;
	    m_nVolume = -1;
	    m_strFile = "";
        m_nChannel = -1;
        m_listChannel = new List<int>();
        m_listID = new List<int>();
    }

    public void Dispose()
    {
        if (m_nSoundHandle >= 0)
        {
            DX.DeleteSoundMem(m_nSoundHandle);
        }

        if (m_listChannel != null)
        {
            for (int i = 0; i < m_listChannel.Count; i++)
            {
                DX.DeleteSoundMem(m_listChannel[i]);
            }
            m_listChannel.Clear();
            m_listChannel = null;
        }

        if (m_listID != null)
        {
            m_listID.Clear();
            m_listID = null;
        }

        m_nSoundHandle = -1;
        m_nVolume = -1;
        m_strFile = "";
    }

    public bool DuplicateChannel(int nChannel)
    {
        if (m_nChannel == -1)
        {
            m_nChannel = nChannel;
            return true;
        }
        if (nChannel == m_nChannel)
            return true;
    
        if (m_listChannel == null)
        {
            m_listChannel = new List<int>();
            m_listID = new List<int>();
        }
        for (int i=0; i<m_listChannel.Count; i++)
        {
            if (m_listChannel[i] == nChannel)
                return true;
        }
        int nID = DX.DuplicateSoundMem(m_nSoundHandle, 1);
        if (nID > 0)
        {
            m_listChannel.Add(nChannel);
            m_listID.Add(nID);
            return true;
        }
        return false;
    }

    // ハンドルを返す (boolではない)
    public int Open(string sFile, int nCount = 1)
    {
        if (m_strFile != "" && m_strFile == sFile) return m_nSoundHandle;

        Dispose();

        if (File.Exists(sFile) == false) return -1;

        long d = new FileInfo(sFile).Length;
        if (d < 100) return -1; // 0byte empty があったので対策

        string s = Path.GetFileName(sFile);
        m_nSoundHandle = DX.LoadSoundMem(sFile, nCount);
        m_nVolume = 255;
        m_nChannel = -1;

        m_strFile = sFile;

        return m_nSoundHandle;
    }

    public int Handle { get { return m_nSoundHandle; } }

    public int Play(int nChannel)
    {
        if (m_nSoundHandle < 0)
            return DX.FALSE;

        if ((nChannel == 0 || m_listChannel == null) || (m_nChannel == nChannel))
        {
            return DX.PlaySoundMem(m_nSoundHandle, DX.DX_PLAYTYPE_BACK, DX.TRUE);
        }
        else
        {
            for(int i=0; i<m_listChannel.Count; i++)
            {
                if (nChannel == m_listChannel[i])
                {
                    return DX.PlaySoundMem(m_listID[i], DX.DX_PLAYTYPE_BACK, DX.TRUE);
                }
            }
        }
        return DX.FALSE;
    }

    public int Stop(bool bAllChannel = false)
    {
        if (m_nSoundHandle < 0)
            return DX.FALSE;

        if (m_listID != null)
        {
            for (int i = 0; i < m_listID.Count; i++)
            {
                DX.StopSoundMem(m_listID[i]);
            }
        }

        return DX.StopSoundMem(m_nSoundHandle);
    }

    public bool Playing { get { return PlayingSub(); } }
    private bool PlayingSub()
    {
        if (m_nSoundHandle < 0)
            return false;

        if (DX.CheckSoundMem(m_nSoundHandle) == DX.TRUE) return true;
        if (m_listID != null)
        {
            for (int i = 0; i < m_listID.Count; i++)
            {
                if (DX.CheckSoundMem(m_listID[i]) == DX.TRUE) return true;
            }
        }
        return false;
    }

    // 設定する音量( 0 ～ 255 )
    public int SetVolume(int nVolume)
    {
        if (m_nSoundHandle < 0)
            return DX.FALSE;

        m_nVolume = nVolume;
        if (m_listID != null)
        {
            for (int i = 0; i < m_listID.Count; i++)
            {
                DX.ChangeVolumeSoundMem(nVolume, m_listID[i]);
            }
        }
        return DX.ChangeVolumeSoundMem(nVolume, m_nSoundHandle);
    }

}

public class CDxLibGraph : IDisposable
{
    public const int GRAPH_TYPE_NONE = 0;
    public const int GRAPH_TYPE_BITMAP = 1;
    public const int GRAPH_TYPE_MOVIE = 2;

    int m_nGraphHandle;
    string m_strFile;
    int m_nGraphType;

	public int Handle { get { return m_nGraphHandle; } }
    public int GraphType { get { return m_nGraphType; } set { m_nGraphType = value; } }
    public string File { get { return m_strFile; } }

    public CDxLibGraph()
    {
	    m_nGraphHandle = -1;
	    m_strFile = "";
    }

    public void Dispose()
    {
        int nResult = 0;
        if (m_nGraphHandle >= 0)
        {
            nResult = DX.DeleteGraph(m_nGraphHandle);
        }

        m_nGraphHandle = -1;
        m_strFile = "";
    }

    public int Load(string strFile) // ハンドルを返す
    {
        if ((m_strFile != "" && m_strFile == strFile) && (m_nGraphType == GRAPH_TYPE_BITMAP)) return m_nGraphHandle;
        Dispose();

        m_nGraphHandle = DX.LoadGraph(strFile);
        if (m_nGraphHandle >= 0)
        {
            m_strFile = strFile;

            int nFrames = DX.GetMovieTotalFrameToGraph(m_nGraphHandle);
            if (nFrames > 0)
            {
                m_nGraphType = GRAPH_TYPE_MOVIE;
            }
            else
            if (nFrames == 0)
            {
                m_nGraphType = GRAPH_TYPE_BITMAP;
            }
            else
            {
                m_nGraphType = GRAPH_TYPE_NONE;
            }
        }

        return m_nGraphHandle;
    }
}
