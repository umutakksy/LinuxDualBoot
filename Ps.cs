using System.Diagnostics;
using System.Text;

namespace CachyDualBoot;

static class Ps
{
    public static string Q(string s) => s.Replace("'", "''");

    public static async Task<(int Code, string Output)> RunAsync(string script, Action<string>? onLine = null)
    {
        var full = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[Text.Encoding]::UTF8\n"
                   + script + "\nexit 0";
        var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
        var psi = new ProcessStartInfo("powershell.exe", $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {enc}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        using var p = Process.Start(psi)!;
        var sb = new StringBuilder();
        void Handle(string? l)
        {
            if (l == null) return;
            lock (sb) sb.AppendLine(l);
            onLine?.Invoke(l);
        }
        p.OutputDataReceived += (_, e) => Handle(e.Data);
        p.ErrorDataReceived += (_, e) => Handle(e.Data);
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync();
        return (p.ExitCode, sb.ToString());
    }
}
