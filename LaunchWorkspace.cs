using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string hubExe = Path.Combine(dir, "WorkspaceHub.exe");
            if (File.Exists(hubExe))
            {
                var psi = new ProcessStartInfo(hubExe, "--launch-all");
                psi.WorkingDirectory = dir;
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            else
            {
                MessageBox.Show("WorkspaceHub.exe が見つかりませんでした: " + hubExe, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("一括起動に失敗しました: " + ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
