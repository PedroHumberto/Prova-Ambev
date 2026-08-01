#!/bin/bash

set -euo pipefail

echo "Install tools if not present"
dotnet tool install --global dotnet-coverage
dotnet tool install --global dotnet-reportgenerator-globaltool

echo "Clean and build solution"
dotnet restore Ambev.DeveloperEvaluation.sln
dotnet build  Ambev.DeveloperEvaluation.sln --configuration Release --no-restore

echo "Run tests with coverage"
rm -rf ./TestResults ./tests/*/TestResults

dotnet-coverage collect \
"dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj --configuration Release --no-build --no-restore --verbosity normal" \
--settings coverage.runsettings \
--output tests/Ambev.DeveloperEvaluation.Unit/TestResults/coverage.cobertura.xml \
--output-format cobertura

dotnet-coverage collect \
"dotnet test tests/Ambev.DeveloperEvaluation.Integration/Ambev.DeveloperEvaluation.Integration.csproj --configuration Release --no-build --no-restore --verbosity normal" \
--settings coverage.runsettings \
--output tests/Ambev.DeveloperEvaluation.Integration/TestResults/coverage.cobertura.xml \
--output-format cobertura

dotnet-coverage collect \
"dotnet test tests/Ambev.DeveloperEvaluation.Functional/Ambev.DeveloperEvaluation.Functional.csproj --configuration Release --no-build --no-restore --verbosity normal" \
--settings coverage.runsettings \
--output tests/Ambev.DeveloperEvaluation.Functional/TestResults/coverage.cobertura.xml \
--output-format cobertura

echo "Generate coverage report"
reportgenerator \
-reports:"./tests/**/TestResults/coverage.cobertura.xml" \
-targetdir:"./TestResults/CoverageReport" \
-reporttypes:Html

echo "Removing temporary files"
rm -rf bin obj

echo ""
echo "Coverage report generated at TestResults/CoverageReport/index.html"
