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
        public const string GameExe32 = "TheForest32.exe";
        public const string GameExeVR = "TheForestVR.exe";

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

            if (args.Length >= 2 && args[0] == "--uninstall")
            {
                try
                {
                    Installer.Uninstall(args[1]);
                    MessageBox.Show("Готово.\n\nЛоадер удалён. Твои моды в DllMods остались на месте.",
                        AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка удаления:\n" + ex.Message,
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

        // Папка может содержать только 32-битный exe (например, репак).
        // Для запуска выбираем тот, который реально есть.
        public static string PlayableExe(string dir)
        {
            string x64 = Path.Combine(dir, GameExe);
            if (File.Exists(x64)) return x64;

            string x86 = Path.Combine(dir, GameExe32);
            if (File.Exists(x86)) return x86;

            return null;
        }

        public static bool Is64Bit(string exe)
        {
            try
            {
                using (FileStream fs = new FileStream(exe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        fs.Position = 0x3C;
                        int peOffset = br.ReadInt32();
                        fs.Position = peOffset + 4;
                        ushort machine = br.ReadUInt16();

                        // 0x014C — x86, 0x8664 — x64. Всё остальное считаем x64.
                        return machine != 0x014C;
                    }
                }
            }
            catch
            {
                return true;
            }
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
                        if (Program.PlayableExe(dir) != null) return dir;
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
            if (Program.PlayableExe(classic) != null) return classic;

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
                    if (Program.PlayableExe(candidate) != null) return candidate;
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

        public static bool HasAnyLoaderFile(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            for (int i = 0; i < Files.Length; i++)
            {
                if (File.Exists(Path.Combine(dir, Files[i]))) return true;
            }
            return File.Exists(Path.Combine(Path.Combine(dir, "DllMods"), "SampleCheats.dll"));
        }

        public static void Install(string dir)
        {
            if (string.IsNullOrEmpty(dir)) throw new ArgumentException("Не указана папка игры.");

            string playable = Program.PlayableExe(dir);
            if (playable == null)
                throw new FileNotFoundException("В папке нет " + Program.GameExe +
                    " (или " + Program.GameExe32 + "). Укажи папку The Forest.");

            // Если игра 32-битная — нужен x86-прокси, иначе 0xc000007b.
            string proxy = Program.Is64Bit(playable) ? "payload.winhttp.dll" : "payload.winhttp.x86.dll";

            Extract(proxy, Path.Combine(dir, "winhttp.dll"), false);
            Extract("payload.doorstop_config.ini", Path.Combine(dir, "doorstop_config.ini"), false);
            Extract("payload.Doorstop.dll", Path.Combine(dir, "Doorstop.dll"), false);

            string mods = Path.Combine(dir, "DllMods");
            if (!Directory.Exists(mods)) Directory.CreateDirectory(mods);

            // Всегда обновляем SampleCheats. Старый файл не удаляем, а держим
            // рядом как .bak — вдруг пользователь его правил.
            Extract("payload.SampleCheats.dll", Path.Combine(mods, "SampleCheats.dll"), true);
        }

        public static void Uninstall(string dir)
        {
            for (int i = 0; i < Files.Length; i++)
            {
                DeleteIfExists(Path.Combine(dir, Files[i]));
            }

            DeleteIfExists(Path.Combine(dir, "doorstop_loader.log"));
            DeleteIfExists(Path.Combine(dir, "doorstop_loader.log.old"));

            string mods = Path.Combine(dir, "DllMods");
            DeleteIfExists(Path.Combine(mods, "SampleCheats.dll"));
            DeleteIfExists(Path.Combine(mods, "SampleCheats.dll.bak"));
            DeleteIfExists(Path.Combine(mods, "SampleCheats.dll.tmp"));

            // Чужие моды не трогаем: удаляем папку, только если она опустела.
            try
            {
                if (Directory.Exists(mods) && Directory.GetFileSystemEntries(mods).Length == 0)
                    Directory.Delete(mods);
            }
            catch
            {
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private static void Extract(string resource, string dest, bool backupIfDifferent)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string tmp = dest + ".tmp";

            try
            {
                if (File.Exists(tmp)) File.Delete(tmp);

                using (Stream src = asm.GetManifestResourceStream(resource))
                {
                    if (src == null) throw new Exception("Повреждён файл лаунчера: нет ресурса " + resource);
                    using (FileStream fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write))
                    {
                        byte[] buffer = new byte[81920];
                        int read;
                        while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fs.Write(buffer, 0, read);
                        }
                    }
                }

                if (File.Exists(dest) && FilesEqual(tmp, dest))
                {
                    File.Delete(tmp);
                    return;
                }

                if (backupIfDifferent && File.Exists(dest))
                {
                    try
                    {
                        File.Copy(dest, dest + ".bak", true);
                    }
                    catch
                    {
                    }
                }

                Replace(tmp, dest);
            }
            catch
            {
                // Не оставляем мусор и, главное, не оставляем недописанный
                // winhttp.dll — рабочий файл заменяется только целиком.
                try
                {
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
                catch
                {
                }
                throw;
            }
        }

        private static void Replace(string tmp, string dest)
        {
            if (!File.Exists(dest))
            {
                File.Move(tmp, dest);
                return;
            }

            try
            {
                // Атомарная замена в пределах одного тома (NTFS).
                File.Replace(tmp, dest, null);
            }
            catch
            {
                File.Delete(dest);
                File.Move(tmp, dest);
            }
        }

        private static bool FilesEqual(string a, string b)
        {
            try
            {
                FileInfo fa = new FileInfo(a);
                FileInfo fb = new FileInfo(b);
                if (fa.Length != fb.Length) return false;

                using (FileStream sa = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream sb = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] ba = new byte[8192];
                    byte[] bb = new byte[8192];
                    while (true)
                    {
                        int na = sa.Read(ba, 0, ba.Length);
                        int nb = sb.Read(bb, 0, bb.Length);
                        if (na != nb) return false;
                        if (na == 0) return true;

                        for (int i = 0; i < na; i++)
                        {
                            if (ba[i] != bb[i]) return false;
                        }
                    }
                }
            }
            catch
            {
                return false;
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
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            Text = Program.AppTitle + " " + version.Major + "." + version.Minor + "." + version.Build;

            // DPI: окно должно тянуться, иначе при 125–150% часть элементов
            // уезжает за границы фиксированного размера.
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(580, 448);
            MinimumSize = new Size(596, 486);
            FormBorderStyle = FormBorderStyle.Sizable;
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
            title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Загрузчик модов без патча игры  ·  UnityDoorstop 4.6.0  ·  x64 / x86";
            sub.ForeColor = Muted;
            sub.SetBounds(22, 50, 540, 20);
            sub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(sub);

            Label pathLabel = new Label();
            pathLabel.Text = "Папка игры:";
            pathLabel.SetBounds(22, 84, 200, 20);
            pathLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Controls.Add(pathLabel);

            _path = new TextBox();
            _path.SetBounds(22, 106, 428, 24);
            _path.BackColor = Panel;
            _path.ForeColor = Color.White;
            _path.BorderStyle = BorderStyle.FixedSingle;
            _path.TextChanged += delegate { RefreshState(); };
            _path.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_path);

            Button browse = MakeButton("Обзор...", 458, 105, 100, 26, false);
            browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browse.Click += delegate { Browse(); };
            Controls.Add(browse);

            _state = new Label();
            _state.SetBounds(22, 142, 540, 20);
            _state.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _state.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_state);

            Button install = MakeButton("Установить", 22, 172, 170, 40, true);
            install.Click += delegate { DoInstall(); };
            Controls.Add(install);

            _play = MakeButton("Запустить игру", 200, 172, 170, 40, false);
            _play.Click += delegate { DoPlay(); };
            Controls.Add(_play);

            Button remove = MakeButton("Удалить", 378, 172, 180, 40, false);
            remove.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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
            _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
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

            if (found != null && File.Exists(Path.Combine(found, Program.GameExeVR)))
            {
                Log("TheForestVR.exe не поддерживается: моды работают только в обычной версии игры.");
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

            if (Program.PlayableExe(dir) == null)
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
            if (Program.PlayableExe(dir) == null)
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
                Elevate("--install \"" + dir + "\"");
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

        private void Elevate(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, arguments);
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
            if (!Installer.HasAnyLoaderFile(dir))
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

            if (MessageBox.Show("Удалить лоадер?\n\nТвои моды в DllMods останутся, удалится только SampleCheats.dll.\nФайлы игры не пострадают.",
                    Program.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                Installer.Uninstall(dir);
                Log("Лоадер удалён. Моды пользователя и файлы игры не изменялись.");
                RefreshState();
            }
            catch (UnauthorizedAccessException)
            {
                Elevate("--uninstall \"" + dir + "\"");
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
            string exe = Program.PlayableExe(dir);
            if (exe == null)
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
            string[] names = new string[] { "TheForest", "TheForest32", "TheForestVR" };
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    if (Process.GetProcessesByName(names[i]).Length > 0) return true;
                }
                catch
                {
                }
            }
            return false;
        }
    }
}
