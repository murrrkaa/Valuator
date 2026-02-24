set VALUATOR_PATH=C:\Labs\RP\project\DISTRIBUTED-PROGRAMMING\Valuator
set NGINX_PATH=C:\nginx

start "" dotnet run --project "%VALUATOR_PATH%" --urls http://localhost:5001
start "" dotnet run --project "%VALUATOR_PATH%" --urls http://localhost:5002
start "" dotnet run --project "%VALUATOR_PATH%" --urls http://localhost:5003
start "" dotnet run --project "%VALUATOR_PATH%" --urls http://localhost:5004

cd /d "%NGINX_PATH%"
start "" nginx.exe