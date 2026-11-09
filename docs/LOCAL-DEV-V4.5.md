# Local development changes for v4.5

The local development workflow is now one-click.

## Before

The developer had to manually:

- change the process execution policy;
- install WinDivert;
- build the solution;
- run WinDivert diagnostics;
- launch the .NET DLL/host.

## Now

Use:

```text
Start-SafeWeb.cmd
```

The launcher automatically:

1. requests Administrator elevation;
2. uses a process-only PowerShell execution-policy bypass;
3. installs WinDivert only when the runtime files are missing;
4. restores NuGet packages only when restore assets are missing;
5. builds the x64 Release application;
6. runs the WinDivert diagnostics;
7. starts the compiled SafeWebGateway.exe.

No permanent execution-policy change is made.

## Existing security policy

This change does not disable or weaken Windows Application Control, Smart App Control, VBS, or Code Integrity. An unsigned SafeWeb build can still be blocked by an enforced Windows Application Control policy. That policy authorization is a separate OS-level requirement.
