# Design

Flap is a clap-style parser for F#. This page explains the main choices behind it.

## Why a builder API, not attributes

Parsers such as clap's derive API, or F# libraries built on attributes and discriminated unions, rely on reflection to read the shape of your types. Reflection is the one thing Native AOT and Fable cannot do well.
Flap describes a command as plain data (records built by piping functions), so the parser walks values it already holds. There is nothing to discover at run time.

This also explains the `--reflectionfree` requirement: F# generates reflection-based `ToString` for `%A`, so the library avoids `%A` and asks AOT apps to compile reflection-free.

## Why a pure core

`Command.tryParse` and `tryParseWith` return `Result<Matches, ParseError>` and never touch the console, the environment or the process. Console I/O lives only in `Command.parse`.
This keeps the core testable, makes it work under Fable (where there is no portable console or environment), and lets you decide how to present errors. `parseWith` sits in between, taking output and exit callbacks.

## Why `Matches` and explicit parsers

A parse result stores strings. You convert with explicit parsers (`Value.int`, `Value.oneOf`, ...) rather than a generic `get<'T>`, because generic conversion needs reflection or statically resolved type machinery that does not survive AOT/Fable.
Typed getters return `Result` so a bad value is a value you handle, not an exception.

## Why follow clap

Flap copies clap's vocabulary (`Arg`, `Command`, `conflictsWith`, `requires`, groups, external subcommands) and conventions (exit code 2 for usage errors, did-you-mean suggestions, help layout). People who know clap know what to expect, and the behaviour has been worked out by a mature tool.

## Why completions are generated from the definition

Because the command is data, `Completions.generate` can derive scripts for every shell from the same tree the parser uses. This keeps completions in step with the parser, including per-subcommand scoping of global args and possible values.
