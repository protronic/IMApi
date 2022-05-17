#Must be the first statement in your script (not coutning comments)
param([string]$cmd = "watch") 

if ($cmd -eq "update" || $cmd -eq "upgrade") {
  if (Test-Path .\wwwroot\CeckedFileInfo.db) { 
    Remove-Item -r -force .\wwwroot\CeckedFileInfo.db;
  }  
  Remove-Item -r -force .\wwwroot\images\out\*;
}

if ($cmd -eq "upgrade") {
  dotnet ef migrations remove --force;
  dotnet ef migrations add InitialCreate;
  dotnet ef database update;
}
if ($cmd -eq "update") {
  dotnet ef database update;
}

dotnet watch;