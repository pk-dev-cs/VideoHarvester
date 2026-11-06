# Update tests

On Windows with the .NET 10 SDK:

```powershell
dotnet run --project tests/VideoHarvester.UpdateTests
```

The tests cover startup and manual checks, unavailable updates, offline operation, failed downloads and installation, deferring updates, unfinished-queue protection, pending updates on relaunch, history migration including WAL data, existing destination protection, and WPF panel layout and command bindings. They use isolated temporary databases and never install an update or modify the user's history.

To verify actual package discovery, download, checksum validation, and equal-version rejection, first create a local release:

```powershell
dotnet tool restore
dotnet publish VideoHarvester/VideoHarvester.csproj -c Release -r win-x64 --self-contained true -o artifacts/update-smoke/publish -p:Version=0.3.1
dotnet vpk pack -u VideoHarvester -v 0.3.1 -p artifacts/update-smoke/publish -e VideoHarvester.exe -r win-x64 -c win -o artifacts/update-smoke/Releases
dotnet run --project tests/VideoHarvester.UpdateTests -- --feed artifacts/update-smoke/Releases
```

The test version stays local; no release is uploaded. The feed check uses Velopack's test locator to simulate an older installation and does not exit or restart the test process.

Optional panel renders for visual inspection:

```powershell
dotnet run --project tests/VideoHarvester.UpdateTests -- --screenshots artifacts/update-smoke/ui
```

For an interactive FlaUI check of the real main window and update dialog with an in-memory history and simulated update service:

```powershell
dotnet run --project tests/VideoHarvester.UpdateTests -- --preview blocked
```

This adds a paused preview item so Update and restart can be checked without installing anything. Later hides the update window; Check for updates or the footer link opens it again. Use `--preview ready` for an empty queue; installation is still simulated and never replaces files.

If a running app locks the normal build output, add `-p:BaseOutputPath=bin/codex-verify/` to the build or run command, before the `--` argument separator.
