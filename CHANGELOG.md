# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial release of Flap, a clap-style fluent CLI parser for F#: `Arg` and `Command` builders, `Matches` with typed getters, help/version output, did-you-mean suggestions and shell completions (Bash, Zsh, Fish, PowerShell, Nushell).
- Native AOT (reflection-free) and Fable compatibility, with samples.
- `Matches.getWith`, `getInt`, `getBool` and `getFloat`: required typed getters returning `Result<'T, string>`.
- `Arg.allowHyphenValues`: options and positionals may take values starting with `-`.
- `Arg.trailingVarArg`: the last multi-value positional takes every remaining token verbatim (for exec-style wrappers like `tool run cmd --flag`). `Command.check` rejects it on any other arg.
- Help customization: `Command.longAbout`, `Command.beforeHelp`, `Command.afterHelp` and `Arg.helpHeading`.
- Value-aware shell completions: possible values complete for options and positionals, and the new `Arg.valueHint` (`ValueHint`: `AnyPath`, `FilePath`, `DirPath`, `Other`) completes file system paths.
- Nushell completions (`Completions.generate Nushell`), generated as `extern` definitions with `@` completers.
- `mise.toml` pinning nushell, powershell and fish for smoke-testing the generated completion scripts.

### Changed

- **Breaking:** `Matches.tryInt`, `tryBool` and `tryFloat` now return `Result<'T option, string>` instead of silently turning an invalid value into `None`. Match on the `Result`, or use the new `getInt`/`getBool`/`getFloat`.
- Completions now include global args inherited by subcommands, and PowerShell completion handles subcommand aliases.

### Fixed

- The missing-subcommand error no longer wraps its message in stray quotes.
- Environment variable values for flag args (`SetTrue`/`SetFalse`) are parsed as booleans, so `MYAPP_DEBUG=1` now reads as true.
- An invalid environment variable value is now reported as an `InvalidValue` error naming the variable, instead of being silently dropped.
- `Arg.numArgs` minimum is now enforced for positional arguments (`TooFewValues`).
