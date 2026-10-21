param([int]$ProcessId, [string]$OutDir)
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public struct RECT { public int L, T, R, B; }
}
"@
[W]::SetProcessDPIAware() | Out-Null
New-Item -ItemType Directory -Force $OutDir | Out-Null
$p = Get-Process -Id $ProcessId
$h = $p.MainWindowHandle

function Snap([string]$name) {
	[W]::SetForegroundWindow($h) | Out-Null
	Start-Sleep -Milliseconds 800
	$r = New-Object W+RECT
	[W]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) | Out-Null
	$w = $r.R - $r.L; $ht = $r.B - $r.T
	$bmp = New-Object System.Drawing.Bitmap $w, $ht
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
	$bmp.Save((Join-Path $OutDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
	$g.Dispose(); $bmp.Dispose()
	"$name ${w}x${ht}"
}

[W]::ShowWindow($h, 3) | Out-Null
Start-Sleep 2
Snap "main-window.png"
[System.Windows.Forms.SendKeys]::SendWait("{F5}")
Start-Sleep 6
Snap "query-results.png"
