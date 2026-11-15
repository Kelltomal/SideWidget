# Windows harness

Из корня репозитория:

```powershell
./scripts/test-windows.ps1
```

Проверки только читают состояние Windows. Публичные HTTPS/ICMP-запросы включаются отдельно: `./scripts/test-windows.ps1 -PublicOnline`.

Требования, ограничения, SKIP и ручные сценарии: [WINDOWS_TESTS.md](../../docs/WINDOWS_TESTS.md).
