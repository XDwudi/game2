# Runs a Unity test entry point in a windowed Editor (PlayMode cannot run in -batchmode on this project:
# WaitForEndOfFrame never fires). When the shell is elevated the Editor shows an
# "Administrator Privileges Detected" dialog; this script presses "Ignore Warning" through UI Automation.
# It changes no system or security setting.
param(
    [string]$Method = 'DiceCraft.Editor.V072BatchTests.RunPlayMode',
    [string]$GameView = '1920x1080',
    [string]$Filter = '',
    [Parameter(Mandatory = $true)][string]$Log,
    [string]$Unity = 'E:\unity\6000.6.7f1\Editor\Unity.exe',
    [string]$Project = ''
)
if (-not $Project) { $Project = Join-Path (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)) 'DicePrototype' }
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -Namespace '' -Name UnityDialog -MemberDefinition '[DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);'
$arguments = @('-projectPath', $Project, '-executeMethod', $Method, '-v072GameView', $GameView, '-logFile', $Log)
if ($Filter) { $arguments += @('-v072TestFilter', $Filter) }
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -PassThru
$deadline = (Get-Date).AddMinutes(4); $clicked = $false
$byName = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Ignore Warning')
while (-not $process.HasExited -and -not $clicked -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 800
    try {
        $byProcess = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
        $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $byProcess)
        foreach ($window in $windows) {
            $button = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byName)
            if ($button) {
                # The dialog's buttons are plain Win32 controls that do not expose InvokePattern; BM_CLICK presses them.
                [void][UnityDialog]::SendMessage([IntPtr]$button.Current.NativeWindowHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)
                $clicked = $true; Write-Output 'dismissed: Ignore Warning'; break
            }
        }
    } catch { Write-Output ("dialog probe: " + $_.Exception.Message) }
    if (Test-Path $Log) { if (Select-String -Path $Log -Pattern 'V072_GAMEVIEW|V072_TESTS_FINISHED' -Quiet) { break } }
}
while (-not $process.HasExited) {
    Start-Sleep -Seconds 5
    if ((Test-Path $Log) -and (Select-String -Path $Log -Pattern 'error CS\d+' -Quiet)) {
        Write-Output 'compile errors: stopping the Editor'; Stop-Process -Id $process.Id -Force; break
    }
}
$process.WaitForExit()
Write-Output ("exit=" + $process.ExitCode)
if (Test-Path $Log) { Select-String -Path $Log -Pattern 'V072_TESTS_FINISHED|V072_GAMEVIEW|error CS' | Select-Object -Last 4 | ForEach-Object { $_.Line } }
exit $process.ExitCode
