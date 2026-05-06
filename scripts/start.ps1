$env:DB_MAIN = "localhost:6000"
$env:DB_RU = "localhost:6001"
$env:DB_EU = "localhost:6002"
$env:DB_ASIA = "localhost:6003"

$env:REDIS_PASSWORD = "redis_admin"
$env:RABBIT_USER = "rabbit_admin"
$env:RABBIT_PASSWORD = "rabbit_admin"

$ports = @(5001, 5002, 5003, 5004)
$workerCount = 2

start-process "nginx.exe" -WorkingDirectory "C:\nginx\"

docker start db-main db-ru db-eu db-asia

Start-Sleep -Seconds 2

$redisContainers = @("db-main", "db-ru", "db-eu", "db-asia")
foreach ($container in $redisContainers) {
    docker exec $container redis-cli CONFIG SET requirepass redis_admin
}

while ($true) {
    $redisCheck = Test-NetConnection -ComputerName localhost -Port 6000 -InformationLevel Quiet
    if ($redisCheck) { 
        break 
    }
    Start-Sleep -Seconds 1
}

start-process "C:\RabbitMQ\rabbitmq_server-4.2.5\sbin\rabbitmq-server.bat"
while ($true) {
    $connection = Test-NetConnection -ComputerName localhost -Port 5672 -InformationLevel Quiet
    if ($connection) { 
        $ctl = "C:\RabbitMQ\rabbitmq_server-4.2.5\sbin\rabbitmqctl.bat"
    
        & $ctl add_user "rabbit_admin" "rabbit_admin" 2>$null
        & $ctl set_user_tags "rabbit_admin" administrator
        & $ctl set_permissions -p "/" "rabbit_admin" ".*" ".*" ".*"
        break 
}
    Start-Sleep -Seconds 2
}

for ($i = 1; $i -le $workerCount; $i++) {
    Start-Process dotnet -ArgumentList "run --verbosity quiet" -WorkingDirectory "$PSScriptRoot\..\RankCalculator"
}


$loggerCount = 2
for ($i = 1; $i -le $loggerCount; $i++) {
    Start-Process dotnet -ArgumentList "run --verbosity quiet" -WorkingDirectory "$PSScriptRoot\..\EventsLogger"

}

foreach ($port in $ports) 
{
    Start-Process dotnet -ArgumentList "run --urls=http://localhost:$port --verbosity quiet /p:Nullable=disable /p:WarningLevel=0" -WorkingDirectory "$PSScriptRoot\..\Valuator"
}
