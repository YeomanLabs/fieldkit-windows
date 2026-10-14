# Building and contributing

FieldKit for Windows is a C# / .NET 10 app with a WinUI 3 interface. It's developed alongside the FieldKit iPhone app in a private repository. **This repo gets a snapshot of the Windows source with every release**, so the code here always matches the latest release. Issues and pull requests are welcome here. An accepted change is applied upstream and comes back here with the next release.

## Build, test and run

You need Windows 10 (2004 or later) or Windows 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download) (`winget install Microsoft.DotNet.SDK.10`). Visual Studio is optional. Run these from the repo root:

```powershell
dotnet test tests\FieldKit.Core.Tests                     # all the logic, no UI needed
dotnet build FieldKit.sln -c Debug -p:Platform=x64        # or -p:Platform=ARM64
pwsh tools\Start-FieldKit.ps1 -Demo                       # build and run with sample data
pwsh tools\Build-Release.ps1                              # MSI and zip for x64 and ARM64, in artifacts\
```

- **Demo mode:** `FieldKit.exe --demo` opens on **Enter demo**, with a made-up Contoso tenant and nothing sent to Microsoft. Its serials:
  - `5CG1234XYZ` is healthy;
  - `PF2ABCDE` has duplicate records, a stale Entra device and a disabled one;
  - `PF9NEW01` is in Autopilot only;
  - `DENIED01` gets access denied.
- **Your own tenant:** register the app as described in the [README](README.md#set-up-once-per-tenant). You enter the tenant and client IDs on first launch.
- **Separate data:** `-DataFolder <folder>` keeps a Debug build's local data apart from an installed FieldKit's (`%LOCALAPPDATA%\FieldKit`). `-Theme dark` or `-Theme light` overrides the Windows theme.
- **XAML errors:** a XAML mistake fails the build with an unhelpful `WMC9999` error. Run `powershell -NoProfile -File tools\Show-XamlErrors.ps1` to see the real error, with file and line.

## Layout

```
src/FieldKit.Core/     plain .NET: no WinUI, so dotnet test covers it
  Domain/              types and rules: flags, serials, compliance, sign-in decoder, install status
  Graph/               Graph client, requests, DTOs (source-generated JSON), lookups and actions
  Auth/                tenant config, permissions (GraphPermission.All), auth errors
  Persistence/         SQLite store: settings, recent lookups, site walks
  ViewModels/          one per page or dialog (CommunityToolkit.Mvvm)
  Demo/                the demo tenant (DemoGraphClient), served without any network
src/FieldKit.App/      WinUI 3 app: pages, controls, MSAL + Windows broker sign-in, Windows Hello
tests/                 xUnit tests; Fixtures/ holds Graph responses
installer/             WiX MSI
tools/                 run, release and XAML error scripts
```

## Ground rules

These keep FieldKit safe to point at a production tenant. A change that needs to bend one should start as an issue.

- **Graph:** use `HttpClient` and `System.Text.Json`, with no Graph SDK. Read through `GraphClientExtensions`. Use v1.0, and beta only where Microsoft offers nothing else; a beta-backed section has to load and fail on its own. Every response shape you decode gets a fixture in `tests/FieldKit.Core.Tests/Fixtures`.
- **Writes are a fixed list:**
  - Autopilot group tag and assigned user;
  - sync, restart, retire and run remediation;
  - group membership for app installs;
  - an Entra device's extension attributes.

  A test pins the write permissions in `GraphPermission.All`. New write calls or permissions need an issue first. Intune reports are read with POST through `PostReportAsync`, which can only reach `deviceManagement/reports/`.
- **Secrets:**
  - LAPS passwords and BitLocker keys are never logged, stored or put in a URL. They're shown only in the secrets dialog and copied only through `ISecureClipboard`.
  - Restart, retire, remediations and recovery secrets ask for Windows Hello first.
  - Never log tokens. Release builds log nothing that identifies a person or device.
  - FieldKit is a public client: never add a client secret or certificate.
- **Errors** tell the engineer what to check (the missing permission or role), not a raw HTTP code.
- **Structure:**
  - Views never call Graph. Pages bind to a view model in Core with `x:Bind`; there's no `{Binding}` and no converters.
  - View models don't use `ConfigureAwait(false)`.
  - One type per file.
- **Builds:** nullable reference types are on and warnings are errors. Package versions are pinned in `Directory.Packages.props`; ask before adding a package.
- **Test data:** use made-up Contoso names, never real tenant data. That covers fixtures, tests, demo data and screenshots.

## Security problems

Please don't post details of a vulnerability in a public issue. Open an issue that just says you have one, and we'll arrange a private way to send it.

## License

By contributing, you agree that your contribution is licensed under the [MIT license](LICENSE), like the rest of FieldKit for Windows.
