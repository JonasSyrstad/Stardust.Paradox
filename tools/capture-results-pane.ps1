param([int]$ProcessId, [string]$OutFile)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W2 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[W2]::SetProcessDPIAware() | Out-Null
$p = Get-Process -Id $ProcessId
[W2]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 800

$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$cond = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::AutomationIdProperty), "ResultDataGrid"
$grid = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
if (-not $grid) { throw "ResultDataGrid not found" }

# Walk up to the enclosing results tab control so the result tabs are included
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$pane = $grid
$cur = $walker.GetParent($grid)
while ($cur -and $cur -ne $root) {
	if ($cur.Current.ControlType -eq [System.Windows.Automation.ControlType]::Tab) { $pane = $cur; break }
	$cur = $walker.GetParent($cur)
}

$r = $pane.Current.BoundingRectangle
"Pane: $($pane.Current.ControlType.ProgrammaticName) $r"
$bmp = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
