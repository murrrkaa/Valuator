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

docker stop valuator-redis


start-process "nginx.exe" "-s stop" -WorkingDirectory "C:\nginx\"