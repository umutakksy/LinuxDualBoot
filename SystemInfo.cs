using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CachyDualBoot;

record SysInfo(bool Uefi, bool? SecureBoot, int DiskNumber, string PartStyle,
               long CSize, long CMin, long CMax, double CFreeGb, string BitLocker)
{
    public bool BitLockerActive => !BitLocker.Contains("FullyDecrypted", StringComparison.OrdinalIgnoreCase)
                                   && !BitLocker.StartsWith("unknown", StringComparison.OrdinalIgnoreCase);
}

static class SystemInfo
{
    [DllImport("kernel32.dll")]
    static extern bool GetFirmwareType(out uint type);

    public static async Task<SysInfo> GatherAsync()
    {
        bool uefi = GetFirmwareType(out var t) && t == 2;

        bool? sb = null;
        using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
            if (k?.GetValue("UEFISecureBootEnabled") is int v) sb = v == 1;

        var (code, output) = await Ps.RunAsync("""
            $p = Get-Partition -DriveLetter C
            $d = Get-Disk -Number $p.DiskNumber
            $s = Get-PartitionSupportedSize -DriveLetter C
            $bl = 'unknown'
            try { $v = Get-BitLockerVolume -MountPoint C:; $bl = "$($v.ProtectionStatus)/$($v.VolumeStatus)" } catch {}
            "INFO|$($d.Number)|$($d.PartitionStyle)|$($p.Size)|$($s.SizeMin)|$($s.SizeMax)|$bl"
            """);
        var line = output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith("INFO|"));
        if (code != 0 || line == null)
            throw new InvalidOperationException("Disk bilgisi okunamadı:\n" + output);
        var f = line.Split('|');
        var free = new DriveInfo("C").AvailableFreeSpace / 1073741824.0;
        return new SysInfo(uefi, sb, int.Parse(f[1]), f[2], long.Parse(f[3]), long.Parse(f[4]), long.Parse(f[5]), free, f[6]);
    }
}
