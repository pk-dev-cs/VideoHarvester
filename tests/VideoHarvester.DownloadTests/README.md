# Download tests

Run these isolated HTTP transfer tests with the .NET 10 SDK:

```powershell
dotnet run --project tests/VideoHarvester.DownloadTests/VideoHarvester.DownloadTests.csproj
```

The executable uses simulated HTTP responses and temporary files without external packages or network access. It checks fresh downloads, cancellation and resuming with ETag or Last-Modified, ignored ranges, incorrect offsets, changed validators, missing validators, truncated responses, and finalization after cancellation at the end of a download. Failures exit with an exception and a nonzero exit code.
