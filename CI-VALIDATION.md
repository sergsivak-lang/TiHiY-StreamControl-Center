# CI validation checkpoint

This checkpoint intentionally triggers a clean Windows validation after restoration of the complete WPF source tree and the CyberAmber resource dictionary.

Required gates:

- WPF source tree verification;
- .NET 9 restore and build;
- self-contained Windows x64 publish;
- real application startup in CI screenshot mode;
- screenshot and startup-log artifact upload.
