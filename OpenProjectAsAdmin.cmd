@echo off
powershell -Command "Start-Process 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\devenv.exe' -Verb runAs -ArgumentList VirtualSpaceBackend.sln"