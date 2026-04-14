$ports = @(5001, 5002, 5003, 5004)
$workerCount = 2

start-process "nginx.exe" -WorkingDirectory "C:\nginx\"

docker start valuator-redis

Write-Host "Waiting for Redis to start..." -ForegroundColor Yellow
while ($true) {
    $redisCheck = Test-NetConnection -ComputerName localhost -Port 6379 -InformationLevel Quiet
    if ($redisCheck) { 
        Write-Host "Redis is READY!" -ForegroundColor Green
        break 
    }
    Write-Host "." -NoNewline
    Start-Sleep -Seconds 1
}

Write-Host "Starting RabbitMQ Server..." -ForegroundColor Cyan
start-process "C:\RabbitMQ\rabbitmq_server-4.2.5\sbin\rabbitmq-server.bat"
Write-Host "Waiting for RabbitMQ to start..." -ForegroundColor Yellow
while ($true) {
    $connection = Test-NetConnection -ComputerName localhost -Port 5672 -InformationLevel Quiet
    if ($connection) { 
        Write-Host "RabbitMQ is READY!" -ForegroundColor Green
        break 
    }
    Write-Host "." -NoNewline
    Start-Sleep -Seconds 2
}

for ($i = 1; $i -le $workerCount; $i++) {
    Start-Process dotnet -ArgumentList "run" -WorkingDirectory "$PSScriptRoot\..\RankCalculator"
} 

foreach ($port in $ports) 
{
    Start-Process dotnet -ArgumentList "run --urls=http://localhost:$port" -WorkingDirectory "$PSScriptRoot\..\Valuator"
}
