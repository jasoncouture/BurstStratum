@echo off
dotnet publish -r win7-x64 --self-contained -c Release
dotnet publish -r win7-x86 --self-contained -c Release
dotnet publish -r win8-x64 --self-contained -c Release
dotnet publish -r win8-x86 --self-contained -c Release
dotnet publish -r win8-arm --self-contained -c Release
dotnet publish -r win81-x64 --self-contained -c Release
dotnet publish -r win81-x86 --self-contained -c Release
dotnet publish -r win81-arm --self-contained -c Release
dotnet publish -r win10-x64 --self-contained -c Release
dotnet publish -r win10-x86 --self-contained -c Release
dotnet publish -r win10-arm --self-contained -c Release
dotnet publish -r linux-x64 --self-contained -c Release
dotnet publish -r linux-arm --self-contained -c Release