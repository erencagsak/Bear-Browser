using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace Bear_Browser.Services
{
    public class CrashReportingService
    {
        private static CrashReportingService? _instance;
        public static CrashReportingService Instance => _instance ??= new CrashReportingService();

        public string LogsDirectory { get; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        private const uint MB_ICONERROR = 0x00000010;
        private const uint MB_YESNO = 0x00000004;
        private const int IDYES = 6;

        public CrashReportingService()
        {
            LogsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser",
                "CrashLogs"
            );
        }

        public void Initialize(Application? app = null)
        {
            try
            {
                Directory.CreateDirectory(LogsDirectory);

                // 1. UI Thread unhandled exceptions (WinUI 3 XAML)
                var targetApp = app ?? Application.Current;
                if (targetApp != null)
                {
                    targetApp.UnhandledException += (sender, args) =>
                    {
                        try
                        {
                            // Uygulamanın aniden kapanmasını önleyip hatayı detaylıca logla
                            args.Handled = true;
                            string logFile = LogCrash(args.Exception, "UI Thread (Xaml.UnhandledException)", isFatal: false);
                            PromptUserAboutCrash(args.Exception, logFile);
                        }
                        catch { }
                    };
                }

                // 2. Arka plan thread ve genel AppDomain hataları
                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                {
                    try
                    {
                        if (args.ExceptionObject is Exception ex)
                        {
                            string logFile = LogCrash(ex, "Background Thread (AppDomain.UnhandledException)", isFatal: args.IsTerminating);
                            PromptUserAboutCrash(ex, logFile);
                        }
                    }
                    catch { }
                };

                // 3. Yakalanmamış async Task hataları (TaskScheduler)
                TaskScheduler.UnobservedTaskException += (sender, args) =>
                {
                    try
                    {
                        args.SetObserved();
                        string logFile = LogCrash(args.Exception, "Async Task (TaskScheduler.UnobservedTaskException)", isFatal: false);
                        PromptUserAboutCrash(args.Exception, logFile);
                    }
                    catch { }
                };
            }
            catch { }
        }

        public string LogCrash(Exception ex, string source, bool isFatal = false)
        {
            try
            {
                Directory.CreateDirectory(LogsDirectory);

                var now = DateTime.Now;
                string fileName = $"crash_{now:yyyyMMdd_HHmmss_fff}.txt";
                string logFilePath = Path.Combine(LogsDirectory, fileName);
                string latestFilePath = Path.Combine(LogsDirectory, "crash_latest.txt");

                long ramMb = 0;
                try
                {
                    ramMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
                }
                catch { }

                var version = Assembly.GetExecutingAssembly().GetName().Version;
                string versionStr = version != null ? version.ToString() : "1.0.0.0";
                string arch = Environment.Is64BitProcess ? "64-bit (x64)" : "32-bit (x86)";

                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("                 BEAR BROWSER - HATA VE ÇÖKME GÜNLÜĞÜ (CRASH REPORT)            ");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Rapor Zamanı (Yerel) : {now:dd.MM.yyyy HH:mm:ss.fff}");
                sb.AppendLine($"Rapor Zamanı (UTC)   : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"Hata Kaynağı         : {source}");
                sb.AppendLine($"Kritiklik Durumu     : {(isFatal ? "KRİTİK (Uygulama Kapanıyor)" : "YAKALANDI (Kurtarıldı)")}");
                sb.AppendLine($"Uygulama Sürümü      : {versionStr}");
                sb.AppendLine($"İşlemci Mimarisi     : {arch}");
                sb.AppendLine($"İşletim Sistemi      : {Environment.OSVersion}");
                sb.AppendLine($"Aktif RAM Tüketimi   : {ramMb} MB");
                sb.AppendLine($"İşlem Kimliği (PID)  : {Process.GetCurrentProcess().Id}");
                sb.AppendLine();

                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("1. HATA VE MESAJ AYRINTILARI (EXCEPTION DETAILS)");
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine($"Hata Türü (Type)     : {ex.GetType().FullName}");
                sb.AppendLine($"Hata Mesajı (Message): {ex.Message}");
                sb.AppendLine($"HResult Kodu         : 0x{ex.HResult:X8}");
                if (ex.TargetSite != null)
                {
                    sb.AppendLine($"Hedef Metot (Method) : {ex.TargetSite.DeclaringType?.FullName}.{ex.TargetSite.Name}");
                }
                sb.AppendLine();

                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("2. NOKTA ATIŞI HATA KONUMU (STACK TRACE - DOSYA VE SATIR NUMARALARI)");
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine(string.IsNullOrWhiteSpace(ex.StackTrace) ? "Çağrı yığını bilgisi bulunamadı." : ex.StackTrace);
                sb.AppendLine();

                if (ex.InnerException != null)
                {
                    sb.AppendLine("--------------------------------------------------------------------------------");
                    sb.AppendLine("3. İÇ HATA (INNER EXCEPTION)");
                    sb.AppendLine("--------------------------------------------------------------------------------");
                    var inner = ex.InnerException;
                    int level = 1;
                    while (inner != null)
                    {
                        sb.AppendLine($"[İç Hata #{level}]");
                        sb.AppendLine($"Tür: {inner.GetType().FullName}");
                        sb.AppendLine($"Mesaj: {inner.Message}");
                        sb.AppendLine($"Stack Trace:\n{inner.StackTrace}");
                        sb.AppendLine();
                        inner = inner.InnerException;
                        level++;
                    }
                }

                sb.AppendLine("================================================================================");
                sb.AppendLine("Rapor Sonu - Bu dosyayı inceleyerek hatanın oluştuğu satıra doğrudan gidebilirsiniz.");
                sb.AppendLine("================================================================================");

                string content = sb.ToString();
                File.WriteAllText(logFilePath, content, Encoding.UTF8);
                File.WriteAllText(latestFilePath, content, Encoding.UTF8);

                return logFilePath;
            }
            catch
            {
                return "";
            }
        }

        public void LogCustomError(string title, string details)
        {
            try
            {
                Directory.CreateDirectory(LogsDirectory);
                string fileName = $"error_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string path = Path.Combine(LogsDirectory, fileName);
                string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n{details}\n";
                File.WriteAllText(path, text, Encoding.UTF8);
            }
            catch { }
        }

        public void PromptUserAboutCrash(Exception ex, string logFilePath)
        {
            try
            {
                string msg =
                    $"Bear Browser beklenmeyen bir hatayla karşılaştı!\n\n" +
                    $"Hata Türü: {ex.GetType().Name}\n" +
                    $"Hata Mesajı: {ex.Message}\n\n" +
                    $"Nokta atışı hata konumu ve satır bilgisi günlüğe kaydedildi:\n" +
                    $"{logFilePath}\n\n" +
                    $"Ayrıntılı hata günlüğünü şimdi Not Defteri ile açmak istiyor musunuz?";

                int result = MessageBox(IntPtr.Zero, msg, "Bear Browser - Hata Raporlama", MB_YESNO | MB_ICONERROR);
                if (result == IDYES && File.Exists(logFilePath))
                {
                    Process.Start(new ProcessStartInfo("notepad.exe", $"\"{logFilePath}\"") { UseShellExecute = true });
                }
            }
            catch { }
        }

        public void OpenLogsFolder()
        {
            try
            {
                Directory.CreateDirectory(LogsDirectory);
                Process.Start(new ProcessStartInfo("explorer.exe", LogsDirectory) { UseShellExecute = true });
            }
            catch { }
        }

        public void TriggerTestCrash()
        {
            // Nokta atışı test çökmesi simülasyonu
            throw new InvalidOperationException("Test amaçlı tetiklenen simüle edilmiş çökme hatası (Crash Test)! Nokta atışı dosya ve satır tespiti başarılı.");
        }
    }
}
