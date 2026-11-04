# Local development helpers.
#   .\start.ps1                  run with hot reload (dotnet watch)
#   .\start.ps1 test             run the tests
#   .\start.ps1 migration <Name> add an EF Core migration after changing the model
# The database (data/imapi.db) is created and migrated automatically on startup.
param([string]$cmd = "watch", [string]$name = "")

if ($cmd -eq "watch") {
  dotnet watch
}
if ($cmd -eq "test") {
  dotnet test --project tests/IMApi.Tests
}
if ($cmd -eq "migration") {
  if (-not $name) { throw "Usage: .\start.ps1 migration <Name>" }
  dotnet ef migrations add $name
}
