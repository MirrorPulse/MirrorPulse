# Contributing to MirrorPulse

Thank you for contributing. MirrorPulse is a Windows-first Cloud Files application with independently packaged Adapter workers.

## Language

Use English for tracked source code, configuration, formal documentation, commit messages, issues, and pull requests. Chinese is reserved for the ignored `draft/` collaboration notes.

## Before opening a pull request

1. Keep one logical behavior or fix in each commit.
2. Add or update tests for behavior changes.
3. Run the smallest relevant validation, followed by the full solution build when practical.
4. Do not commit credentials, tokens, certificates, private paths, or user data.
5. Describe Cloud Files, Adapter protocol, configuration, migration, and release impact.

## Local validation

```powershell
pwsh ./eng/verify-build.ps1
```

The current build uses the .NET SDK selected by `global.json`. Windows Cloud Files integration requires a supported Windows environment.

## Adapter changes

Adapter workers must use the versioned Adapter Host Protocol and remain outside the MP host process. Update the contract tests and package manifest when changing capabilities, roots, installation policy, or protocol behavior.

## Pull requests

Keep pull requests reviewable. Include the verification command and explain any known limitation or CfSharp preview issue. Release, signing, and Microsoft Store changes require explicit evidence in the pull request.
