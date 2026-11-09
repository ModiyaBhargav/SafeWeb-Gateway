# SafeWeb Gateway — one-click local start

You no longer need to manually run the setup/build/test commands every time.

## First run / every run

Double-click:

```text
Start-SafeWeb.cmd
```

The launcher automatically:

1. Starts PowerShell as Administrator when needed.
2. Uses a process-only execution-policy bypass; it does not change your Windows execution policy permanently.
3. Installs WinDivert only if `native\WinDivert.dll` or `native\WinDivert64.sys` is missing.
4. Restores NuGet packages only when the project's restore assets are missing.
5. Builds the x64 Release build.
6. Runs the WinDivert filter diagnostics.
7. Starts the compiled `SafeWebGateway.exe`.
8. Opens the dashboard at `http://127.0.0.1:8787` once the application is running.

## Equivalent PowerShell command

If you prefer PowerShell, this is now enough:

```powershell
.\scripts\run-dev.ps1
```

It will elevate itself automatically if required.

## Useful optional switches

Skip the WinDivert installation check:

```powershell
.\scripts\run-dev.ps1 -SkipWinDivertInstall
```

Skip the build:

```powershell
.\scripts\run-dev.ps1 -SkipBuild
```

Skip the WinDivert diagnostic test:

```powershell
.\scripts\run-dev.ps1 -SkipWinDivertTest
```

These are optional; normal use should simply be `Start-SafeWeb.cmd`.

## Important: Windows Application Control

The launcher does not disable Smart App Control, VBS, Code Integrity, or any Windows security policy.

If Windows blocks the compiled SafeWeb binary with `0x800711C7`, the launcher will report that the development build needs to be authorized by the existing Windows Application Control policy. That is a Windows policy issue, not a SafeWeb runtime exception.
