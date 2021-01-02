$source = "c:\Users\jachy\Downloads\DNO.exe"
$target = "c:\Windows\DNO.exe"

Start-Sleep -s 3
Move-Item -Path $source -Destination $target -Force
if ($true -ne $?) {
    Start-Process -WindowStyle Hidden -Verb runAs PowerShell -Args "Move-Item -Path $source -Destination $target -Force" -Wait
}
Start-Process $target