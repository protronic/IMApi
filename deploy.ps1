param([string]$cmd = "")
$sess = New-PSSession -ComputerName prot-exchange -Credential (Get-Credential);

# restart app without rebuilding / reuploading by running ".\publish.ps1 restart"
if ( $cmd -ne "restart" ){
  dotnet publish -c Release;
  Copy-Item -Recurse -Path "bin/Release/net6.0/publish" -Destination "E:/imapi" -ToSession $sess -Force;
}

# Invoke-Command -Session $sess -ScriptBlock {
#   Get-Process "dotnet" -ErrorAction Ignore | Stop-Process -ErrorAction Ignore
#   Set-Location "E:/imapi/publish/";
#   $env:ASPNETCORE_URLS="http://0.0.0.0:5000";
#   Start-Process -Filepath dotnet -ArgumentList "E:/imapi/publish/IMApi.dll" -RedirectStandardOutput stdout.txt -RedirectStandardError stderr.txt;
# };
