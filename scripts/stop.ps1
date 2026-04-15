$ports = @(5001, 5002, 5003, 5004)

foreach ($port in $ports) 
{
    $procId = (Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue).OwningProcess
    
    if ($procId) 
    {
        Write-Host "Closing process on port $port (PID: $procId)..." -ForegroundColor Yellow
        Stop-Process -Id $procId -Force
    } else {
        Write-Host "Port $port is already free." -ForegroundColor Gray
    }
}

$rankProcs = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like "*RankCalculator*" }
foreach ($p in $rankProcs) {
    Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
    Write-Host "Stopped RankCalculator process (PID: $($p.ProcessId))" -ForegroundColor Gray
}

$loggerProcs = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like "*EventsLogger*" }
foreach ($l in $loggerProcs) {
    Stop-Process -Id $l.ProcessId -Force -ErrorAction SilentlyContinue
    Write-Host "Stopped EventsLogger process (PID: $($l.ProcessId))" -ForegroundColor Gray
}

docker stop valuator-redis


start-process "nginx.exe" "-s stop" -WorkingDirectory "C:\nginx\"
& "C:\RabbitMQ\rabbitmq_server-4.2.5\sbin\rabbitmqctl.bat" stop