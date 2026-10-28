# PC 루프를 Windows 작업 스케줄러에 등록한다. 클라우드 예약 세션과 겹치지 않는 시각으로 잡았다.
#   평일 01:00, 05:00 / 주말 12:30, 18:30 (한국 시간, PC 시계 기준)
# 쓰는 법: PowerShell에서  powershell -ExecutionPolicy Bypass -File tools\agent_loop\register_task.ps1
# 지우려면: Unregister-ScheduledTask -TaskName "BloodRain PC 루프 *" -Confirm:$false

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$python = (Get-Command python).Source
$action = New-ScheduledTaskAction -Execute $python -Argument "tools\agent_loop\run_loop.py" -WorkingDirectory $root
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 1)

$weekday = @(
  New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday,Tuesday,Wednesday,Thursday,Friday -At 01:00
  New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday,Tuesday,Wednesday,Thursday,Friday -At 05:00
)
$weekend = @(
  New-ScheduledTaskTrigger -Weekly -DaysOfWeek Saturday,Sunday -At 12:30
  New-ScheduledTaskTrigger -Weekly -DaysOfWeek Saturday,Sunday -At 18:30
)
Register-ScheduledTask -TaskName "BloodRain PC 루프 평일" -Action $action -Trigger $weekday -Settings $settings -Force | Out-Null
Register-ScheduledTask -TaskName "BloodRain PC 루프 주말" -Action $action -Trigger $weekend -Settings $settings -Force | Out-Null
Write-Output "등록 완료: BloodRain PC 루프 평일 / 주말"
