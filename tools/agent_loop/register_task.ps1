# PC 루프를 Windows 작업 스케줄러에 등록한다. PC Unity 예약 세션(매일 20:37·22:37·00:37·02:37·04:37, 50분 안)과
# 겹치지 않는 틈에 잡았다. 평일 낮은 회사 일이라 비운다.
#   매일 21:32, 23:32, 01:32, 03:32 / 주말 낮 10:30, 12:30, 14:30, 16:30 (한국 시간, PC 시계 기준)
# 쓰는 법: PowerShell에서  powershell -ExecutionPolicy Bypass -File tools\agent_loop\register_task.ps1
# 지우려면: Unregister-ScheduledTask -TaskName "BloodRain PC 루프 *" -Confirm:$false

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$python = (Get-Command python).Source
$action = New-ScheduledTaskAction -Execute $python -Argument "tools\agent_loop\run_loop.py" -WorkingDirectory $root
# 놓친 예약을 나중에 몰아 돌리지 않는다(StartWhenAvailable 끔). 아침에 PC 를 켰을 때 회사 시간에 Unity 가 뜨는 걸 막는다
$settings = New-ScheduledTaskSettingsSet -DontStopIfGoingOnBatteries -AllowStartIfOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 1)

$weekday = @(
  New-ScheduledTaskTrigger -Daily -At 21:32
  New-ScheduledTaskTrigger -Daily -At 23:32
  New-ScheduledTaskTrigger -Daily -At 01:32
  New-ScheduledTaskTrigger -Daily -At 03:32
)
$weekend = @(
  New-ScheduledTaskTrigger -Weekly -DaysOfWeek Saturday,Sunday -At 10:30
  New-ScheduledTaskTrigger -Weekly -DaysOfWeek Saturday,Sunday -At 12:30
  New-ScheduledTaskTrigger -Weekly -DaysOfWeek Saturday,Sunday -At 14:30
  New-ScheduledTaskTrigger -Weekly -DaysOfWeek Saturday,Sunday -At 16:30
)
Register-ScheduledTask -TaskName "BloodRain PC 루프 밤" -Action $action -Trigger $weekday -Settings $settings -Force | Out-Null
Register-ScheduledTask -TaskName "BloodRain PC 루프 주말" -Action $action -Trigger $weekend -Settings $settings -Force | Out-Null
Unregister-ScheduledTask -TaskName "BloodRain PC 루프 평일" -Confirm:$false -ErrorAction SilentlyContinue
Write-Output "등록 완료: BloodRain PC 루프 밤 / 주말"
