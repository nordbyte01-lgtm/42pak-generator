<p align="center">
  <img src="assets/custom-pak-tool-banner.jpg" alt="42pak-generator" width="100%" />
</p>

# Contributing to 42pak-generator

Thank you for your interest in contributing! This guide will help you get started.

## Getting Started

1. **Fork** the repository
2. **Clone** your fork locally
3. **Install** .NET 8.0 SDK
4. **Build** the solution:
   ```bash
   dotnet restore
   dotnet build
   ```
5. **Run tests** to verify everything works:
   ```bash
   dotnet test
   ```

## Development Setup

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 version 1809+ (for WebView2)
- Visual Studio 2026 or VS Code with C# extension

### Project Layout

- `FortyTwoPak.Core` - Format handling, crypto, compression, legacy reader
- `FortyTwoPak.UI` - Desktop app (WebView2 + WinForms)
- `FortyTwoPak.Tests` - xUnit test suite

## How to Contribute

### Reporting Bugs

- Use GitHub Issues
- Include: steps to reproduce, expected vs actual behavior, .NET version, OS version
- Attach the `syserr.txt` log if relevant

### Suggesting Features

- Open a GitHub Issue with the "enhancement" label
- Describe the use case and why it benefits the Metin2 community

### Submitting Code

1. Create a feature branch from `main`:
   ```bash
   git checkout -b feature/my-improvement
   ```
2. Make your changes
3. Add or update tests for any new functionality
4. Ensure all tests pass: `dotnet test`
5. Commit with a clear message:
   ```
   Add support for VPK v2 entry metadata
   ```
6. Push and open a Pull Request

## Code Style

- Follow standard C# conventions (PascalCase for public members, camelCase for locals)
- Use `var` when the type is obvious from the right side
- Keep methods focused - if a method does more than one thing, split it
- No regions (`#region`) - use classes and files for organization
- Add XML doc comments only on public API methods

## Testing

- Write tests for all new public API methods
- Use descriptive test names: `MethodName_Scenario_ExpectedResult`
- Tests should be independent and not rely on external files
- Use temporary directories for file I/O tests (clean up in teardown)

## Security

- Never commit real passphrases, keys, or credentials
- Encryption-related changes must be reviewed carefully
- Use constant-time comparison for all security-critical byte comparisons
- Wipe key material from memory after use

## Areas Where Help Is Needed

- Locale support (translating UI strings)
- macOS/Linux client loader support
- Performance optimizations for large archives (100k+ files)
- CI/CD pipeline setup
- Additional archive format support beyond VPK

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
