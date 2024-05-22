rd /S /Q bin obj
dotnet publish -r win-x64
copy ..\connectionString.txt bin\Release\net8.0-windows\win-x64\publish
pushd bin\Release\net8.0-windows\win-x64
scp -r publish user@10.0.0.12:.
popd
ssh user@10.0.0.12