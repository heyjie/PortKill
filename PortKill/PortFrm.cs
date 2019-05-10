using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PortKill
{
    public partial class PortFrm : Form
    {
        public PortFrm()
        {
            InitializeComponent();
            tabControl1.DrawItem += TabControl1_DrawItem;
            //根据进程显示图片
            dataGridViewPort.CellFormatting += DataGridViewPort_CellFormatting;
        }

        private void DataGridViewPort_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dataGridViewPort.Columns[e.ColumnIndex].Name.Equals("ProcessIcon")&& dataGridViewPort.Rows[e.RowIndex].Cells["ProcessIcon"].Value==null)
            {
                int pid = (int)dataGridViewPort.Rows[e.RowIndex].Cells["Pid"].Value;
                Icon ico = null;
                IntPtr pHandle = IntPtr.Zero;
                pHandle = OpenProcess(PROCESS_ALL_ACCESS, 0, pid);
                StringBuilder sb = new StringBuilder(MAX_PATH);
                GetModuleFileNameEx(pHandle, IntPtr.Zero, sb, MAX_PATH);
                CloseHandle(pHandle);
                //获取图标
                IntPtr[] largeIcons, smallIcons;
                int IconCount = ExtractIconEx(sb.ToString(), -1, null, null, 0);
                largeIcons = new IntPtr[IconCount];
                smallIcons = new IntPtr[IconCount];
                ExtractIconEx(sb.ToString(), 0, largeIcons, smallIcons, IconCount);
                IntPtr icon = new IntPtr(0);
                try
                {
                    icon = largeIcons[0];
                    ico = Icon.FromHandle(icon);
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine(ex.Message);
                }
                dataGridViewPort.Rows[e.RowIndex].Cells["ProcessIcon"].Value = ico;
                //调整行高到能正常显示缩略图
            }
        }

        private void TabControl1_DrawItem(object sender, DrawItemEventArgs e)
        {
            SolidBrush back = new SolidBrush(Color.FromArgb(64, 64, 64));
            SolidBrush white = new SolidBrush(Color.FromArgb(51, 153, 255));
            Rectangle rec = tabControl1.GetTabRect(0);
            e.Graphics.FillRectangle(back, rec);
            Rectangle rec1 = tabControl1.GetTabRect(1);
            e.Graphics.FillRectangle(back, rec1);
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            for (int i = 0; i < tabControl1.TabPages.Count; i++)
            {
                Rectangle rec2 = tabControl1.GetTabRect(i);
                e.Graphics.DrawString(tabControl1.TabPages[i].Text, new Font("微软雅黑", 9), white, rec2, sf);
            }
        }

        //查杀端口
        private void t_btn_kill_Click(object sender, EventArgs e)
        {
            int port;
            bool b = int.TryParse(textBoxFind.Text, out port);
            if (!b)
            {
                MessageBox.Show("请输入正确的监听端口");
                return;
            }
            Process p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            List<int> list_pid = GetPidByPort(p, port);
            if (list_pid.Count == 0)
            {
                MessageBox.Show(string.Format("没有进程占用{0}端口", port));
                return;
            }
            List<string> list_process = GetProcessNameByPid(p, list_pid);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("占用" + port + "端口的进程有:");
            foreach (var item in list_process)
            {
                sb.Append(item + "\r\n");
            }
            sb.AppendLine("是否要结束这些进程？");

            ProgramFrm frm = new ProgramFrm();
            frm.textBox1.AppendText(sb.ToString());
            if (frm.ShowDialog(this) == DialogResult.Cancel)
                return;
            PidKill(p, list_pid);
            MessageBox.Show("操作完成");
            button2_Click(sender, e);
        }

        /// <summary>结束占用端口进程
        /// 
        /// </summary>
        /// <param name="p">控制台</param>
        /// <param name="list_pid">进程pid</param>
        private static void PidKill(Process p, List<int> list_pid)
        {
            foreach (var item in list_pid)
            {
                p.Start();
                p.StandardInput.WriteLine("taskkill /pid " + item + " /f");
                p.StandardInput.WriteLine("exit");
            }
            p.Close();
        }

        /// <summary>获取端口占用进程pid
        /// 
        /// </summary>
        /// <param name="p">控制台</param>
        /// <param name="port">端口号</param>
        /// <returns>该端口所有进程pid</returns>
        private static List<int> GetPidByPort(Process p, int port)
        {
            int result;
            bool b = true;
            p.Start();
            p.StandardInput.WriteLine(string.Format("netstat -ano|find \"{0}\"", port));
            p.StandardInput.WriteLine("exit");
            StreamReader reader = p.StandardOutput;
            //读取第一行cmd执行结果
            string strLine = reader.ReadLine();
            List<int> list_pid = new List<int>();
            while (!reader.EndOfStream)
            {
                strLine = strLine.Trim();
                if (strLine.Length > 0 && ((strLine.Contains("TCP") || strLine.Contains("UDP"))))
                {
                    Regex r = new Regex(@"\s+");
                    //用指定格式拆分字符串
                    string[] strArr = r.Split(strLine);
                    if (strArr.Length > 4)
                    {
                        //转换为int
                        b = int.TryParse(strArr[4], out result);
                        //获取端口号
                        string Port = strArr[1].Split(':')[1];
                        if (b && !list_pid.Contains(result)&& Port.Equals(port.ToString()))
                            //转换int成功并且列表无相同元素，则添加该pid到列表
                            list_pid.Add(result);
                    }
                }
                //读取下一行cmd执行结果
                strLine = reader.ReadLine();
            }
            p.WaitForExit();
            reader.Close();
            p.Close();
            return list_pid;
        }

        /// <summary>通过端口占用进程pid获取占用程序名
        /// 
        /// </summary>
        /// <param name="p">控制台</param>
        /// <param name="list_pid">进程pid</param>
        /// <returns>和进程相关程序</returns>
        private static List<string> GetProcessNameByPid(Process p, List<int> list_pid)
        {
            List<string> list_process = new List<string>();
            foreach (var pid in list_pid)
            {
                p.Start();
                p.StandardInput.WriteLine(string.Format("tasklist |find \"{0}\"", pid));
                p.StandardInput.WriteLine("exit");
                StreamReader reader = p.StandardOutput;//截取输出流
                string strLine = reader.ReadLine();//每次读取一行
                while (!reader.EndOfStream)
                {
                    strLine = strLine.Trim();
                    if (strLine.Length > 0 && ((strLine.Contains(".exe"))))
                    {
                        Regex r = new Regex(@"\s+");
                        string[] strArr = r.Split(strLine);
                        if (strArr.Length > 0)
                        {
                            //添加程序名到列表
                            list_process.Add(strArr[0]);
                        }
                    }
                    //读取下一行执行结果
                    strLine = reader.ReadLine();
                }
                p.WaitForExit();
                reader.Close();
            }
            p.Close();

            return list_process;
        }

        //开始扫描按钮
        private void button2_Click(object sender, EventArgs e)
        {
            buttonEnd.Enabled = false;
            buttonStart.Enabled = false;
            labelMsg.Text = "正在扫描端口";
            progressBar.Value = 0;
            dataGridViewPort.Rows.Clear();
            new Thread(() => {
                MonitorTcpConnections();
            }).Start();
        }

        //关闭进程按钮
        private void button1_Click(object sender, EventArgs e)
        {
            var selectRows = dataGridViewPort.SelectedRows;
            if (selectRows.Count == 0)
            {
                return;
            }
            int pid = (int)selectRows[0].Cells[4].Value;
            Process process = Process.GetProcessById(pid);
            if (process == null)
            {
                MessageBox.Show("获取进程相关信息失败,请尝试重新操作");
                return;
            }
            try
            {
                process.Kill();
                process.WaitForExit();
                process.Close();
                dataGridViewPort.Rows.Remove(selectRows[0]);
                MessageBox.Show("操作成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "结束进程失败");
            }
        }

        //获取所有端口占用信息
        private void MonitorTcpConnections()
        {
            List<Scan> scanList = new List<Scan>();
            for (int j = 0; j < 50; j++)
            {
                TcpConnectionTableHelper.MIB_TCPROW_OWNER_PID[] tcpProgressInfoTable = TcpConnectionTableHelper.GetAllTcpConnections();
                int tableRowCount = tcpProgressInfoTable.Length;
                Process[] ps = Process.GetProcesses();
                List<Process> processes = new List<Process>(ps);
                for (int i = 0; i < tableRowCount; i++)
                {
                    TcpConnectionTableHelper.MIB_TCPROW_OWNER_PID row = tcpProgressInfoTable[i];
                    string source = string.Format("{0}:{1}", TcpConnectionTableHelper.GetIpAddress(row.localAddr), row.LocalPort);
                    string dest = string.Format("{0}:{1}", TcpConnectionTableHelper.GetIpAddress(row.remoteAddr), row.RemotePort);
                    Process proname =processes.Find(x=> 
                    {
                        if (x.Id == row.owningPid) return true;
                        else return false;
                    });
                    Scan scan = new Scan("TCP", source, dest, (TCP_CONNECTION_STATE)row.state, row.owningPid, proname.ProcessName);
                    scanList.Add(scan);
                }
                this.Invoke(new Action(() => {
                    progressBar.Increment(2);
                }));
            }
            
            this.Invoke(new Action(() => {
                foreach (var item in scanList)
                {
                    int index = dataGridViewPort.Rows.Add();
                    dataGridViewPort.Rows[index].Cells["Proto"].Value = item.type;
                    dataGridViewPort.Rows[index].Cells["LocalAddress"].Value = item.source;
                    dataGridViewPort.Rows[index].Cells["ForeignAddress"].Value = item.dest;
                    dataGridViewPort.Rows[index].Cells["State"].Value = item.state;
                    dataGridViewPort.Rows[index].Cells["PID"].Value = item.owningPid;
                    dataGridViewPort.Rows[index].Cells["PName"].Value = item.ProcessName;
                }
                buttonEnd.Enabled = true;
                buttonStart.Enabled = true;
                labelMsg.Text = "扫描完成";
                progressBar.Value = 0;
            }));
        }

        /// <summary>执行CMD命令
        /// 
        /// </summary>
        /// <param name="command">命令</param>
        private string RunCmd(string command)
        {
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.UseShellExecute = false;    //是否使用操作系统shell启动
            p.StartInfo.RedirectStandardInput = true;//接受来自调用程序的输入信息
            p.StartInfo.RedirectStandardOutput = true;//由调用程序获取输出信息
            p.StartInfo.RedirectStandardError = true;//重定向标准错误输出
            p.StartInfo.CreateNoWindow = true;//不显示程序窗口
            p.Start();//启动程序
            //向cmd窗口发送输入信息
            p.StandardInput.WriteLine(command + "&exit");
            p.StandardInput.AutoFlush = true;
            //p.StandardInput.WriteLine("exit");
            //向标准输入写入要执行的命令。这里使用&是批处理命令的符号，表示前面一个命令不管是否执行成功都执行后面(exit)命令，如果不执行exit命令，后面调用ReadToEnd()方法会假死
            //同类的符号还有&&和||前者表示必须前一个命令执行成功才会执行后面的命令，后者表示必须前一个命令执行失败才会执行后面的命令
            //获取cmd窗口的输出信息
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();//等待程序执行完退出进程
            p.Close();
            return output;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (this.comboBox1.SelectedIndex)
            {
                case 0:
                    textBox4.Text = "netstat -ano";
                    break;
                case 1:
                    textBox4.Text = "netstat -ano|find \"\"";
                    break;
                case 2:
                    textBox4.Text = "taskkill /pid \"\" /f";
                    break;
                case 3:
                    textBox4.Text = "tasklist |find \"\"";
                    break;
                default:
                    break;
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            textBox3.Text=RunCmd(textBox4.Text);
        }

        private void textBoxFind_Enter(object sender, EventArgs e)
        {
            textBoxFind.Text=string.Empty;
        }

        private bool IsWindowExist(IntPtr handle)
        {
            //判断窗口是否存在
            return (!(GetWindow(new HandleRef(this, handle), 4) != IntPtr.Zero) && IsWindowVisible(new HandleRef(this, handle)));
        }

        private const int MAX_PATH = 260;
        public const int PROCESS_ALL_ACCESS = 0x000F0000 | 0x00100000 | 0xFFF;

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr GetWindow(HandleRef hWnd, int uCmd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool IsWindowVisible(HandleRef hWnd);

        public delegate bool EnumThreadWindowsCallback(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool EnumWindows(EnumThreadWindowsCallback callback, IntPtr extraData);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowThreadProcessId(HandleRef handle, out int processId);

        [DllImport("Kernel32.dll")]
        public extern static IntPtr OpenProcess(int fdwAccess, int fInherit, int IDProcess);

        [DllImport("shell32.dll", EntryPoint = "GetModuleFileName")]
        private static extern uint GetModuleFileName(IntPtr hModule, [Out] StringBuilder lpszFileName, int nSize);

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern int ExtractIconEx(string lpszFile, int niconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, int nIcons);

        [DllImport("psapi.dll")]
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In] [MarshalAs(UnmanagedType.U4)] int nSize);
        [DllImport("Kernel32.dll")]
        public extern static bool CloseHandle(IntPtr hObject);
    }
}
