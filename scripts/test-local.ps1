$ErrorActionPreference = 'Stop'
Invoke-RestMethod http://127.0.0.1:8787/api/status | ConvertTo-Json -Depth 5
Invoke-RestMethod http://127.0.0.1:8787/api/rules | ConvertTo-Json -Depth 5
Invoke-RestMethod http://127.0.0.1:8787/api/network | ConvertTo-Json -Depth 5
