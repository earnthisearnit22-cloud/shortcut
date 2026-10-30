using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms; // Screen, OpenFileDialog, NotifyIcon
using System.Windows.Interop; // HwndSource
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using Application = System.Windows.Application;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace WorkspaceHubApp
{
    // ==========================================
    // データモデル
    // ==========================================
    public class ToolItem
    {
        public string Id;
        public string Name;
        public string Path;
        public string Arguments;
        public int MonitorIndex; // -1: Primary, 0, 1...
        public string LayoutType; // Maximize, LeftHalf, RightHalf, TopHalf, BottomHalf, TopLeft, TopRight, BottomLeft, BottomRight, Center, Custom, None
        public int CustomX;
        public int CustomY;
        public int CustomWidth;
        public int CustomHeight;
        public int DelayMs;
        public bool Enabled;
        public string BrowserMode; // NewWindow, App, Tab

        public ToolItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "新規ツール";
            Path = "";
            Arguments = "";
            MonitorIndex = -1;
            LayoutType = "LeftHalf";
            CustomX = 0;
            CustomY = 0;
            CustomWidth = 800;
            CustomHeight = 600;
            DelayMs = 300;
            Enabled = true;
            BrowserMode = "NewWindow";
        }

        public ToolItem Clone()
        {
            return new ToolItem
            {
                Id = this.Id,
                Name = this.Name,
                Path = this.Path,
                Arguments = this.Arguments,
                MonitorIndex = this.MonitorIndex,
                LayoutType = this.LayoutType,
                CustomX = this.CustomX,
                CustomY = this.CustomY,
                CustomWidth = this.CustomWidth,
                CustomHeight = this.CustomHeight,
                DelayMs = this.DelayMs,
                Enabled = this.Enabled,
                BrowserMode = this.BrowserMode
            };
        }
    }

    public class Profile
    {
        public string Id;
        public string Name;
        public List<ToolItem> Items;

        public Profile()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "デフォルト";
            Items = new List<ToolItem>();
        }
    }

    public class AppConfig
    {
        public string SelectedProfileId;
        public List<Profile> Profiles;
        public bool EnableGlobalHotkey = true;
        public bool StartWithWindows = false;
        public bool MinimizeToTrayOnClose = true;

        public AppConfig()
        {
            Profiles = new List<Profile>();
            EnableGlobalHotkey = true;
            StartWithWindows = false;
            MinimizeToTrayOnClose = true;
        }

        public static AppConfig CreateDefault()
        {
            var config = new AppConfig();
            var p1 = new Profile { Name = "💻 デフォルト (サンプル)" };

            // サンプル1: メモ帳（左半分）
            p1.Items.Add(new ToolItem
            {
                Name = "メモ帳",
                Path = "notepad.exe",
                MonitorIndex = -1,
                LayoutType = "LeftHalf",
                DelayMs = 200,
                Enabled = true
            });

            // サンプル2: 電卓（右上1/4）
            p1.Items.Add(new ToolItem
            {
                Name = "電卓",
                Path = "calc.exe",
                MonitorIndex = -1,
                LayoutType = "TopRight",
                DelayMs = 300,
                Enabled = true
            });

            // サンプル3: ブラウザ (右半分またはサブ画面)
            int subMonitor = Screen.AllScreens.Length > 1 ? 1 : -1;
            p1.Items.Add(new ToolItem
            {
                Name = "Google 検索",
                Path = "https://www.google.com",
                MonitorIndex = subMonitor,
                LayoutType = (subMonitor == 1) ? "Maximize" : "BottomRight",
                DelayMs = 300,
                Enabled = true
            });

            config.Profiles.Add(p1);
            config.SelectedProfileId = p1.Id;
            return config;
        }
    }

    // ==========================================
    // 超軽量・自己完結型 JSON シリアライザ
    // ==========================================
    public static class SimpleJson
    {
        public static string SerializeConfig(AppConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"SelectedProfileId\": \"" + Escape(config.SelectedProfileId) + "\",");
            sb.AppendLine("  \"EnableGlobalHotkey\": " + (config.EnableGlobalHotkey ? "true" : "false") + ",");
            sb.AppendLine("  \"StartWithWindows\": " + (config.StartWithWindows ? "true" : "false") + ",");
            sb.AppendLine("  \"MinimizeToTrayOnClose\": " + (config.MinimizeToTrayOnClose ? "true" : "false") + ",");
            sb.AppendLine("  \"Profiles\": [");
            for (int i = 0; i < config.Profiles.Count; i++)
            {
                var p = config.Profiles[i];
                sb.AppendLine("    {");
                sb.AppendLine("      \"Id\": \"" + Escape(p.Id) + "\",");
                sb.AppendLine("      \"Name\": \"" + Escape(p.Name) + "\",");
                sb.AppendLine("      \"Items\": [");
                for (int j = 0; j < p.Items.Count; j++)
                {
                    var item = p.Items[j];
                    sb.AppendLine("        {");
                    sb.AppendLine("          \"Id\": \"" + Escape(item.Id) + "\",");
                    sb.AppendLine("          \"Name\": \"" + Escape(item.Name) + "\",");
                    sb.AppendLine("          \"Path\": \"" + Escape(item.Path) + "\",");
                    sb.AppendLine("          \"Arguments\": \"" + Escape(item.Arguments) + "\",");
                    sb.AppendLine("          \"MonitorIndex\": " + item.MonitorIndex + ",");
                    sb.AppendLine("          \"LayoutType\": \"" + Escape(item.LayoutType) + "\",");
                    sb.AppendLine("          \"CustomX\": " + item.CustomX + ",");
                    sb.AppendLine("          \"CustomY\": " + item.CustomY + ",");
                    sb.AppendLine("          \"CustomWidth\": " + item.CustomWidth + ",");
                    sb.AppendLine("          \"CustomHeight\": " + item.CustomHeight + ",");
                    sb.AppendLine("          \"DelayMs\": " + item.DelayMs + ",");
                    sb.AppendLine("          \"Enabled\": " + (item.Enabled ? "true" : "false") + ",");
                    sb.AppendLine("          \"BrowserMode\": \"" + Escape(string.IsNullOrEmpty(item.BrowserMode) ? "NewWindow" : item.BrowserMode) + "\"");
                    sb.Append("        }");
                    if (j < p.Items.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("      ]");
                sb.Append("    }");
                if (i < config.Profiles.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static AppConfig DeserializeConfig(string json)
        {
            var config = new AppConfig();
            if (string.IsNullOrEmpty(json)) return config;

            try
            {
                config.SelectedProfileId = ExtractString(json, "SelectedProfileId");
                config.EnableGlobalHotkey = ExtractBool(json, "EnableGlobalHotkey", true);
                config.StartWithWindows = ExtractBool(json, "StartWithWindows", false);
                config.MinimizeToTrayOnClose = ExtractBool(json, "MinimizeToTrayOnClose", true);
                int profStart = json.IndexOf("\"Profiles\"");
                if (profStart >= 0)
                {
                    int arrStart = json.IndexOf("[", profStart);
                    if (arrStart >= 0)
                    {
                        var profileBlocks = SplitObjects(json, arrStart);
                        foreach (var pBlock in profileBlocks)
                        {
                            var p = new Profile();
                            p.Id = ExtractString(pBlock, "Id");
                            p.Name = ExtractString(pBlock, "Name");

                            int itemsStart = pBlock.IndexOf("\"Items\"");
                            if (itemsStart >= 0)
                            {
                                int iArrStart = pBlock.IndexOf("[", itemsStart);
                                if (iArrStart >= 0)
                                {
                                    var itemBlocks = SplitObjects(pBlock, iArrStart);
                                    foreach (var iBlock in itemBlocks)
                                    {
                                        var item = new ToolItem();
                                        item.Id = ExtractString(iBlock, "Id");
                                        item.Name = ExtractString(iBlock, "Name");
                                        item.Path = ExtractString(iBlock, "Path");
                                        item.Arguments = ExtractString(iBlock, "Arguments");
                                        item.MonitorIndex = ExtractInt(iBlock, "MonitorIndex", -1);
                                        item.LayoutType = ExtractString(iBlock, "LayoutType");
                                        if (string.IsNullOrEmpty(item.LayoutType)) item.LayoutType = "LeftHalf";
                                        item.CustomX = ExtractInt(iBlock, "CustomX", 0);
                                        item.CustomY = ExtractInt(iBlock, "CustomY", 0);
                                        item.CustomWidth = ExtractInt(iBlock, "CustomWidth", 800);
                                        item.CustomHeight = ExtractInt(iBlock, "CustomHeight", 600);
                                        item.DelayMs = ExtractInt(iBlock, "DelayMs", 300);
                                        item.Enabled = ExtractBool(iBlock, "Enabled", true);
                                        item.BrowserMode = ExtractString(iBlock, "BrowserMode");
                                        if (string.IsNullOrEmpty(item.BrowserMode)) item.BrowserMode = "NewWindow";
                                        p.Items.Add(item);
                                    }
                                }
                            }
                            config.Profiles.Add(p);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("JSON parse error: " + ex.Message);
            }

            if (config.Profiles.Count == 0)
            {
                return AppConfig.CreateDefault();
            }
            return config;
        }

        private static List<string> SplitObjects(string text, int arrayStartIndex)
        {
            var list = new List<string>();
            int depth = 0;
            int objStart = -1;
            for (int i = arrayStartIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        list.Add(text.Substring(objStart, i - objStart + 1));
                        objStart = -1;
                    }
                }
                else if (c == ']' && depth == 0)
                {
                    break;
                }
            }
            return list;
        }

        private static string ExtractString(string block, string key)
        {
            string pattern = "\"" + key + "\"";
            int idx = block.IndexOf(pattern);
            if (idx < 0) return "";
            int colon = block.IndexOf(":", idx + pattern.Length);
            if (colon < 0) return "";
            int q1 = block.IndexOf("\"", colon);
            if (q1 < 0) return "";
            int q2 = block.IndexOf("\"", q1 + 1);
            while (q2 > 0 && block[q2 - 1] == '\\')
            {
                q2 = block.IndexOf("\"", q2 + 1);
            }
            if (q2 < 0) return "";
            return Unescape(block.Substring(q1 + 1, q2 - q1 - 1));
        }

        private static int ExtractInt(string block, string key, int defaultVal)
        {
            string pattern = "\"" + key + "\"";
            int idx = block.IndexOf(pattern);
            if (idx < 0) return defaultVal;
            int colon = block.IndexOf(":", idx + pattern.Length);
            if (colon < 0) return defaultVal;
            int end = block.IndexOfAny(new char[] { ',', '}', '\r', '\n' }, colon + 1);
            if (end < 0) end = block.Length;
            string val = block.Substring(colon + 1, end - colon - 1).Trim();
            int res;
            if (int.TryParse(val, out res)) return res;
            return defaultVal;
        }

        private static bool ExtractBool(string block, string key, bool defaultVal)
        {
            string pattern = "\"" + key + "\"";
            int idx = block.IndexOf(pattern);
            if (idx < 0) return defaultVal;
            int colon = block.IndexOf(":", idx + pattern.Length);
            if (colon < 0) return defaultVal;
            int end = block.IndexOfAny(new char[] { ',', '}', '\r', '\n' }, colon + 1);
            if (end < 0) end = block.Length;
            string val = block.Substring(colon + 1, end - colon - 1).Trim().ToLower();
            if (val.Contains("true")) return true;
            if (val.Contains("false")) return false;
            return defaultVal;
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Unescape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\r", "\r").Replace("\\n", "\n");
        }
    }

    // ==========================================
    // Win32 ウィンドウ制御エンジン
    // ==========================================
    public static class WindowEngine
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const int SW_SHOWNORMAL = 1;
        public const int SW_RESTORE = 9;
        public const int SW_MAXIMIZE = 3;
        public const int SW_MINIMIZE = 6;

        public const uint WM_CLOSE = 0x0010;
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public class OpenWindowInfo
        {
            public IntPtr Handle;
            public string Title;
            public string ProcessName;
            public string ExePath;
            public uint ProcessId;
            public RECT Rect;
            public int MonitorIndex;

            public override string ToString()
            {
                return string.Format("[{0}] {1} ({2}x{3} @ モニター{4})", ProcessName, Title, Rect.Width, Rect.Height, MonitorIndex >= 0 ? (MonitorIndex + 1).ToString() : "主");
            }
        }

        public static List<OpenWindowInfo> GetOpenWindows()
        {
            var list = new List<OpenWindowInfo>();
            int currentPid = Process.GetCurrentProcess().Id;

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, 256);
                    string title = sb.ToString();
                    RECT rect;
                    GetWindowRect(hWnd, out rect);

                    // 幅・高さが150ピクセル以上の有意義なトップレベルウィンドウのみ
                    if (!string.IsNullOrEmpty(title) && rect.Width >= 150 && rect.Height >= 150 && title != "Program Manager")
                    {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);

                        // 自プロセスのウィンドウは除外
                        if ((int)pid == currentPid) return true;

                        string procName = "";
                        string exePath = "";
                        try
                        {
                            using (var proc = Process.GetProcessById((int)pid))
                            {
                                procName = proc.ProcessName;
                                try { exePath = proc.MainModule.FileName; } catch { }
                            }
                        }
                        catch { }

                        if (string.IsNullOrEmpty(exePath) && !string.IsNullOrEmpty(procName))
                        {
                            exePath = procName + ".exe";
                        }

                        int monIdx = GetMonitorIndexForRect(rect);
                        list.Add(new OpenWindowInfo
                        {
                            Handle = hWnd,
                            Title = title,
                            ProcessName = procName,
                            ExePath = exePath,
                            ProcessId = pid,
                            Rect = rect,
                            MonitorIndex = monIdx
                        });
                    }
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }

        public static int GetMonitorIndexForRect(RECT rect)
        {
            int centerX = rect.Left + rect.Width / 2;
            int centerY = rect.Top + rect.Height / 2;
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                var s = Screen.AllScreens[i];
                if (s.Bounds.Contains(centerX, centerY))
                {
                    return i;
                }
            }
            return -1;
        }

        public static Screen GetTargetScreen(int monitorIndex)
        {
            if (monitorIndex >= 0 && monitorIndex < Screen.AllScreens.Length)
            {
                return Screen.AllScreens[monitorIndex];
            }
            return Screen.PrimaryScreen;
        }

        public static bool IsBrowserTarget(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string p = path.Trim().ToLower();
            if (p.StartsWith("http://") || p.StartsWith("https://") || p.StartsWith("www.")) return true;
            if (p.EndsWith("chrome.exe") || p.Contains("chrome.exe")) return true;
            if (p.EndsWith("msedge.exe") || p.Contains("msedge.exe")) return true;
            if (p.EndsWith("brave.exe") || p.Contains("brave.exe")) return true;
            if (p.EndsWith("firefox.exe") || p.Contains("firefox.exe")) return true;
            return false;
        }

        public static string ResolvePath(string rawPath, out string extraArgs)
        {
            extraArgs = null;
            if (string.IsNullOrWhiteSpace(rawPath)) return "";

            string trimmed = rawPath.Trim();

            // 1. 環境変数の展開 (%APPDATA%, %LOCALAPPDATA%, %USERPROFILE% 等)
            string expanded = Environment.ExpandEnvironmentVariables(trimmed);

            // 2. URL または Windows設定URI (http:, https:, ms-settings: 等) はそのまま返却
            if (expanded.Contains("://") || expanded.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase))
            {
                return expanded;
            }

            // 3. 展開後のパスで直接存在確認
            if (File.Exists(expanded) || Directory.Exists(expanded))
            {
                return expanded;
            }

            // 4. ユーザー名を含むパスの自動置換 (例: C:\Users\earnt\... を現在のユーザーのプロファイルへ置換)
            try
            {
                string currentUserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                int usersIdx = expanded.IndexOf(@"\Users\", StringComparison.OrdinalIgnoreCase);
                if (usersIdx >= 0)
                {
                    int nextSlash = expanded.IndexOf('\\', usersIdx + 7);
                    if (nextSlash > 0)
                    {
                        string relPart = expanded.Substring(nextSlash + 1);
                        string remapped = Path.Combine(currentUserProfile, relPart);
                        if (File.Exists(remapped) || Directory.Exists(remapped))
                        {
                            return remapped;
                        }
                    }
                }
            }
            catch { }

            // 5. 相対パスの場合、アプリの配置ディレクトリ基準で解決
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string combined = Path.Combine(baseDir, expanded);
                if (File.Exists(combined) || Directory.Exists(combined))
                {
                    return Path.GetFullPath(combined);
                }
            }
            catch { }

            // 6. 一般的なアプリの代表的インストール先フォールバック (Discord など)
            try
            {
                string fileName = Path.GetFileName(expanded).ToLowerInvariant();
                if (fileName.Contains("discord"))
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string[] discordCandidates = new string[]
                    {
                        Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs\Discord Inc\Discord.lnk"),
                        Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs\Discord.lnk"),
                        Path.Combine(localAppData, @"Discord\Update.exe")
                    };
                    foreach (var c in discordCandidates)
                    {
                        if (File.Exists(c))
                        {
                            if (c.EndsWith("Update.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                extraArgs = "--processStart Discord.exe";
                            }
                            return c;
                        }
                    }
                }
            }
            catch { }

            // 7. PATH環境変数からの探索 (notepad.exe, calc.exe 等)
            try
            {
                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    string[] dirs = pathEnv.Split(';');
                    foreach (var dir in dirs)
                    {
                        if (string.IsNullOrWhiteSpace(dir)) continue;
                        try
                        {
                            string candidate = Path.Combine(dir.Trim(), expanded);
                            if (File.Exists(candidate)) return candidate;
                            if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(candidate + ".exe"))
                            {
                                return candidate + ".exe";
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return expanded;
        }

        public static string FindBrowserExecutable()
        {
            string[] candidates = new string[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe"),
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Mozilla Firefox\firefox.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Mozilla Firefox\firefox.exe")
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            // レジストリ探索
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"))
                {
                    if (key != null)
                    {
                        string p = key.GetValue("") as string;
                        if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
                    }
                }
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe"))
                {
                    if (key != null)
                    {
                        string p = key.GetValue("") as string;
                        if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
                    }
                }
            }
            catch { }

            return "msedge.exe";
        }

        public static void GetSafeScreenBounds(Screen screen, out int safeLeft, out int safeTop, out int safeRight, out int safeBottom)
        {
            var area = screen.WorkingArea;
            safeLeft = area.Left;
            safeTop = area.Top;
            safeRight = area.Right;
            safeBottom = area.Bottom;

            bool hasScreenToRight = false;
            bool hasScreenToLeft = false;
            bool hasScreenAbove = false;
            bool hasScreenBelow = false;

            foreach (var other in Screen.AllScreens)
            {
                if (other.DeviceName == screen.DeviceName) continue;

                // 水平方向の隣接判定（垂直方向の領域が重なっているか）
                bool verticalOverlap = (other.Bounds.Top < screen.Bounds.Bottom) && (other.Bounds.Bottom > screen.Bounds.Top);
                if (verticalOverlap)
                {
                    if (other.Bounds.Left >= screen.Bounds.Right - 10)
                    {
                        hasScreenToRight = true;
                    }
                    if (other.Bounds.Right <= screen.Bounds.Left + 10)
                    {
                        hasScreenToLeft = true;
                    }
                }

                // 垂直方向の隣接判定（水平方向の領域が重なっているか）
                bool horizontalOverlap = (other.Bounds.Left < screen.Bounds.Right) && (other.Bounds.Right > screen.Bounds.Left);
                if (horizontalOverlap)
                {
                    if (other.Bounds.Top >= screen.Bounds.Bottom - 10)
                    {
                        hasScreenBelow = true;
                    }
                    if (other.Bounds.Bottom <= screen.Bounds.Top + 10)
                    {
                        hasScreenAbove = true;
                    }
                }
            }

            // 隣接モニターと接する境目には2ピクセルの安全マージンを設け、
            // Windowsの不可視リサイズ境界やドロップシャドウが隣のモニターへ跨ぐのを厳格に防止する
            if (hasScreenToRight) safeRight -= 2;
            if (hasScreenToLeft) safeLeft += 2;
            if (hasScreenBelow) safeBottom -= 2;
            if (hasScreenAbove) safeTop += 2;
        }

        public static void ClampToSafeBounds(int safeLeft, int safeTop, int safeRight, int safeBottom, ref int x, ref int y, ref int w, ref int h)
        {
            int availW = Math.Max(100, safeRight - safeLeft);
            int availH = Math.Max(100, safeBottom - safeTop);

            if (w > availW) w = availW;
            if (h > availH) h = availH;
            if (w < 100) w = 100;
            if (h < 100) h = 100;

            // X軸クランプ
            if (x < safeLeft)
            {
                x = safeLeft;
            }
            if (x + w > safeRight)
            {
                if (x >= safeLeft && x < safeRight)
                {
                    // 左端が画面内にある場合は幅を縮めて右端を収める
                    w = safeRight - x;
                }
                else
                {
                    // 左端も画面外なら右端を基準に左へ寄せる
                    x = safeRight - w;
                    if (x < safeLeft)
                    {
                        x = safeLeft;
                        w = safeRight - safeLeft;
                    }
                }
            }

            // Y軸クランプ
            if (y < safeTop)
            {
                y = safeTop;
            }
            if (y + h > safeBottom)
            {
                if (y >= safeTop && y < safeBottom)
                {
                    // 上端が画面内にある場合は高さを縮めて下端を収める
                    h = safeBottom - y;
                }
                else
                {
                    // 上端も画面外なら下端を基準に上へ寄せる
                    y = safeBottom - h;
                    if (y < safeTop)
                    {
                        y = safeTop;
                        h = safeBottom - safeTop;
                    }
                }
            }
        }

        public static void CalculateRect(ToolItem item, out int x, out int y, out int w, out int h, out bool isMaximize)
        {
            var screen = GetTargetScreen(item.MonitorIndex);
            isMaximize = (item.LayoutType == "Maximize");

            int safeLeft, safeTop, safeRight, safeBottom;
            GetSafeScreenBounds(screen, out safeLeft, out safeTop, out safeRight, out safeBottom);

            int availW = Math.Max(200, safeRight - safeLeft);
            int availH = Math.Max(200, safeBottom - safeTop);

            x = safeLeft;
            y = safeTop;
            w = availW;
            h = availH;

            switch (item.LayoutType)
            {
                case "LeftHalf":
                    w = availW / 2;
                    break;
                case "RightHalf":
                    x = safeLeft + availW / 2;
                    w = availW - availW / 2;
                    break;
                case "TopHalf":
                    h = availH / 2;
                    break;
                case "BottomHalf":
                    y = safeTop + availH / 2;
                    h = availH - availH / 2;
                    break;
                case "TopLeft":
                    w = availW / 2;
                    h = availH / 2;
                    break;
                case "TopRight":
                    x = safeLeft + availW / 2;
                    w = availW - availW / 2;
                    h = availH / 2;
                    break;
                case "BottomLeft":
                    y = safeTop + availH / 2;
                    w = availW / 2;
                    h = availH - availH / 2;
                    break;
                case "BottomRight":
                    x = safeLeft + availW / 2;
                    y = safeTop + availH / 2;
                    w = availW - availW / 2;
                    h = availH - availH / 2;
                    break;
                case "Center":
                    w = (int)(availW * 0.7);
                    h = (int)(availH * 0.7);
                    x = safeLeft + (availW - w) / 2;
                    y = safeTop + (availH - h) / 2;
                    break;
                case "Custom":
                    x = item.CustomX;
                    y = item.CustomY;
                    w = item.CustomWidth;
                    h = item.CustomHeight;

                    // ポータブルフォールバック: 保存された座標が対象モニターの表示領域と完全に交差しない場合
                    // （例: 送信元の別モニター座標が保存されている場合）
                    // 画面内に安全に収まるよう自動センタリング＆適正サイズ調整
                    var itemRect = new System.Drawing.Rectangle(x, y, Math.Max(1, w), Math.Max(1, h));
                    if (!screen.Bounds.IntersectsWith(itemRect))
                    {
                        w = Math.Min(availW, Math.Max(400, (int)(availW * 0.75)));
                        h = Math.Min(availH, Math.Max(300, (int)(availH * 0.75)));
                        x = safeLeft + (availW - w) / 2;
                        y = safeTop + (availH - h) / 2;
                    }
                    break;
            }

            // モニター境界厳格クランプ: どのような設定でも隣接モニターへのはみ出しを防止
            ClampToSafeBounds(safeLeft, safeTop, safeRight, safeBottom, ref x, ref y, ref w, ref h);
        }

        public static void ApplyLayout(IntPtr hWnd, ToolItem item)
        {
            if (hWnd == IntPtr.Zero || item.LayoutType == "None") return;

            int x, y, w, h;
            bool isMaximize;
            CalculateRect(item, out x, out y, out w, out h, out isMaximize);

            var screen = GetTargetScreen(item.MonitorIndex);
            var area = screen.WorkingArea;

            ShowWindow(hWnd, SW_RESTORE);
            Thread.Sleep(50);

            if (isMaximize)
            {
                SetWindowPos(hWnd, IntPtr.Zero, area.Left + 50, area.Top + 50, 600, 400, SWP_SHOWWINDOW | SWP_NOZORDER);
                ShowWindow(hWnd, SW_MAXIMIZE);
                return;
            }

            SetWindowPos(hWnd, IntPtr.Zero, x, y, w, h, SWP_SHOWWINDOW | SWP_NOZORDER);

            // 適用後の実ウィンドウ境界検証とオーバーフロー補正 (隣接画面へのはみ出し・跨ぎを二重防御)
            Thread.Sleep(50);
            RECT actualRect;
            if (GetWindowRect(hWnd, out actualRect))
            {
                int safeLeft, safeTop, safeRight, safeBottom;
                GetSafeScreenBounds(screen, out safeLeft, out safeTop, out safeRight, out safeBottom);

                bool needsAdjustment = false;
                int adjX = x;
                int adjY = y;
                int adjW = w;
                int adjH = h;

                // もし実ウィンドウが safeRight を超えて隣のモニターにはみ出している場合
                if (actualRect.Right > safeRight)
                {
                    int diff = actualRect.Right - safeRight;
                    if (adjW - diff >= 150)
                    {
                        adjW -= diff;
                    }
                    else
                    {
                        adjX -= diff;
                    }
                    needsAdjustment = true;
                }

                // もし実ウィンドウが safeLeft を超えて左側のモニターにはみ出している場合
                if (actualRect.Left < safeLeft)
                {
                    int diff = safeLeft - actualRect.Left;
                    adjX += diff;
                    if (adjX + adjW > safeRight) adjW = safeRight - adjX;
                    needsAdjustment = true;
                }

                // 下端のはみ出し補正
                if (actualRect.Bottom > safeBottom)
                {
                    int diff = actualRect.Bottom - safeBottom;
                    if (adjH - diff >= 150)
                    {
                        adjH -= diff;
                    }
                    else
                    {
                        adjY -= diff;
                    }
                    needsAdjustment = true;
                }

                // 上端のはみ出し補正
                if (actualRect.Top < safeTop)
                {
                    int diff = safeTop - actualRect.Top;
                    adjY += diff;
                    if (adjY + adjH > safeBottom) adjH = safeBottom - adjY;
                    needsAdjustment = true;
                }

                if (needsAdjustment)
                {
                    SetWindowPos(hWnd, IntPtr.Zero, adjX, adjY, adjW, adjH, SWP_SHOWWINDOW | SWP_NOZORDER);
                }
            }
        }

        public static IntPtr LaunchAndFindWindow(ToolItem item, HashSet<IntPtr> usedHwnds = null)
        {
            if (string.IsNullOrEmpty(item.Path)) return IntPtr.Zero;
            if (usedHwnds == null) usedHwnds = new HashSet<IntPtr>();

            int targetX, targetY, targetW, targetH;
            bool isMaximize;
            CalculateRect(item, out targetX, out targetY, out targetW, out targetH, out isMaximize);

            // 起動前ウィンドウのスナップショット
            var beforeWindows = new HashSet<IntPtr>();
            EnumWindows((h, l) =>
            {
                if (IsWindowVisible(h)) beforeWindows.Add(h);
                return true;
            }, IntPtr.Zero);

            string pathTrim = item.Path.Trim();
            bool isBrowser = IsBrowserTarget(pathTrim);

            try
            {
                var psi = new ProcessStartInfo();

                if (isBrowser)
                {
                    string browserExe;
                    string targetUrl = "";

                    if (pathTrim.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        pathTrim.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        pathTrim.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    {
                        browserExe = FindBrowserExecutable();
                        targetUrl = pathTrim;
                    }
                    else
                    {
                        browserExe = pathTrim;
                        targetUrl = item.Arguments ?? "";
                    }

                    string mode = string.IsNullOrEmpty(item.BrowserMode) ? "NewWindow" : item.BrowserMode;
                    string browserArgs = "";

                    if (mode == "App")
                    {
                        browserArgs = string.Format("--app=\"{0}\" --window-position={1},{2} --window-size={3},{4}", targetUrl, targetX, targetY, targetW, targetH);
                    }
                    else if (mode == "Tab")
                    {
                        browserArgs = "\"" + targetUrl + "\"";
                    }
                    else // NewWindow (デフォルト)
                    {
                        browserArgs = string.Format("--new-window --window-position={0},{1} --window-size={2},{3} \"{4}\"", targetX, targetY, targetW, targetH, targetUrl);
                    }

                    psi.FileName = browserExe;
                    psi.Arguments = browserArgs;
                }
                else
                {
                    string extraArgs;
                    string resolved = ResolvePath(pathTrim, out extraArgs);
                    psi.FileName = resolved;

                    string finalArgs = item.Arguments ?? "";
                    if (!string.IsNullOrEmpty(extraArgs))
                    {
                        finalArgs = string.IsNullOrEmpty(finalArgs) ? extraArgs : extraArgs + " " + finalArgs;
                    }
                    if (!string.IsNullOrEmpty(finalArgs))
                    {
                        psi.Arguments = finalArgs;
                    }

                    try
                    {
                        if (File.Exists(resolved))
                        {
                            psi.WorkingDirectory = Path.GetDirectoryName(resolved);
                        }
                        else
                        {
                            psi.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                        }
                    }
                    catch { }
                }

                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                // もしブラウザ起動でエラーになった場合、OS既定のブラウザでURL直接起動にフォールバック
                if (isBrowser)
                {
                    try
                    {
                        string fallbackUrl = (pathTrim.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || pathTrim.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || pathTrim.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) ? pathTrim : item.Arguments;
                        if (!string.IsNullOrEmpty(fallbackUrl))
                        {
                            Process.Start(new ProcessStartInfo(fallbackUrl) { UseShellExecute = true });
                        }
                    }
                    catch { }
                }
                Console.WriteLine("起動失敗: " + ex.Message);
                return IntPtr.Zero;
            }

            // ウィンドウ検出（最大5秒ポーリング）
            string exeName = "";
            try
            {
                exeName = System.IO.Path.GetFileNameWithoutExtension(item.Path).ToLower();
            }
            catch { }

            IntPtr foundHwnd = IntPtr.Zero;
            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(150);
                var currentWindows = new List<IntPtr>();
                EnumWindows((h, l) =>
                {
                    if (IsWindowVisible(h)) currentWindows.Add(h);
                    return true;
                }, IntPtr.Zero);

                // 1. 新しく出現したウィンドウ（かつ既に別のツールに割り当てられていないもの）
                foreach (var h in currentWindows)
                {
                    if (!beforeWindows.Contains(h) && !usedHwnds.Contains(h))
                    {
                        var sb = new StringBuilder(256);
                        GetWindowText(h, sb, 256);
                        if (sb.Length > 0)
                        {
                            foundHwnd = h;
                            break;
                        }
                    }
                }

                if (foundHwnd != IntPtr.Zero) break;

                // 2. 1秒経過後、未割当の既存ウィンドウから探す（ブラウザ以外、またはタブ起動時など）
                if (i > 8 && !string.IsNullOrEmpty(exeName))
                {
                    foreach (var h in currentWindows)
                    {
                        if (usedHwnds.Contains(h)) continue;

                        uint pid;
                        GetWindowThreadProcessId(h, out pid);
                        try
                        {
                            var proc = Process.GetProcessById((int)pid);
                            if (proc.ProcessName.ToLower().Contains(exeName))
                            {
                                foundHwnd = h;
                                break;
                            }
                        }
                        catch { }
                    }
                }
                if (foundHwnd != IntPtr.Zero) break;
            }

            if (foundHwnd != IntPtr.Zero)
            {
                usedHwnds.Add(foundHwnd);
                ApplyLayout(foundHwnd, item);
            }

            return foundHwnd;
        }
    }

    // ==========================================
    // メインウィンドウ (WPF)
    // ==========================================
    public class MainWindow : Window
    {
        private AppConfig config;
        private string configPath;

        private ComboBox cbProfiles;
        private StackPanel panelToolCards;
        private TextBlock txtStatus;
        private TextBlock txtMonitorInfo;
        private Button btnLaunchAll;

        private const int HOTKEY_ID_LAUNCH = 9001;
        private const int HOTKEY_ID_CLEANUP = 9002;
        private HwndSource hwndSource;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private bool isExiting = false;
        private CheckBox chkStartWithWindows;
        private CheckBox chkGlobalHotkey;
        private const string RUN_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "WorkspaceHub";

        public MainWindow()
        {
            configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace_config.json");
            LoadConfig();

            // レジストリ状態と同期
            config.StartWithWindows = CheckStartupRegistry();

            InitUI();
            InitNotifyIcon();
            RefreshProfilesCombo();
            RefreshToolCards();
        }

        private void LoadConfig()
        {
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath, Encoding.UTF8);
                    config = SimpleJson.DeserializeConfig(json);
                }
                catch
                {
                    config = AppConfig.CreateDefault();
                }
            }
            else
            {
                config = AppConfig.CreateDefault();
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                string json = SimpleJson.SerializeConfig(config);
                File.WriteAllText(configPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show("設定の保存に失敗しました: " + ex.Message);
            }
        }

        private Profile GetCurrentProfile()
        {
            if (config.Profiles.Count == 0)
            {
                config.Profiles.Add(new Profile { Name = "デフォルト" });
            }
            var p = config.Profiles.Find(x => x.Id == config.SelectedProfileId);
            if (p == null)
            {
                p = config.Profiles[0];
                config.SelectedProfileId = p.Id;
            }
            return p;
        }

        private void InitUI()
        {
            this.Title = "⚡ Workspace Hub - デスクトップ・ホームランチャー";
            this.Width = 980;
            this.Height = 720;
            this.MinWidth = 850;
            this.MinHeight = 600;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")); // Deep Slate

            // フォント設定
            FontFamily = new FontFamily("Yu Gothic UI, Segoe UI, Meiryo");

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Hero Button & Action Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 2: Tool List
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: Settings Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4: Footer / Status

            // --- 1. Header ---
            var headerBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 16, 24, 16)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titlePanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            var iconText = new TextBlock
            {
                Text = "⚡",
                FontSize = 26,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            var titleTextPanel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Workspace Hub",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"))
            };
            var subTitleLabel = new TextBlock
            {
                Text = "ワンクリックで好みのレイアウトに一括起動するホームアプリ",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"))
            };
            titleTextPanel.Children.Add(titleLabel);
            titleTextPanel.Children.Add(subTitleLabel);
            titlePanel.Children.Add(iconText);
            titlePanel.Children.Add(titleTextPanel);
            Grid.SetColumn(titlePanel, 0);

            // プロファイル選択パネル
            var profilePanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var lblProf = new TextBlock
            {
                Text = "プロファイル:",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13
            };

            cbProfiles = new ComboBox
            {
                Width = 190,
                Height = 34,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            cbProfiles.SelectionChanged += CbProfiles_SelectionChanged;

            var btnSnapshotProf = CreateStyledButton("📸 画面から新規作成", "#0EA5E9", 145, 34);
            btnSnapshotProf.ToolTip = "現在デスクトップで開いているウィンドウの配置をそのままプロファイルとして保存します";
            btnSnapshotProf.Click += BtnSnapshotProfile_Click;
            btnSnapshotProf.Margin = new Thickness(0, 0, 6, 0);

            var btnNewProf = CreateStyledButton("＋ 新規", "#3B82F6", 65, 34);
            btnNewProf.Click += BtnNewProf_Click;
            btnNewProf.Margin = new Thickness(0, 0, 6, 0);

            var btnDelProf = CreateStyledButton("🗑️ 削除", "#EF4444", 65, 34);
            btnDelProf.Click += BtnDelProf_Click;

            profilePanel.Children.Add(lblProf);
            profilePanel.Children.Add(cbProfiles);
            profilePanel.Children.Add(btnSnapshotProf);
            profilePanel.Children.Add(btnNewProf);
            profilePanel.Children.Add(btnDelProf);
            Grid.SetColumn(profilePanel, 1);

            headerGrid.Children.Add(titlePanel);
            headerGrid.Children.Add(profilePanel);
            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // --- 2. Hero Section & Actions ---
            var heroBorder = new Border
            {
                Padding = new Thickness(24, 18, 24, 14),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#131D31"))
            };

            var heroPanel = new StackPanel();

            // 一括起動ヒーローボタン
            btnLaunchAll = new Button
            {
                Height = 58,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 14)
            };

            // グラデーション背景
            var launchBrush = new LinearGradientBrush();
            launchBrush.StartPoint = new Point(0, 0);
            launchBrush.EndPoint = new Point(1, 0);
            launchBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#6366F1"), 0.0));
            launchBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#8B5CF6"), 1.0));
            btnLaunchAll.Background = launchBrush;

            var btnTemplate = CreateButtonTemplate();
            btnLaunchAll.Template = btnTemplate;

            var heroContent = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var heroIcon = new TextBlock { Text = "⚡", FontSize = 24, Margin = new Thickness(0, 0, 10, 0), Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            var heroText = new TextBlock { Text = "すべてのツールを任意の形で一括起動", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            heroContent.Children.Add(heroIcon);
            heroContent.Children.Add(heroText);
            btnLaunchAll.Content = heroContent;
            btnLaunchAll.Click += BtnLaunchAll_Click;
            heroPanel.Children.Add(btnLaunchAll);

            // ツールバー（ツール追加、デスクトップショートカット作成、再配置）
            var toolbarGrid = new Grid();
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftActions = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            var btnAddTool = CreateStyledButton("＋ ツールを追加", "#10B981", 135, 34);
            btnAddTool.Click += BtnAddTool_Click;
            btnAddTool.Margin = new Thickness(0, 0, 10, 0);

            var btnArrangeOnly = CreateStyledButton("📐 起動中ツールを再配置", "#64748B", 175, 34);
            btnArrangeOnly.Click += BtnArrangeOnly_Click;
            btnArrangeOnly.Margin = new Thickness(0, 0, 10, 0);

            var btnToggleMinimize = CreateStyledButton("🧹 最小化/復元", "#475569", 125, 34);
            btnToggleMinimize.ToolTip = "登録ツールのウィンドウを一括最小化、または元の位置に復元します (Ctrl+Alt+H)";
            btnToggleMinimize.Click += (s, e) => ToggleMinimizeRestore();
            btnToggleMinimize.Margin = new Thickness(0, 0, 10, 0);

            var btnCloseAll = CreateStyledButton("❌ 一括終了", "#991B1B", 100, 34);
            btnCloseAll.ToolTip = "登録ツールのウィンドウを一括終了します";
            btnCloseAll.Click += (s, e) => CloseAllTools();
            btnCloseAll.Margin = new Thickness(0, 0, 10, 0);

            leftActions.Children.Add(btnAddTool);
            leftActions.Children.Add(btnArrangeOnly);
            leftActions.Children.Add(btnToggleMinimize);
            leftActions.Children.Add(btnCloseAll);
            Grid.SetColumn(leftActions, 0);

            var btnDesktopShortcut = CreateStyledButton("📌 デスクトップにショートカット作成", "#8B5CF6", 240, 34);
            btnDesktopShortcut.Click += BtnDesktopShortcut_Click;
            Grid.SetColumn(btnDesktopShortcut, 2);

            toolbarGrid.Children.Add(leftActions);
            toolbarGrid.Children.Add(btnDesktopShortcut);
            heroPanel.Children.Add(toolbarGrid);

            heroBorder.Child = heroPanel;
            Grid.SetRow(heroBorder, 1);
            rootGrid.Children.Add(heroBorder);

            // --- 3. Tool List (Cards) ---
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 16, 24, 16)
            };
            panelToolCards = new StackPanel();
            scrollViewer.Content = panelToolCards;
            Grid.SetRow(scrollViewer, 2);
            rootGrid.Children.Add(scrollViewer);

            // --- 4. Settings Bar ---
            var settingsBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 7, 24, 7)
            };
            var settingsPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

            chkGlobalHotkey = new CheckBox
            {
                Content = "⌨️ ホットキー有効 (Ctrl+Alt+W: 一括起動/整列, Ctrl+Alt+H: 一括最小化/復元)",
                IsChecked = config.EnableGlobalHotkey,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 24, 0),
                FontSize = 12
            };
            chkGlobalHotkey.Checked += (s, e) => { config.EnableGlobalHotkey = true; SaveConfig(); UpdateHotkeys(); };
            chkGlobalHotkey.Unchecked += (s, e) => { config.EnableGlobalHotkey = false; SaveConfig(); UpdateHotkeys(); };

            chkStartWithWindows = new CheckBox
            {
                Content = "🚀 PC起動時に常駐 (タスクトレイ)",
                IsChecked = config.StartWithWindows,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 24, 0),
                FontSize = 12
            };
            chkStartWithWindows.Checked += (s, e) => { config.StartWithWindows = true; SetStartupRegistry(true); SaveConfig(); };
            chkStartWithWindows.Unchecked += (s, e) => { config.StartWithWindows = false; SetStartupRegistry(false); SaveConfig(); };

            settingsPanel.Children.Add(chkGlobalHotkey);
            settingsPanel.Children.Add(chkStartWithWindows);
            settingsBorder.Child = settingsPanel;
            Grid.SetRow(settingsBorder, 3);
            rootGrid.Children.Add(settingsBorder);

            // --- 5. Footer & Monitor Info ---
            var footerBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B0F19")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 10, 20, 10)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtStatus = new TextBlock
            {
                Text = "準備完了",
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtStatus, 0);

            // モニター情報
            var sbMon = new StringBuilder("🖥️ 接続モニター: ");
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                var s = Screen.AllScreens[i];
                sbMon.Append(string.Format("[{0}: {1}x{2}{3}] ", i + 1, s.Bounds.Width, s.Bounds.Height, s.Primary ? "(主)" : ""));
            }
            txtMonitorInfo = new TextBlock
            {
                Text = sbMon.ToString().TrimEnd(),
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtMonitorInfo, 1);

            footerGrid.Children.Add(txtStatus);
            footerGrid.Children.Add(txtMonitorInfo);
            footerBorder.Child = footerGrid;
            Grid.SetRow(footerBorder, 4);
            rootGrid.Children.Add(footerBorder);

            this.Content = rootGrid;
        }

        private ControlTemplate CreateButtonTemplate()
        {
            string xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='Button'>
    <Border Name='border' Background='{TemplateBinding Background}' CornerRadius='8' Padding='{TemplateBinding Padding}'>
        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='border' Property='Opacity' Value='0.88'/>
        </Trigger>
        <Trigger Property='IsPressed' Value='True'>
            <Setter TargetName='border' Property='Opacity' Value='0.75'/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        private Button CreateStyledButton(string text, string hexColor, double width, double height)
        {
            var btn = new Button
            {
                Content = text,
                Width = width,
                Height = height,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)),
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = CreateButtonTemplate()
            };
            return btn;
        }

        private void RefreshProfilesCombo()
        {
            cbProfiles.SelectionChanged -= CbProfiles_SelectionChanged;
            cbProfiles.Items.Clear();
            int selectedIdx = 0;
            for (int i = 0; i < config.Profiles.Count; i++)
            {
                cbProfiles.Items.Add(config.Profiles[i].Name);
                if (config.Profiles[i].Id == config.SelectedProfileId)
                {
                    selectedIdx = i;
                }
            }
            if (cbProfiles.Items.Count > 0)
            {
                cbProfiles.SelectedIndex = selectedIdx;
            }
            cbProfiles.SelectionChanged += CbProfiles_SelectionChanged;
        }

        private void CbProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbProfiles.SelectedIndex >= 0 && cbProfiles.SelectedIndex < config.Profiles.Count)
            {
                config.SelectedProfileId = config.Profiles[cbProfiles.SelectedIndex].Id;
                SaveConfig();
                RefreshToolCards();
            }
        }

        private void BtnNewProf_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("新規プロファイル作成", "プロファイル名を入力してください:", "プロファイル " + (config.Profiles.Count + 1));
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.InputText))
            {
                var newProf = new Profile { Name = dialog.InputText.Trim() };
                config.Profiles.Add(newProf);
                config.SelectedProfileId = newProf.Id;
                SaveConfig();
                RefreshProfilesCombo();
                RefreshToolCards();
            }
        }

        private void BtnDelProf_Click(object sender, RoutedEventArgs e)
        {
            if (config.Profiles.Count <= 1)
            {
                MessageBox.Show("最後のプロファイルは削除できません。", "通知", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var current = GetCurrentProfile();
            if (MessageBox.Show("プロファイル「" + current.Name + "」を削除してもよろしいですか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                config.Profiles.Remove(current);
                config.SelectedProfileId = config.Profiles[0].Id;
                SaveConfig();
                RefreshProfilesCombo();
                RefreshToolCards();
            }
        }

        private void RefreshToolCards()
        {
            panelToolCards.Children.Clear();
            var profile = GetCurrentProfile();

            if (profile.Items.Count == 0)
            {
                var emptyBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(30),
                    Margin = new Thickness(0, 10, 0, 0)
                };
                var emptyPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                emptyPanel.Children.Add(new TextBlock
                {
                    Text = "📂 ツールがまだ登録されていません",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6)
                });
                emptyPanel.Children.Add(new TextBlock
                {
                    Text = "上の「＋ ツールを追加」ボタンから、よく使うアプリやWebサイトを登録してください。",
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyBorder.Child = emptyPanel;
                panelToolCards.Children.Add(emptyBorder);
                return;
            }

            for (int i = 0; i < profile.Items.Count; i++)
            {
                var item = profile.Items[i];
                var card = CreateToolCard(item, i);
                panelToolCards.Children.Add(card);
            }
        }

        private Border CreateToolCard(ToolItem item, int index)
        {
            var cardBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.Enabled ? "#334155" : "#1F2937")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10),
                Opacity = item.Enabled ? 1.0 : 0.6
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Checkbox
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Tool info
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Badges & Actions

            // 1. 有効/無効 チェックボックス
            var chk = new System.Windows.Controls.CheckBox
            {
                IsChecked = item.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            chk.Checked += (s, e) => { item.Enabled = true; SaveConfig(); cardBorder.Opacity = 1.0; };
            chk.Unchecked += (s, e) => { item.Enabled = false; SaveConfig(); cardBorder.Opacity = 0.6; };
            Grid.SetColumn(chk, 0);

            // 2. ツール情報
            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var namePanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

            var txtName = new TextBlock
            {
                Text = item.Name,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                Margin = new Thickness(0, 0, 10, 0)
            };
            namePanel.Children.Add(txtName);

            // モニター＆レイアウトバッジ
            string monLabel = item.MonitorIndex < 0 ? "主画面" : "画面 " + (item.MonitorIndex + 1);
            string layoutLabel = GetLayoutLabel(item.LayoutType);
            var badge = CreateBadge(monLabel + " | " + layoutLabel, GetLayoutColor(item.LayoutType));
            namePanel.Children.Add(badge);

            // ブラウザバッジ
            if (WindowEngine.IsBrowserTarget(item.Path))
            {
                string bMode = string.IsNullOrEmpty(item.BrowserMode) ? "NewWindow" : item.BrowserMode;
                string bLabel = bMode == "App" ? "🌐 アプリ枠" : (bMode == "Tab" ? "🌐 タブ" : "🌐 独立ウィンドウ");
                var bBadge = CreateBadge(bLabel, "#0284C7");
                bBadge.Margin = new Thickness(6, 0, 0, 0);
                namePanel.Children.Add(bBadge);
            }

            infoPanel.Children.Add(namePanel);

            var txtPath = new TextBlock
            {
                Text = item.Path + (string.IsNullOrEmpty(item.Arguments) ? "" : " " + item.Arguments),
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 450,
                Margin = new Thickness(0, 4, 0, 0)
            };
            infoPanel.Children.Add(txtPath);
            Grid.SetColumn(infoPanel, 1);

            // 3. アクションボタン
            var actionPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var btnLaunch = CreateStyledButton("▶ 起動", "#3B82F6", 60, 30);
            btnLaunch.Margin = new Thickness(0, 0, 6, 0);
            btnLaunch.Click += (s, e) =>
            {
                ThreadPool.QueueUserWorkItem(state =>
                {
                    Dispatcher.Invoke(new Action(() => { txtStatus.Text = item.Name + " を起動中..."; }));
                    var hwnd = WindowEngine.LaunchAndFindWindow(item);
                    if (hwnd != IntPtr.Zero)
                    {
                        WindowEngine.ApplyLayout(hwnd, item);
                        Dispatcher.Invoke(new Action(() => { txtStatus.Text = item.Name + " を配置しました！"; }));
                    }
                    else
                    {
                        Dispatcher.Invoke(new Action(() => { txtStatus.Text = item.Name + " の起動完了（ウィンドウ直接配置スキップ）"; }));
                    }
                });
            };

            var btnApply = CreateStyledButton("📐 配置", "#6366F1", 60, 30);
            btnApply.Margin = new Thickness(0, 0, 6, 0);
            btnApply.Click += (s, e) =>
            {
                var windows = WindowEngine.GetOpenWindows();
                string exeName = "";
                try { exeName = System.IO.Path.GetFileNameWithoutExtension(item.Path).ToLower(); } catch { }
                IntPtr targetHwnd = IntPtr.Zero;
                foreach (var w in windows)
                {
                    if (!string.IsNullOrEmpty(exeName) && w.ProcessName.ToLower().Contains(exeName))
                    {
                        targetHwnd = w.Handle;
                        break;
                    }
                }
                if (targetHwnd != IntPtr.Zero)
                {
                    WindowEngine.ApplyLayout(targetHwnd, item);
                    txtStatus.Text = item.Name + " のウィンドウを再配置しました！";
                }
                else
                {
                    MessageBox.Show("「" + item.Name + "」の実行中ウィンドウが見つかりませんでした。先に「起動」を押してください。", "通知", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            var btnEdit = CreateStyledButton("✏️ 編集", "#64748B", 60, 30);
            btnEdit.Margin = new Thickness(0, 0, 6, 0);
            btnEdit.Click += (s, e) =>
            {
                var dlg = new ToolEditDialog(item);
                dlg.Owner = this;
                if (dlg.ShowDialog() == true)
                {
                    SaveConfig();
                    RefreshToolCards();
                }
            };

            var btnDelete = CreateStyledButton("🗑️", "#EF4444", 36, 30);
            btnDelete.Click += (s, e) =>
            {
                if (MessageBox.Show("「" + item.Name + "」を削除しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    GetCurrentProfile().Items.Remove(item);
                    SaveConfig();
                    RefreshToolCards();
                }
            };

            actionPanel.Children.Add(btnLaunch);
            actionPanel.Children.Add(btnApply);
            actionPanel.Children.Add(btnEdit);
            actionPanel.Children.Add(btnDelete);
            Grid.SetColumn(actionPanel, 2);

            grid.Children.Add(chk);
            grid.Children.Add(infoPanel);
            grid.Children.Add(actionPanel);
            cardBorder.Child = grid;

            return cardBorder;
        }

        private Border CreateBadge(string text, string hexColor)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            var txt = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            border.Child = txt;
            return border;
        }

        private string GetLayoutLabel(string layoutType)
        {
            switch (layoutType)
            {
                case "Maximize": return "🖥️ 全画面";
                case "LeftHalf": return "◀️ 左半分";
                case "RightHalf": return "▶️ 右半分";
                case "TopHalf": return "⬆️ 上半分";
                case "BottomHalf": return "⬇️ 下半分";
                case "TopLeft": return "◰ 左上 1/4";
                case "TopRight": return "◳ 右上 1/4";
                case "BottomLeft": return "◱ 左下 1/4";
                case "BottomRight": return "◲ 右下 1/4";
                case "Center": return "🎯 中央";
                case "Custom": return "📐 カスタム";
                default: return "そのまま";
            }
        }

        private string GetLayoutColor(string layoutType)
        {
            switch (layoutType)
            {
                case "Maximize": return "#10B981"; // Emerald
                case "LeftHalf": return "#0EA5E9"; // Sky
                case "RightHalf": return "#6366F1"; // Indigo
                case "TopHalf":
                case "BottomHalf": return "#8B5CF6"; // Purple
                case "TopLeft":
                case "TopRight":
                case "BottomLeft":
                case "BottomRight": return "#EC4899"; // Pink
                case "Center": return "#F59E0B"; // Amber
                case "Custom": return "#F97316"; // Orange
                default: return "#64748B";
            }
        }

        private void BtnAddTool_Click(object sender, RoutedEventArgs e)
        {
            var newItem = new ToolItem();
            var dlg = new ToolEditDialog(newItem);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                GetCurrentProfile().Items.Add(newItem);
                SaveConfig();
                RefreshToolCards();
            }
        }

        private void BtnLaunchAll_Click(object sender, RoutedEventArgs e)
        {
            ExecuteLaunchAll();
        }

        public void ExecuteLaunchAll()
        {
            var profile = GetCurrentProfile();
            var itemsToLaunch = profile.Items.FindAll(x => x.Enabled);

            if (itemsToLaunch.Count == 0)
            {
                MessageBox.Show("起動対象のツールが登録されていないか、すべて無効になっています。", "通知", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            btnLaunchAll.IsEnabled = false;
            txtStatus.Text = "一括起動を開始します... (対象: " + itemsToLaunch.Count + " 件)";

            ThreadPool.QueueUserWorkItem(state =>
            {
                int count = 0;
                var usedHwnds = new HashSet<IntPtr>();

                foreach (var item in itemsToLaunch)
                {
                    count++;
                    Dispatcher.Invoke(new Action(() =>
                    {
                        txtStatus.Text = string.Format("起動中 ({0}/{1}): {2}...", count, itemsToLaunch.Count, item.Name);
                    }));

                    var hwnd = WindowEngine.LaunchAndFindWindow(item, usedHwnds);

                    // ブラウザの場合、ウィンドウが確実に生成されるよう最低400ms待機
                    int waitMs = item.DelayMs;
                    if (WindowEngine.IsBrowserTarget(item.Path) && waitMs < 400)
                    {
                        waitMs = 400;
                    }

                    if (waitMs > 0)
                    {
                        Thread.Sleep(waitMs);
                    }
                }

                Dispatcher.Invoke(new Action(() =>
                {
                    txtStatus.Text = "✨ すべてのツールの一括起動＆配置が完了しました！ (" + itemsToLaunch.Count + " 件)";
                    btnLaunchAll.IsEnabled = true;
                }));
            });
        }

        private void BtnArrangeOnly_Click(object sender, RoutedEventArgs e)
        {
            var profile = GetCurrentProfile();
            var items = profile.Items.FindAll(x => x.Enabled);
            var windows = WindowEngine.GetOpenWindows();

            int matched = 0;
            foreach (var item in items)
            {
                string exeName = "";
                try { exeName = System.IO.Path.GetFileNameWithoutExtension(item.Path).ToLower(); } catch { }
                foreach (var w in windows)
                {
                    if (!string.IsNullOrEmpty(exeName) && w.ProcessName.ToLower().Contains(exeName))
                    {
                        WindowEngine.ApplyLayout(w.Handle, item);
                        matched++;
                        break;
                    }
                }
            }

            txtStatus.Text = "起動中ツールの再配置が完了しました (" + matched + " 件整列)";
        }

        private void BtnDesktopShortcut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                string icoPath = System.IO.Path.Combine(dir, "workspace.ico");

                // 1. Windows標準の .lnk ショートカット（稲妻アイコン付き）を作成
                try
                {
                    string lnkPath = System.IO.Path.Combine(desktopPath, "⚡ ワークスペース一括起動.lnk");
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        object shell = Activator.CreateInstance(shellType);
                        object shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                        Type scType = shortcut.GetType();
                        scType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                        scType.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "--launch-all" });
                        scType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { dir });
                        if (File.Exists(icoPath))
                        {
                            scType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { icoPath + ",0" });
                        }
                        scType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "ワークスペースのツールを一括起動します" });
                        scType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
                    }
                }
                catch { }

                // 2. 互換性のための silent 実行用 VBScript も生成
                string vbsPath = System.IO.Path.Combine(desktopPath, "⚡ ワークスペース一括起動.vbs");
                string vbsContent = string.Format(
                    "Set WshShell = CreateObject(\"WScript.Shell\")\r\n" +
                    "WshShell.Run \"\"\"{0}\"\" --launch-all\", 0, False\r\n",
                    exePath);
                File.WriteAllText(vbsPath, vbsContent, Encoding.Default);

                MessageBox.Show(
                    "デスクトップにワンクリック起動用のショートカットを作成しました！\r\n\r\n" +
                    "・⚡ ワークスペース一括起動.lnk （稲妻アイコン）\r\n" +
                    "・⚡ ワークスペース一括起動.vbs\r\n\r\n" +
                    "ダブルクリックするだけで、このホームアプリを開かずに全ツールが一括配置されます。",
                    "作成完了",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("ショートカット作成に失敗しました: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // 項目1: グローバルホットキー & タスクトレイ & スタートアップ
        // ==========================================
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(HwndHook);
                UpdateHotkeys();
            }
        }

        private void UpdateHotkeys()
        {
            if (hwndSource == null) return;
            IntPtr handle = hwndSource.Handle;
            WindowEngine.UnregisterHotKey(handle, HOTKEY_ID_LAUNCH);
            WindowEngine.UnregisterHotKey(handle, HOTKEY_ID_CLEANUP);

            if (config.EnableGlobalHotkey)
            {
                // Ctrl + Alt + W : 一括起動＆整列
                WindowEngine.RegisterHotKey(handle, HOTKEY_ID_LAUNCH, WindowEngine.MOD_CONTROL | WindowEngine.MOD_ALT | WindowEngine.MOD_NOREPEAT, (uint)Keys.W);
                // Ctrl + Alt + H : 一括最小化/復元
                WindowEngine.RegisterHotKey(handle, HOTKEY_ID_CLEANUP, WindowEngine.MOD_CONTROL | WindowEngine.MOD_ALT | WindowEngine.MOD_NOREPEAT, (uint)Keys.H);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WindowEngine.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_LAUNCH)
                {
                    handled = true;
                    ExecuteLaunchAll();
                }
                else if (id == HOTKEY_ID_CLEANUP)
                {
                    handled = true;
                    ToggleMinimizeRestore();
                }
            }
            return IntPtr.Zero;
        }

        private bool CheckStartupRegistry()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, false))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(APP_NAME) as string;
                        return !string.IsNullOrEmpty(val);
                    }
                }
            }
            catch { }
            return false;
        }

        private void SetStartupRegistry(bool enable)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Process.GetCurrentProcess().MainModule.FileName;
                            key.SetValue(APP_NAME, "\"" + exePath + "\" --tray");
                        }
                        else
                        {
                            key.DeleteValue(APP_NAME, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("スタートアップ設定の変更に失敗しました: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InitNotifyIcon()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string icoPath = System.IO.Path.Combine(dir, "workspace.ico");
            if (File.Exists(icoPath))
            {
                try { notifyIcon.Icon = new System.Drawing.Icon(icoPath); }
                catch { notifyIcon.Icon = System.Drawing.SystemIcons.Application; }
            }
            else
            {
                notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            notifyIcon.Text = "⚡ Workspace Hub";
            notifyIcon.Visible = true;

            notifyIcon.DoubleClick += (s, e) =>
            {
                ShowAndActivate();
            };

            BuildNotifyContextMenu();
        }

        private void BuildNotifyContextMenu()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();

            var profile = GetCurrentProfile();
            var itemLaunch = new System.Windows.Forms.ToolStripMenuItem("⚡ 一括起動・整列 (" + (profile != null ? profile.Name : "") + ")");
            itemLaunch.Font = new System.Drawing.Font(itemLaunch.Font, System.Drawing.FontStyle.Bold);
            itemLaunch.Click += (s, e) => ExecuteLaunchAll();
            menu.Items.Add(itemLaunch);

            // プロファイル切り替えサブメニュー
            var profMenu = new System.Windows.Forms.ToolStripMenuItem("📁 プロファイル選択");
            foreach (var p in config.Profiles)
            {
                var pItem = new System.Windows.Forms.ToolStripMenuItem(p.Name);
                pItem.Checked = (p.Id == config.SelectedProfileId);
                string pid = p.Id;
                pItem.Click += (s, e) =>
                {
                    config.SelectedProfileId = pid;
                    SaveConfig();
                    RefreshProfilesCombo();
                    RefreshToolCards();
                    BuildNotifyContextMenu();
                };
                profMenu.DropDownItems.Add(pItem);
            }
            menu.Items.Add(profMenu);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var itemMin = new System.Windows.Forms.ToolStripMenuItem("🧹 一括最小化 / 復元 (Ctrl+Alt+H)");
            itemMin.Click += (s, e) => ToggleMinimizeRestore();
            menu.Items.Add(itemMin);

            var itemClose = new System.Windows.Forms.ToolStripMenuItem("❌ ツールを一括終了");
            itemClose.Click += (s, e) => CloseAllTools();
            menu.Items.Add(itemClose);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var itemShow = new System.Windows.Forms.ToolStripMenuItem("🖥️ ホーム画面を表示");
            itemShow.Click += (s, e) => ShowAndActivate();
            menu.Items.Add(itemShow);

            var itemExit = new System.Windows.Forms.ToolStripMenuItem("🚪 アプリを完全終了");
            itemExit.Click += (s, e) =>
            {
                isExiting = true;
                this.Close();
            };
            menu.Items.Add(itemExit);

            notifyIcon.ContextMenuStrip = menu;
        }

        public void ShowAndActivate()
        {
            this.Show();
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }
            this.Activate();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!isExiting && config.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                this.Hide();
                try
                {
                    notifyIcon.ShowBalloonTip(2000, "⚡ Workspace Hub", "バックグラウンドで動作中です。タスクトレイからいつでも開けます。", System.Windows.Forms.ToolTipIcon.Info);
                }
                catch { }
                return;
            }

            if (hwndSource != null)
            {
                WindowEngine.UnregisterHotKey(hwndSource.Handle, HOTKEY_ID_LAUNCH);
                WindowEngine.UnregisterHotKey(hwndSource.Handle, HOTKEY_ID_CLEANUP);
            }
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            base.OnClosing(e);
            try { System.Windows.Application.Current.Shutdown(); } catch { }
        }

        // ==========================================
        // 項目2: 片付け・切り替えをスマートにする機能
        // ==========================================
        private void ToggleMinimizeRestore()
        {
            var profile = GetCurrentProfile();
            var items = profile.Items.FindAll(x => x.Enabled);
            if (items.Count == 0) return;

            var windows = WindowEngine.GetOpenWindows();
            var matchedWindows = new List<Tuple<WindowEngine.OpenWindowInfo, ToolItem>>();

            foreach (var item in items)
            {
                string exeName = "";
                try { exeName = System.IO.Path.GetFileNameWithoutExtension(item.Path).ToLowerInvariant(); } catch { }
                if (string.IsNullOrEmpty(exeName)) continue;

                foreach (var w in windows)
                {
                    if (w.ProcessName.ToLowerInvariant().Contains(exeName))
                    {
                        matchedWindows.Add(new Tuple<WindowEngine.OpenWindowInfo, ToolItem>(w, item));
                        break;
                    }
                }
            }

            if (matchedWindows.Count == 0)
            {
                txtStatus.Text = "最小化/復元対象の実行中ツールが見つかりませんでした。";
                return;
            }

            // 1つでも通常状態のウィンドウがあれば一括最小化、すべて最小化されていれば復元
            bool anyVisible = false;
            foreach (var m in matchedWindows)
            {
                if (!WindowEngine.IsIconic(m.Item1.Handle))
                {
                    anyVisible = true;
                    break;
                }
            }

            if (anyVisible)
            {
                foreach (var m in matchedWindows)
                {
                    WindowEngine.ShowWindow(m.Item1.Handle, WindowEngine.SW_MINIMIZE);
                }
                txtStatus.Text = "🧹 ツールを一括最小化しました (" + matchedWindows.Count + " 件)";
            }
            else
            {
                foreach (var m in matchedWindows)
                {
                    WindowEngine.ApplyLayout(m.Item1.Handle, m.Item2);
                }
                txtStatus.Text = "✨ ツールを一括復元・整列しました (" + matchedWindows.Count + " 件)";
            }
        }

        private void CloseAllTools()
        {
            var profile = GetCurrentProfile();
            var items = profile.Items.FindAll(x => x.Enabled);
            if (items.Count == 0) return;

            var res = MessageBox.Show(
                "「" + profile.Name + "」に登録されている起動中のツールを一括終了しますか？\r\n（保存していないデータがある場合は各ツールの確認ダイアログが表示されます）",
                "一括終了の確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            var windows = WindowEngine.GetOpenWindows();
            int closedCount = 0;

            foreach (var item in items)
            {
                string exeName = "";
                try { exeName = System.IO.Path.GetFileNameWithoutExtension(item.Path).ToLowerInvariant(); } catch { }
                if (string.IsNullOrEmpty(exeName)) continue;

                foreach (var w in windows)
                {
                    if (w.ProcessName.ToLowerInvariant().Contains(exeName))
                    {
                        WindowEngine.PostMessage(w.Handle, WindowEngine.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        closedCount++;
                        break;
                    }
                }
            }

            txtStatus.Text = "❌ ツールに終了リクエストを送信しました (" + closedCount + " 件)";
        }

        // ==========================================
        // 項目3: 現在の画面から丸ごとプロファイル保存
        // ==========================================
        private void BtnSnapshotProfile_Click(object sender, RoutedEventArgs e)
        {
            var modal = new SnapshotModal();
            modal.Owner = this;
            if (modal.ShowDialog() == true && modal.CreatedProfile != null)
            {
                config.Profiles.Add(modal.CreatedProfile);
                config.SelectedProfileId = modal.CreatedProfile.Id;
                SaveConfig();
                RefreshProfilesCombo();
                RefreshToolCards();
                BuildNotifyContextMenu();
                txtStatus.Text = "📸 「" + modal.CreatedProfile.Name + "」を作成しました！(登録ツール: " + modal.CreatedProfile.Items.Count + " 件)";
            }
        }
    }

    // ==========================================
    // ツール編集・登録モーダルダイアログ
    // ==========================================
    public class ToolEditDialog : Window
    {
        private ToolItem targetItem;

        private TextBox txtName;
        private TextBox txtPath;
        private TextBox txtArgs;
        private ComboBox cbMonitor;
        private ComboBox cbLayout;
        private ComboBox cbBrowserMode;
        private TextBox txtDelay;
        private TextBox txtCustomX;
        private TextBox txtCustomY;
        private TextBox txtCustomW;
        private TextBox txtCustomH;
        private StackPanel panelCustomCoords;

        public ToolEditDialog(ToolItem item)
        {
            this.targetItem = item;

            this.Title = "ツールの設定・レイアウト登録";
            this.Width = 620;
            this.Height = 720;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            this.ResizeMode = ResizeMode.NoResize;
            FontFamily = new FontFamily("Yu Gothic UI, Segoe UI, Meiryo");

            var mainPanel = new StackPanel { Margin = new Thickness(24) };

            // 1. 名前
            mainPanel.Children.Add(CreateFieldLabel("ツール名:"));
            txtName = CreateTextBox(targetItem.Name);
            mainPanel.Children.Add(txtName);

            // 2. パス / URL
            mainPanel.Children.Add(CreateFieldLabel("起動ファイル・URL・コマンド:"));
            var pathGrid = new Grid();
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            txtPath = CreateTextBox(targetItem.Path);
            Grid.SetColumn(txtPath, 0);

            var btnBrowse = new Button
            {
                Content = "参照...",
                Width = 70,
                Height = 32,
                Margin = new Thickness(8, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
                Foreground = Brushes.White,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnBrowse.Click += (s, e) =>
            {
                var ofd = new OpenFileDialog();
                ofd.Filter = "実行可能ファイル・ショートカット (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|すべてのファイル (*.*)|*.*";
                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtPath.Text = ofd.FileName;
                    if (string.IsNullOrEmpty(txtName.Text) || txtName.Text == "新規ツール")
                    {
                        txtName.Text = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
                    }
                }
            };
            Grid.SetColumn(btnBrowse, 1);
            pathGrid.Children.Add(txtPath);
            pathGrid.Children.Add(btnBrowse);
            mainPanel.Children.Add(pathGrid);

            // 3. 引数 (オプション)
            mainPanel.Children.Add(CreateFieldLabel("引数 (オプション):"));
            txtArgs = CreateTextBox(targetItem.Arguments);
            mainPanel.Children.Add(txtArgs);

            // 4. モニター選択
            mainPanel.Children.Add(CreateFieldLabel("配置先モニター:"));
            cbMonitor = new ComboBox { Height = 32, FontSize = 13, Margin = new Thickness(0, 0, 0, 10) };
            cbMonitor.Items.Add("🖥️ メインモニター (Primary)");
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                var s = Screen.AllScreens[i];
                cbMonitor.Items.Add(string.Format("モニター {0}: {1}x{2}{3}", i + 1, s.Bounds.Width, s.Bounds.Height, s.Primary ? " (主)" : ""));
            }
            if (targetItem.MonitorIndex < 0) cbMonitor.SelectedIndex = 0;
            else if (targetItem.MonitorIndex + 1 < cbMonitor.Items.Count) cbMonitor.SelectedIndex = targetItem.MonitorIndex + 1;
            else cbMonitor.SelectedIndex = 0;
            mainPanel.Children.Add(cbMonitor);

            // 5. レイアウト・プリセット
            mainPanel.Children.Add(CreateFieldLabel("配置レイアウト (任意の形で立ち上げる):"));
            cbLayout = new ComboBox { Height = 32, FontSize = 13, Margin = new Thickness(0, 0, 0, 8) };
            cbLayout.Items.Add(new ComboBoxItem { Content = "🖥️ 全画面 (Maximize)", Tag = "Maximize" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "◀️ 画面 左半分 (LeftHalf 50%)", Tag = "LeftHalf" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "▶️ 画面 右半分 (RightHalf 50%)", Tag = "RightHalf" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "⬆️ 画面 上半分 (TopHalf 50%)", Tag = "TopHalf" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "⬇️ 画面 下半分 (BottomHalf 50%)", Tag = "BottomHalf" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "◰ 左上 1/4 (TopLeft 25%)", Tag = "TopLeft" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "◳ 右上 1/4 (TopRight 25%)", Tag = "TopRight" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "◱ 左下 1/4 (BottomLeft 25%)", Tag = "BottomLeft" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "◲ 右下 1/4 (BottomRight 25%)", Tag = "BottomRight" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "🎯 画面 中央 (Center 70%)", Tag = "Center" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "📐 カスタム座標・サイズ (Custom)", Tag = "Custom" });
            cbLayout.Items.Add(new ComboBoxItem { Content = "そのまま (配置変更なし)", Tag = "None" });

            // 選択
            for (int i = 0; i < cbLayout.Items.Count; i++)
            {
                var cbi = (ComboBoxItem)cbLayout.Items[i];
                if ((string)cbi.Tag == targetItem.LayoutType)
                {
                    cbLayout.SelectedIndex = i;
                    break;
                }
            }
            if (cbLayout.SelectedIndex < 0) cbLayout.SelectedIndex = 1;
            mainPanel.Children.Add(cbLayout);

            // 6. カスタム座標パネル & キャプチャボタン
            var captureGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            captureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            captureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnCapture = new Button
            {
                Content = "✨ 開いているウィンドウから位置を自動取得...",
                Height = 32,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(12, 0, 12, 0)
            };
            btnCapture.Click += BtnCapture_Click;
            Grid.SetColumn(btnCapture, 1);
            captureGrid.Children.Add(btnCapture);
            mainPanel.Children.Add(captureGrid);

            // カスタム座標入力欄
            panelCustomCoords = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            panelCustomCoords.Children.Add(new TextBlock { Text = "X:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            txtCustomX = new TextBox { Width = 55, Height = 28, Text = targetItem.CustomX.ToString(), Margin = new Thickness(0, 0, 10, 0) };
            panelCustomCoords.Children.Add(txtCustomX);

            panelCustomCoords.Children.Add(new TextBlock { Text = "Y:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            txtCustomY = new TextBox { Width = 55, Height = 28, Text = targetItem.CustomY.ToString(), Margin = new Thickness(0, 0, 10, 0) };
            panelCustomCoords.Children.Add(txtCustomY);

            panelCustomCoords.Children.Add(new TextBlock { Text = "幅:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            txtCustomW = new TextBox { Width = 65, Height = 28, Text = targetItem.CustomWidth.ToString(), Margin = new Thickness(0, 0, 10, 0) };
            panelCustomCoords.Children.Add(txtCustomW);

            panelCustomCoords.Children.Add(new TextBlock { Text = "高さ:", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            txtCustomH = new TextBox { Width = 65, Height = 28, Text = targetItem.CustomHeight.ToString() };
            panelCustomCoords.Children.Add(txtCustomH);

            mainPanel.Children.Add(panelCustomCoords);

            // 7. ブラウザ表示モード (Chrome / Edge等)
            mainPanel.Children.Add(CreateFieldLabel("🌐 ブラウザ表示モード (複数開いて分割する設定):"));
            cbBrowserMode = new ComboBox { Height = 32, FontSize = 13, Margin = new Thickness(0, 0, 0, 4) };
            cbBrowserMode.Items.Add(new ComboBoxItem { Content = "🪟 独立した別ウィンドウで開く (--new-window) [推奨・分割配置用]", Tag = "NewWindow" });
            cbBrowserMode.Items.Add(new ComboBoxItem { Content = "📱 専用アプリ枠で開く (--app) [タブ・URLバーなし]", Tag = "App" });
            cbBrowserMode.Items.Add(new ComboBoxItem { Content = "📑 既存ウィンドウのタブで開く (まとめる)", Tag = "Tab" });

            string curMode = string.IsNullOrEmpty(targetItem.BrowserMode) ? "NewWindow" : targetItem.BrowserMode;
            for (int i = 0; i < cbBrowserMode.Items.Count; i++)
            {
                if ((string)((ComboBoxItem)cbBrowserMode.Items[i]).Tag == curMode)
                {
                    cbBrowserMode.SelectedIndex = i;
                    break;
                }
            }
            if (cbBrowserMode.SelectedIndex < 0) cbBrowserMode.SelectedIndex = 0;
            mainPanel.Children.Add(cbBrowserMode);

            var lblBrowserHint = new TextBlock
            {
                Text = "※ 複数のChromeやWebサイトを一括起動する場合、「別ウィンドウ」を選択しておくとタブでまとまらずにそれぞれ画面に分割配置されます。",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            mainPanel.Children.Add(lblBrowserHint);

            // 8. 起動待ち時間 (ms)
            var delayPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            delayPanel.Children.Add(new TextBlock { Text = "起動後待機時間 (ミリ秒):", Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            txtDelay = new TextBox { Width = 80, Height = 28, Text = targetItem.DelayMs.ToString() };
            delayPanel.Children.Add(txtDelay);
            mainPanel.Children.Add(delayPanel);

            // ボタン（保存・キャンセル）
            var btnRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnCancel = new Button { Content = "キャンセル", Width = 90, Height = 34, Margin = new Thickness(0, 0, 10, 0), Cursor = System.Windows.Input.Cursors.Hand };
            btnCancel.Click += (s, e) => { this.DialogResult = false; };

            var btnSave = new Button
            {
                Content = "保存する",
                Width = 110,
                Height = 34,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnSave.Click += BtnSave_Click;

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnSave);
            mainPanel.Children.Add(btnRow);

            this.Content = mainPanel;
        }

        private TextBlock CreateFieldLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private TextBox CreateTextBox(string text)
        {
            return new TextBox
            {
                Text = text ?? "",
                Height = 32,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(6, 0, 6, 0)
            };
        }

        private void BtnCapture_Click(object sender, RoutedEventArgs e)
        {
            var windows = WindowEngine.GetOpenWindows();
            var dlg = new WindowPickerModal(windows);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true && dlg.SelectedWindow != null)
            {
                var win = dlg.SelectedWindow;
                int capX = win.Rect.Left;
                int capY = win.Rect.Top;
                int capW = win.Rect.Width;
                int capH = win.Rect.Height;

                // モニター境界への自動クランプ（はみ出し・跨ぎを防止）
                var targetScreen = WindowEngine.GetTargetScreen(win.MonitorIndex);
                int sLeft, sTop, sRight, sBottom;
                WindowEngine.GetSafeScreenBounds(targetScreen, out sLeft, out sTop, out sRight, out sBottom);
                WindowEngine.ClampToSafeBounds(sLeft, sTop, sRight, sBottom, ref capX, ref capY, ref capW, ref capH);

                txtCustomX.Text = capX.ToString();
                txtCustomY.Text = capY.ToString();
                txtCustomW.Text = capW.ToString();
                txtCustomH.Text = capH.ToString();

                // モニター選択を同期
                if (win.MonitorIndex >= 0 && win.MonitorIndex + 1 < cbMonitor.Items.Count)
                {
                    cbMonitor.SelectedIndex = win.MonitorIndex + 1;
                }

                // レイアウトを「カスタム」に切り替え
                for (int i = 0; i < cbLayout.Items.Count; i++)
                {
                    if ((string)((ComboBoxItem)cbLayout.Items[i]).Tag == "Custom")
                    {
                        cbLayout.SelectedIndex = i;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(txtName.Text) || txtName.Text == "新規ツール")
                {
                    txtName.Text = win.ProcessName;
                }

                MessageBox.Show(
                    string.Format("ウィンドウ「{0}」の情報を取得しました！\r\n（画面境界に合わせて自動調整済み）\r\n座標: ({1}, {2}), サイズ: {3}x{4}", win.Title, capX, capY, capW, capH),
                    "取得完了",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            targetItem.Name = txtName.Text.Trim();
            targetItem.Path = txtPath.Text.Trim();
            targetItem.Arguments = txtArgs.Text.Trim();

            // モニター
            if (cbMonitor.SelectedIndex <= 0) targetItem.MonitorIndex = -1;
            else targetItem.MonitorIndex = cbMonitor.SelectedIndex - 1;

            // レイアウト
            var selItem = (ComboBoxItem)cbLayout.SelectedItem;
            targetItem.LayoutType = (string)selItem.Tag;

            // ブラウザ表示モード
            if (cbBrowserMode != null && cbBrowserMode.SelectedItem != null)
            {
                var selBrowser = (ComboBoxItem)cbBrowserMode.SelectedItem;
                targetItem.BrowserMode = (string)selBrowser.Tag;
            }

            // 座標
            int val;
            if (int.TryParse(txtCustomX.Text, out val)) targetItem.CustomX = val;
            if (int.TryParse(txtCustomY.Text, out val)) targetItem.CustomY = val;
            if (int.TryParse(txtCustomW.Text, out val)) targetItem.CustomWidth = val;
            if (int.TryParse(txtCustomH.Text, out val)) targetItem.CustomHeight = val;
            if (int.TryParse(txtDelay.Text, out val)) targetItem.DelayMs = val;

            // カスタム座標保存時のモニター境界厳格クランプ
            if (targetItem.LayoutType == "Custom")
            {
                var targetScreen = WindowEngine.GetTargetScreen(targetItem.MonitorIndex);
                int sLeft, sTop, sRight, sBottom;
                WindowEngine.GetSafeScreenBounds(targetScreen, out sLeft, out sTop, out sRight, out sBottom);
                WindowEngine.ClampToSafeBounds(sLeft, sTop, sRight, sBottom,
                    ref targetItem.CustomX, ref targetItem.CustomY, ref targetItem.CustomWidth, ref targetItem.CustomHeight);
            }

            this.DialogResult = true;
        }
    }

    // ==========================================
    // 開いているウィンドウ選択モーダル
    // ==========================================
    public class WindowPickerModal : Window
    {
        public WindowEngine.OpenWindowInfo SelectedWindow { get; private set; }
        private System.Windows.Controls.ListBox listBox;

        public WindowPickerModal(List<WindowEngine.OpenWindowInfo> windows)
        {
            this.Title = "取得対象のウィンドウを選択";
            this.Width = 600;
            this.Height = 450;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            FontFamily = new FontFamily("Yu Gothic UI, Segoe UI, Meiryo");

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = "現在デスクトップで開いているウィンドウ一覧から選択してください:",
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(lbl, 0);

            listBox = new System.Windows.Controls.ListBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                Foreground = Brushes.White,
                FontSize = 13
            };
            foreach (var w in windows)
            {
                listBox.Items.Add(w);
            }
            if (listBox.Items.Count > 0) listBox.SelectedIndex = 0;
            Grid.SetRow(listBox, 1);

            var btnRow = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var btnCancel = new Button { Content = "キャンセル", Width = 90, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
            btnCancel.Click += (s, e) => { this.DialogResult = false; };

            var btnSelect = new Button
            {
                Content = "このウィンドウの位置を取得",
                Width = 190,
                Height = 32,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };
            btnSelect.Click += (s, e) =>
            {
                if (listBox.SelectedItem != null)
                {
                    SelectedWindow = (WindowEngine.OpenWindowInfo)listBox.SelectedItem;
                    this.DialogResult = true;
                }
            };

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnSelect);
            Grid.SetRow(btnRow, 2);

            grid.Children.Add(lbl);
            grid.Children.Add(listBox);
            grid.Children.Add(btnRow);

            this.Content = grid;
        }
    }

    // ==========================================
    // 汎用テキスト入力ダイアログ
    // ==========================================
    public class InputDialog : Window
    {
        public string InputText { get; private set; }
        private TextBox txtInput;

        public InputDialog(string title, string prompt, string defaultVal)
        {
            this.Title = title;
            this.Width = 400;
            this.Height = 180;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            this.ResizeMode = ResizeMode.NoResize;
            FontFamily = new FontFamily("Yu Gothic UI, Segoe UI, Meiryo");

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = prompt, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) });

            txtInput = new TextBox { Text = defaultVal, Height = 30, Margin = new Thickness(0, 0, 0, 16), VerticalContentAlignment = VerticalAlignment.Center };
            panel.Children.Add(txtInput);

            var btnRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnCancel = new Button { Content = "キャンセル", Width = 80, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            btnCancel.Click += (s, e) => { this.DialogResult = false; };

            var btnOk = new Button { Content = "OK", Width = 80, Height = 30, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")), Foreground = Brushes.White };
            btnOk.Click += (s, e) =>
            {
                InputText = txtInput.Text;
                this.DialogResult = true;
            };

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnOk);
            panel.Children.Add(btnRow);

            this.Content = panel;
        }
    }

    // ==========================================
    // 画面スナップショットから新規プロファイル作成モーダル
    // ==========================================
    public class SnapshotModal : Window
    {
        public Profile CreatedProfile { get; private set; }
        private TextBox txtProfileName;
        private List<CheckBox> checkBoxes = new List<CheckBox>();
        private List<WindowEngine.OpenWindowInfo> windowList;

        public SnapshotModal()
        {
            this.Title = "📸 現在の画面配置からプロファイルを新規作成";
            this.Width = 720;
            this.Height = 560;
            this.MinWidth = 600;
            this.MinHeight = 450;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            FontFamily = new FontFamily("Yu Gothic UI, Segoe UI, Meiryo");

            var mainGrid = new Grid { Margin = new Thickness(20) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header & Profile Name
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls (Select all etc)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Action Buttons

            // 1. Header & Profile Name
            var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            headerPanel.Children.Add(new TextBlock
            {
                Text = "📸 デスクトップ上の開いているウィンドウから自動生成",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = "検出されたウィンドウの位置・サイズをそのままカスタム座標として登録します。\r\n登録したいアプリにチェックを入れて「作成」を押してください。",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                Margin = new Thickness(0, 0, 0, 12)
            });

            var nameRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            nameRow.Children.Add(new TextBlock
            {
                Text = "プロファイル名:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                FontWeight = FontWeights.SemiBold
            });
            txtProfileName = new TextBox
            {
                Text = "作業セット " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Width = 320,
                Height = 32,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 13
            };
            nameRow.Children.Add(txtProfileName);
            headerPanel.Children.Add(nameRow);
            Grid.SetRow(headerPanel, 0);
            mainGrid.Children.Add(headerPanel);

            // 2. Select All / Deselect All Controls
            var ctrlRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var btnSelectAll = new Button { Content = "すべて選択", Width = 90, Height = 26, Margin = new Thickness(0, 0, 8, 0) };
            btnSelectAll.Click += (s, e) => { foreach (var cb in checkBoxes) cb.IsChecked = true; };
            var btnDeselectAll = new Button { Content = "すべて解除", Width = 90, Height = 26 };
            btnDeselectAll.Click += (s, e) => { foreach (var cb in checkBoxes) cb.IsChecked = false; };
            ctrlRow.Children.Add(btnSelectAll);
            ctrlRow.Children.Add(btnDeselectAll);
            Grid.SetRow(ctrlRow, 1);
            mainGrid.Children.Add(ctrlRow);

            // 3. Window List
            windowList = WindowEngine.GetOpenWindows();
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var listPanel = new StackPanel();

            if (windowList.Count == 0)
            {
                listPanel.Children.Add(new TextBlock
                {
                    Text = "検出可能なウィンドウが見つかりませんでした。\r\nアプリを起動して画面上に配置してから再度お試しください。",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
                    Margin = new Thickness(10)
                });
            }
            else
            {
                for (int i = 0; i < windowList.Count; i++)
                {
                    var win = windowList[i];
                    var itemBorder = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(0, 0, 0, 6),
                        Padding = new Thickness(12, 10, 12, 10)
                    };

                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var chk = new CheckBox
                    {
                        IsChecked = true,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 12, 0)
                    };
                    checkBoxes.Add(chk);
                    Grid.SetColumn(chk, 0);

                    var infoPanel = new StackPanel();
                    var titleText = new TextBlock
                    {
                        Text = string.IsNullOrEmpty(win.Title) ? win.ProcessName : win.Title,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        FontSize = 13,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    string monStr = win.MonitorIndex >= 0 ? string.Format("モニター {0}", win.MonitorIndex + 1) : "メインモニター";
                    var detailText = new TextBlock
                    {
                        Text = string.Format("プロセス: {0} | 座標: ({1}, {2}) | サイズ: {3}x{4} | 配置先: {5}",
                            win.ProcessName, win.Rect.Left, win.Rect.Top, win.Rect.Width, win.Rect.Height, monStr),
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                        FontSize = 11,
                        Margin = new Thickness(0, 3, 0, 0)
                    };
                    infoPanel.Children.Add(titleText);
                    infoPanel.Children.Add(detailText);
                    Grid.SetColumn(infoPanel, 1);

                    row.Children.Add(chk);
                    row.Children.Add(infoPanel);
                    itemBorder.Child = row;
                    listPanel.Children.Add(itemBorder);
                }
            }

            scroll.Content = listPanel;
            Grid.SetRow(scroll, 2);
            mainGrid.Children.Add(scroll);

            // 4. Action Buttons
            var actionPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };

            var btnCancel = new Button
            {
                Content = "キャンセル",
                Width = 90,
                Height = 34,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnCancel.Click += (s, e) => { this.DialogResult = false; };

            var btnCreate = new Button
            {
                Content = "✅ この画面配置でプロファイル作成",
                Width = 240,
                Height = 34,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0EA5E9")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };
            btnCreate.Click += (s, e) =>
            {
                string profName = txtProfileName.Text.Trim();
                if (string.IsNullOrEmpty(profName))
                {
                    MessageBox.Show("プロファイル名を入力してください。", "入力確認", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newProfile = new Profile { Name = profName };
                int selectedCount = 0;

                for (int i = 0; i < windowList.Count; i++)
                {
                    if (checkBoxes[i].IsChecked == true)
                    {
                        var win = windowList[i];
                        selectedCount++;

                        var item = new ToolItem
                        {
                            Name = string.IsNullOrEmpty(win.Title) ? win.ProcessName : win.Title,
                            Path = !string.IsNullOrEmpty(win.ExePath) ? win.ExePath : win.ProcessName + ".exe",
                            MonitorIndex = win.MonitorIndex,
                            LayoutType = "Custom",
                            CustomX = win.Rect.Left,
                            CustomY = win.Rect.Top,
                            CustomWidth = win.Rect.Width,
                            CustomHeight = win.Rect.Height,
                            DelayMs = 300,
                            Enabled = true
                        };
                        newProfile.Items.Add(item);
                    }
                }

                if (selectedCount == 0)
                {
                    MessageBox.Show("少なくとも1つのウィンドウにチェックを入れてください。", "確認", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CreatedProfile = newProfile;
                this.DialogResult = true;
            };

            actionPanel.Children.Add(btnCancel);
            actionPanel.Children.Add(btnCreate);
            Grid.SetRow(actionPanel, 3);
            mainGrid.Children.Add(actionPanel);

            this.Content = mainGrid;
        }
    }

    // ==========================================
    // エントリーポイント
    // ==========================================
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // コマンドライン引数: --launch-all (GUIを表示せずに直接一括起動) / --tray (トレイ常駐)
            bool silentLaunch = false;
            bool startInTray = false;
            foreach (var arg in args)
            {
                if (arg.Equals("--launch-all", StringComparison.OrdinalIgnoreCase))
                {
                    silentLaunch = true;
                    break;
                }
                if (arg.Equals("--tray", StringComparison.OrdinalIgnoreCase))
                {
                    startInTray = true;
                }
            }

            if (silentLaunch)
            {
                string configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace_config.json");
                if (File.Exists(configPath))
                {
                    try
                    {
                        string json = File.ReadAllText(configPath, Encoding.UTF8);
                        var cfg = SimpleJson.DeserializeConfig(json);
                        var profile = cfg.Profiles.Find(x => x.Id == cfg.SelectedProfileId);
                        if (profile == null && cfg.Profiles.Count > 0) profile = cfg.Profiles[0];

                        if (profile != null)
                        {
                            var usedHwnds = new HashSet<IntPtr>();
                            foreach (var item in profile.Items)
                            {
                                if (item.Enabled)
                                {
                                    var hwnd = WindowEngine.LaunchAndFindWindow(item, usedHwnds);
                                    int waitMs = item.DelayMs;
                                    if (WindowEngine.IsBrowserTarget(item.Path) && waitMs < 400)
                                    {
                                        waitMs = 400;
                                    }
                                    if (waitMs > 0) Thread.Sleep(waitMs);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Silent launch error: " + ex.Message);
                    }
                }
                return;
            }

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show("エラーが発生しました: " + e.Exception.Message, "Workspace Hub エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            var mainWin = new MainWindow();
            if (!startInTray)
            {
                mainWin.Show();
            }
            app.Run();
        }
    }
}
