$ports = @(5001, 5002, 5003, 5004)
$workerCount = 2

start-process "nginx.exe" -WorkingDirectory "C:\nginx\"

docker start db-main db-ru db-eu db-asia

Start-Sleep -Seconds 2

Write-Host "Setting passwords for Redis containers..." -ForegroundColor Yellow
$redisContainers = @("db-main", "db-ru", "db-eu", "db-asia")
foreach ($container in $redisContainers) {
    docker exec $container redis-cli CONFIG SET requirepass redis_admin
}

Write-Host "Waiting for Redis to start..." -ForegroundColor Yellow
while ($true) {
    $redisCheck = Test-NetConnection -ComputerName localhost -Port 6000 -InformationLevel Quiet
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
        $ctl = "C:\RabbitMQ\rabbitmq_server-4.2.5\sbin\rabbitmqctl.bat"

        Write-Host "Configuring RabbitMQ credentials..." -ForegroundColor Yellow
    
        & $ctl add_user "rabbit_admin" "rabbit_admin" 2>$null
        & $ctl set_user_tags "rabbit_admin" administrator
        & $ctl set_permissions -p "/" "rabbit_admin" ".*" ".*" ".*"
        break 
}
    Write-Host "." -NoNewline
    Start-Sleep -Seconds 2
}

for ($i = 1; $i -le $workerCount; $i++) {
    Start-Process dotnet -ArgumentList "run" -WorkingDirectory "$PSScriptRoot\..\RankCalculator"
}


$loggerCount = 2
for ($i = 1; $i -le $loggerCount; $i++) {
    Write-Host "Starting EventsLogger Instance $i..." -ForegroundColor Magenta
    Start-Process dotnet -ArgumentList "run" -WorkingDirectory "$PSScriptRoot\..\EventsLogger"

}

foreach ($port in $ports) 
{
    Start-Process dotnet -ArgumentList "run --urls=http://localhost:$port" -WorkingDirectory "$PSScriptRoot\..\Valuator"
}
