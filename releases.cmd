dotnet restore
dotnet publish --configuration Release /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary --runtime linux-x64 --output bin/release/netcoreapp3.1/publish --self-contained
dotnet publish --configuration Release /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary --runtime osx-x64 --output bin/release/netcoreapp3.1/publish --self-contained
dotnet publish --configuration Release /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary --runtime win-x64 --output bin/release/netcoreapp3.1/publish --self-contained
