$ports = @(5001, 5002, 5003, 5004)

start-process "nginx.exe" -WorkingDirectory "C:\nginx\"


docker start valuator-redis


foreach ($port in $ports) 
{
    Start-Process dotnet -ArgumentList "run --urls=http://localhost:$port" -WorkingDirectory "$PSScriptRoot\..\Valuator"
}