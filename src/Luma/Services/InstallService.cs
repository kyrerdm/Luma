using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Luma.Services;

internal sealed record InstallPlan(string Root, string Directory, string Executable, string UninstallScript);
internal static class InstallService
{
    internal static readonly string[] Notices = { "KULLANIM.txt", "DOTNET-LICENSE.txt", "DOTNET-THIRD-PARTY-NOTICES.txt", "WINDOWS-DESKTOP-LICENSE.txt", "WINRT-LICENSE.txt", "WINDOWS-SDK-NOTICE.txt" };
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Luma");
    public static InstallPlan Plan(string root, Version version)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (root == Path.GetPathRoot(root)) throw new ArgumentException("Kurulum için uygulama klasörü gerekir.");
        var directory = Path.Combine(root, version.ToString(3));
        return new(root, directory, Path.Combine(directory, "Luma.exe"), Path.Combine(root, "uninstall.ps1"));
    }
    internal static byte[] ReadNotice(string name)
    {
        if (Array.IndexOf(Notices, name) < 0) throw new ArgumentException("Bilinmeyen bildirim.");
        using var stream = typeof(InstallService).Assembly.GetManifestResourceStream("Luma.Install." + name) ?? throw new IOException("Kurulum bildirimi bulunamadı: " + name);
        using var buffer = new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray();
    }
    public static InstallPlan Install(bool desktopShortcut)
    {
        var source = Environment.ProcessPath ?? throw new IOException("Uygulama dosyası bulunamadı.");
        if (new FileInfo(source).Length < 20 * 1024 * 1024) throw new InvalidOperationException("Kurulum için Releases bölümündeki çalışma zamanı dahil Luma paketini aç.");
        return InstallFiles(source, Plan(DefaultRoot, AppVersion.Current), desktopShortcut, Register);
    }
    internal static InstallPlan InstallFiles(string source, InstallPlan plan, bool desktopShortcut, Action<InstallPlan, bool> register)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(plan.Root));
        if (root == Path.GetPathRoot(root) || !Path.GetFullPath(plan.Directory).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(plan.Executable)), Path.GetFullPath(plan.Directory), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(plan.UninstallScript)), root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Kurulum dosyaları hedef klasör içinde olmalıdır.");
        if (Directory.Exists(plan.Root) && (File.GetAttributes(plan.Root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Kurulum klasörü bir bağlantı olamaz.");
        if (Directory.Exists(plan.Directory) && (File.GetAttributes(plan.Directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("Sürüm klasörü bir bağlantı olamaz.");
        Directory.CreateDirectory(plan.Directory);
        if (!source.Equals(plan.Executable, StringComparison.OrdinalIgnoreCase))
        {
            var staged = Path.Combine(plan.Directory, ".install-" + Guid.NewGuid().ToString("N") + ".tmp");
            try { File.Copy(source, staged); File.Move(staged, plan.Executable, true); }
            finally { if (File.Exists(staged)) File.Delete(staged); }
        }
        foreach (var name in Notices) File.WriteAllBytes(Path.Combine(plan.Directory, name), ReadNotice(name));
        File.WriteAllText(plan.UninstallScript, UninstallScript, new UTF8Encoding(true));
        register(plan, desktopShortcut);
        return plan;
    }
    internal static void Register(InstallPlan plan, bool desktopShortcut)
    {
        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Luma.lnk"), plan.Executable);
        var desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Luma.lnk");
        if (desktopShortcut || OwnShortcut(desktopPath, plan.Root)) CreateShortcut(desktopPath, plan.Executable);
        using var registration = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LumaFlyouts");
        registration.SetValue("DisplayName", "Luma"); registration.SetValue("DisplayVersion", Path.GetFileName(plan.Directory));
        registration.SetValue("Publisher", "kyrerdm"); registration.SetValue("InstallLocation", plan.Root); registration.SetValue("DisplayIcon", plan.Executable);
        registration.SetValue("URLInfoAbout", "https://github.com/kyrerdm/Luma");
        var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        registration.SetValue("UninstallString", $"\"{powershell}\" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{plan.UninstallScript}\"");
        registration.SetValue("NoModify", 1); registration.SetValue("NoRepair", 1);
    }
    private static bool OwnShortcut(string path, string root)
    {
        if (!File.Exists(path)) return false;
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            dynamic automation = shell!; shortcut = automation.CreateShortcut(path); dynamic link = shortcut;
            return ((string)link.TargetPath).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (shortcut is not null) Marshal.ReleaseComObject(shortcut); if (shell is not null) Marshal.ReleaseComObject(shell); }
    }
    private static void CreateShortcut(string path, string executable)
    {
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Başlat menüsü kısayolu oluşturulamıyor."));
            dynamic automation = shell!; shortcut = automation.CreateShortcut(path);
            dynamic link = shortcut; link.TargetPath = executable; link.WorkingDirectory = Path.GetDirectoryName(executable); link.IconLocation = executable + ",0"; link.Description = "Luma · ses, parlaklık ve medya"; link.Save();
        }
        finally { if (shortcut is not null) Marshal.ReleaseComObject(shortcut); if (shell is not null) Marshal.ReleaseComObject(shell); }
    }
    internal const string UninstallScript = """
        $ErrorActionPreference = 'Stop'
        Add-Type -AssemblyName System.Windows.Forms
        $installRoot = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Luma')).TrimEnd('\')
        $scriptRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
        if (-not [string]::Equals($installRoot, $scriptRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Kurulum klasörü doğrulanamadı.' }
        if ((Get-Item -LiteralPath $installRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Kurulum klasörü bağlantı olamaz.' }
        if ([Windows.Forms.MessageBox]::Show('Luma kaldırılsın mı? Kişisel ayarların korunacak.', 'Luma', 'YesNo', 'Question') -ne 'Yes') { exit }
        try { $signal = [Threading.EventWaitHandle]::OpenExisting('Local\Luma.Flyouts.Exit'); $signal.Set() | Out-Null; $signal.Dispose() } catch { }
        Start-Sleep -Milliseconds 800
        $prefix = $installRoot + '\'
        foreach ($process in @(Get-Process -Name Luma -ErrorAction SilentlyContinue)) {
          if ($process.Path -and $process.Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { Stop-Process -Id $process.Id }
        }
        $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
        $startup = (Get-ItemProperty -LiteralPath $runKey -Name LumaFlyouts -ErrorAction SilentlyContinue).LumaFlyouts
        if ($startup -and $startup.StartsWith('"' + $prefix, [StringComparison]::OrdinalIgnoreCase)) { Remove-ItemProperty -LiteralPath $runKey -Name LumaFlyouts }
        $shell = New-Object -ComObject WScript.Shell
        foreach ($shortcutPath in @((Join-Path ([Environment]::GetFolderPath('Programs')) 'Luma.lnk'), (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Luma.lnk'))) {
          if (Test-Path -LiteralPath $shortcutPath) {
            $shortcut = $shell.CreateShortcut($shortcutPath)
            if ($shortcut.TargetPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $shortcutPath }
            [Runtime.InteropServices.Marshal]::ReleaseComObject($shortcut) | Out-Null
          }
        }
        [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
        $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\LumaFlyouts'
        if ((Get-ItemProperty -LiteralPath $uninstallKey -ErrorAction SilentlyContinue).InstallLocation -eq $installRoot) { Remove-Item -LiteralPath $uninstallKey -Recurse }
        Remove-Item -LiteralPath $installRoot -Recurse -Force
        """;
}
