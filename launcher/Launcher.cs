using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ForestModLoader
{
    internal static class Program
    {
        public const string AppTitle = "The Forest Mod Loader";
        public const string GameExe = "TheForest.exe";

        [STAThread]
        private static void Main(string[] args)
        {
            // Тихая установка (используется при перезапуске с правами администратора и для тестов)
            if (args.Length >= 2 && args[0] == "--install")
            {
                try
                {
                    Installer.Install(args[1]);
                    MessageBox.Show("Готово.\n\nЛоадер установлен в:\n" + args[1] +
                                    "\n\nЗапускай игру через Steam как обычно.",
                        AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка установки:\n" + ex.Message,
                        AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            if (args.Length >= 2 && args[0] == "--selftest")
            {
                try
                {
                    Installer.Install(args[1]);
                    bool ok = Installer.IsInstalled(args[1]);
                    Environment.Exit(ok ? 0 : 2);
                }
                catch
                {
                    Environment.Exit(3);
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal static class Installer
    {
        private static readonly string[] Files = new string[]
        {
            "winhttp.dll",
            "doorstop_config.ini",
            "Doorstop.dll"
        };

        public static string GameDirFromRegistry()
        {
            string[] keys = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 242760",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 242760"
            };

            for (int i = 0; i < keys.Length; i++)
            {
                try
                {
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(keys[i]))
                    {
                        if (k == null) continue;
                        object v = k.GetValue("InstallLocation");
                        if (v == null) continue;
                        string dir = v.ToString();
                        if (File.Exists(Path.Combine(dir, Program.GameExe))) return dir;
                    }
                }
                catch
                {
                }
            }
            return null;
        }

        private static string SteamRoot()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("SteamPath");
                        if (v != null) return v.ToString().Replace('/', '\\');
                    }
                }
            }
            catch
            {
            }

            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("InstallPath");
                        if (v != null) return v.ToString().Replace('/', '\\');
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static string FindGame()
        {
            string direct = GameDirFromRegistry();
            if (direct != null) return direct;

            string steam = SteamRoot();
            if (steam == null) return null;

            string classic = Path.Combine(steam, @"steamapps\common\The Forest");
            if (File.Exists(Path.Combine(classic, Program.GameExe))) return classic;

            // Парсим libraryfolders.vdf: берём все строки в кавычках, похожие на путь
            string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
            if (!File.Exists(vdf)) return null;

            try
            {
                string text = File.ReadAllText(vdf);
                MatchCollection ms = Regex.Matches(text, "\"([^\"]*[\\\\/][^\"]*)\"");
                foreach (Match m in ms)
                {
                    string raw = m.Groups[1].Value.Replace("\\\\", "\\");
                    string candidate = Path.Combine(raw, @"steamapps\common\The Forest");
                    if (File.Exists(Path.Combine(candidate, Program.GameExe))) return candidate;
                }
            }
            catch
            {
            }

            return null;
        }

        public static bool IsInstalled(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            for (int i = 0; i < Files.Length; i++)
            {
                if (!File.Exists(Path.Combine(dir, Files[i]))) return false;
            }
            return true;
        }

        public static void Install(string dir)
        {
            if (string.IsNullOrEmpty(dir)) throw new ArgumentException("Не указана папка игры.");
            if (!File.Exists(Path.Combine(dir, Program.GameExe)))
                throw new FileNotFoundException("В папке нет " + Program.GameExe + ". Укажи папку The Forest.");

            Extract("payload.winhttp.dll", Path.Combine(dir, "winhttp.dll"));
            Extract("payload.doorstop_config.ini", Path.Combine(dir, "doorstop_config.ini"));
            Extract("payload.Doorstop.dll", Path.Combine(dir, "Doorstop.dll"));

            string mods = Path.Combine(dir, "DllMods");
            if (!Directory.Exists(mods)) Directory.CreateDirectory(mods);

            string sample = Path.Combine(mods, "SampleCheats.dll");
            if (!File.Exists(sample)) Extract("payload.SampleCheats.dll", sample);
        }

        public static void Uninstall(string dir)
        {
            for (int i = 0; i < Files.Length; i++)
            {
                string p = Path.Combine(dir, Files[i]);
                if (File.Exists(p)) File.Delete(p);
            }

            string log = Path.Combine(dir, "doorstop_loader.log");
            if (File.Exists(log)) File.Delete(log);

            string mods = Path.Combine(dir, "DllMods");
            if (Directory.Exists(mods)) Directory.Delete(mods, true);
        }

        private static void Extract(string resource, string dest)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream src = asm.GetManifestResourceStream(resource))
            {
                if (src == null) throw new Exception("Повреждён файл лаунчера: нет ресурса " + resource);
                using (FileStream fs = new FileStream(dest, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fs.Write(buffer, 0, read);
                    }
                }
            }
        }
    }

    internal class MainForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(14, 21, 18);
        private static readonly Color Panel = Color.FromArgb(22, 34, 27);
        private static readonly Color Lime = Color.FromArgb(163, 230, 100);
        private static readonly Color Muted = Color.FromArgb(127, 160, 138);
        private static readonly Color Warn = Color.FromArgb(235, 170, 80);

        private TextBox _path;
        private Label _state;
        private TextBox _log;
        private Button _play;

        public MainForm()
        {
            Text = Program.AppTitle + " " + Assembly.GetExecutingAssembly().GetName().Version.Major +
                   "." + Assembly.GetExecutingAssembly().GetName().Version.Minor;
            ClientSize = new Size(580, 448);
            MinimumSize = new Size(596, 486);
            MaximumSize = new Size(596, 486);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            Label title = new Label();
            title.Text = "The Forest Mod Loader";
            title.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            title.ForeColor = Lime;
            title.SetBounds(20, 14, 540, 36);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Загрузчик модов без патча игры  ·  UnityDoorstop 4.6.0  ·  x64";
            sub.ForeColor = Muted;
            sub.SetBounds(22, 50, 540, 20);
            Controls.Add(sub);

            Label pathLabel = new Label();
            pathLabel.Text = "Папка игры:";
            pathLabel.SetBounds(22, 84, 200, 20);
            Controls.Add(pathLabel);

            _path = new TextBox();
            _path.SetBounds(22, 106, 428, 24);
            _path.BackColor = Panel;
            _path.ForeColor = Color.White;
            _path.BorderStyle = BorderStyle.FixedSingle;
            _path.TextChanged += delegate { RefreshState(); };
            Controls.Add(_path);

            Button browse = MakeButton("Обзор...", 458, 105, 100, 26, false);
            browse.Click += delegate { Browse(); };
            Controls.Add(browse);

            _state = new Label();
            _state.SetBounds(22, 142, 540, 20);
            _state.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Controls.Add(_state);

            Button install = MakeButton("Установить", 22, 172, 170, 40, true);
            install.Click += delegate { DoInstall(); };
            Controls.Add(install);

            _play = MakeButton("Запустить игру", 200, 172, 170, 40, false);
            _play.Click += delegate { DoPlay(); };
            Controls.Add(_play);

            Button remove = MakeButton("Удалить", 378, 172, 180, 40, false);
            remove.Click += delegate { DoUninstall(); };
            Controls.Add(remove);

            _log = new TextBox();
            _log.SetBounds(22, 228, 536, 196);
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = Color.FromArgb(9, 15, 12);
            _log.ForeColor = Color.FromArgb(154, 190, 165);
            _log.BorderStyle = BorderStyle.FixedSingle;
            _log.Font = new Font("Consolas", 8.5f);
            Controls.Add(_log);

            string found = Installer.FindGame();
            if (found != null)
            {
                _path.Text = found;
                Log("Игра найдена: " + found);
            }
            else
            {
                Log("Игра не найдена автоматически — укажи папку вручную.");
            }

            RefreshState();
        }

        private Button MakeButton(string text, int x, int y, int w, int h, bool accent)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Lime;
            b.BackColor = accent ? Lime : Panel;
            b.ForeColor = accent ? Color.FromArgb(12, 20, 14) : Lime;
            b.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private void Browse()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Выбери папку с TheForest.exe";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _path.Text = dlg.SelectedPath;
                }
            }
        }

        private void RefreshState()
        {
            string dir = _path.Text.Trim();
            if (dir.Length == 0)
            {
                _state.Text = "Статус: папка не выбрана";
                _state.ForeColor = Warn;
                return;
            }

            if (!File.Exists(Path.Combine(dir, Program.GameExe)))
            {
                _state.Text = "Статус: TheForest.exe не найден в этой папке";
                _state.ForeColor = Warn;
                return;
            }

            if (Installer.IsInstalled(dir))
            {
                _state.Text = "Статус: лоадер установлен";
                _state.ForeColor = Lime;
            }
            else
            {
                _state.Text = "Статус: не установлен";
                _state.ForeColor = Warn;
            }
        }

        private void DoInstall()
        {
            string dir = _path.Text.Trim();
            if (!File.Exists(Path.Combine(dir, Program.GameExe)))
            {
                MessageBox.Show("Выбери папку, где лежит TheForest.exe.", Program.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (IsGameRunning())
            {
                MessageBox.Show("Игра запущена — Windows не даст перезаписать winhttp.dll.\nЗакрой игру и повтори.",
                    Program.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Installer.Install(dir);
                Log("Установлено: winhttp.dll, doorstop_config.ini, Doorstop.dll, DllMods\\SampleCheats.dll");
                RefreshState();
            }
            catch (UnauthorizedAccessException)
            {
                Elevate(dir);
            }
            catch (IOException ex)
            {
                Log("Ошибка ввода-вывода: " + ex.Message);
                MessageBox.Show("Не удалось записать файлы.\n\nЗакрой игру и повтори.\n\n" + ex.Message,
                    Program.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                Log("Ошибка: " + ex.Message);
                MessageBox.Show(ex.Message, Program.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Elevate(string dir)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath,
                    "--install \"" + dir + "\"");
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                Close();
            }
            catch (Exception ex)
            {
                Log("Не удалось получить права администратора: " + ex.Message);
                MessageBox.Show("Папка игры защищена. Запусти лаунчер от имени администратора.",
                    Program.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DoUninstall()
        {
            string dir = _path.Text.Trim();
            if (!Installer.IsInstalled(dir))
            {
                MessageBox.Show("Лоадер не установлен.", Program.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (IsGameRunning())
            {
                MessageBox.Show("Игра запущена. Закрой её и повтори.", Program.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Удалить лоадер и папку DllMods (вместе с модами)?\n\nФайлы игры не пострадают.",
                    Program.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                Installer.Uninstall(dir);
                Log("Лоадер удалён. Игра не изменялась.");
                RefreshState();
            }
            catch (Exception ex)
            {
                Log("Ошибка удаления: " + ex.Message);
                MessageBox.Show("Не удалось удалить файлы. Закрой игру и повтори.\n\n" + ex.Message,
                    Program.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoPlay()
        {
            string dir = _path.Text.Trim();
            string exe = Path.Combine(dir, Program.GameExe);
            if (!File.Exists(exe))
            {
                MessageBox.Show("TheForest.exe не найден.", Program.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (IsGameRunning())
            {
                MessageBox.Show("The Forest уже запущена.", Program.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!Installer.IsInstalled(dir) &&
                MessageBox.Show("Лоадер не установлен. Установить сейчас?", Program.AppTitle,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                DoInstall();
                if (!Installer.IsInstalled(dir)) return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                psi.WorkingDirectory = dir;
                Process.Start(psi);
                Log("Запуск: " + exe);
            }
            catch (Exception ex)
            {
                Log("Ошибка запуска: " + ex.Message);
                MessageBox.Show("Не удалось запустить игру:\n" + ex.Message, Program.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Log(string message)
        {
            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + "\r\n");
        }

        private static bool IsGameRunning()
        {
            try
            {
                return Process.GetProcessesByName("TheForest").Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
