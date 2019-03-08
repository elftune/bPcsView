using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace bPcsView
{
    public partial class Form2 : Form
    {
        public int Value { get {return nCP;} set { nCP = value; } }
        int nCP = -1;

        public Form2()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.None;
            string s = textBox1.Text;
            if (s == "")
            {
                MessageBox.Show("CodePage番号を入力してください。", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            try
            {
                Encoding enc = Encoding.GetEncoding(Int32.Parse(s));
                nCP = Int32.Parse(s);
            }
            catch
            {
                MessageBox.Show("この数字に対応するCodePageは取得できません。", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Form2_Shown(object sender, EventArgs e)
        {
            textBox1.Text = nCP.ToString();
            textBox1.Focus();
        }
    }
}
