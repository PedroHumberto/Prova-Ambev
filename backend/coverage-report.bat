@ECHO OFF

REM Install tools if not present
dotnet tool install --global dotnet-coverage
if errorlevel 1 exit /b %errorlevel%
dotnet tool install --global dotnet-reportgenerator-globaltool
if errorlevel 1 exit /b %errorlevel%

REM Clean and build solution
dotnet restore Ambev.DeveloperEvaluation.sln
if errorlevel 1 exit /b %errorlevel%
dotnet build Ambev.DeveloperEvaluation.sln --configuration Release --no-restore
if errorlevel 1 exit /b %errorlevel%

REM Run tests with coverage
if exist TestResults rmdir /s /q TestResults
for /d %%D in (tests\*\TestResults) do rmdir /s /q "%%D"

dotnet-coverage collect "dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj --configuration Release --no-build --no-restore --verbosity normal" ^
--settings coverage.runsettings ^
--output tests/Ambev.DeveloperEvaluation.Unit/TestResults/coverage.cobertura.xml ^
--output-format cobertura
if errorlevel 1 exit /b %errorlevel%

dotnet-coverage collect "dotnet test tests/Ambev.DeveloperEvaluation.Integration/Ambev.DeveloperEvaluation.Integration.csproj --configuration Release --no-build --no-restore --verbosity normal" ^
--settings coverage.runsettings ^
--output tests/Ambev.DeveloperEvaluation.Integration/TestResults/coverage.cobertura.xml ^
--output-format cobertura
if errorlevel 1 exit /b %errorlevel%

dotnet-coverage collect "dotnet test tests/Ambev.DeveloperEvaluation.Functional/Ambev.DeveloperEvaluation.Functional.csproj --configuration Release --no-build --no-restore --verbosity normal" ^
--settings coverage.runsettings ^
--output tests/Ambev.DeveloperEvaluation.Functional/TestResults/coverage.cobertura.xml ^
--output-format cobertura
if errorlevel 1 exit /b %errorlevel%

REM Generate coverage report
reportgenerator ^
-reports:"./tests/**/TestResults/coverage.cobertura.xml" ^
-targetdir:"./TestResults/CoverageReport" ^
-reporttypes:Html
if errorlevel 1 exit /b %errorlevel%

REM Removing temporary files
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul

echo.
echo Coverage report generated at TestResults/CoverageReport/index.html
