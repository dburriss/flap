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

## Why runtime completion is hybrid

A script can only list values it knew when it was generated. Values that change (ids, project names) have to come from the running program, so an arg can carry a completer and the script then calls back into the program for that arg.

Only those args call back. Everything else (subcommands, flags, fixed values, paths) stays in the generated script, because launching a process on every Tab press is slow, especially for apps that are not compiled ahead of time. An app with no completers gets the same static script as before.

The call-back is signalled with an environment variable named after the app (`MY_TOOL_COMPLETE`) rather than a shared name. A process inherits its parent's environment, so a shared name set while completing one Flap app would also switch any Flap app it starts into completion mode.

Computing candidates is a pure function of the command tree and the words typed so far (`Complete.candidates`). Only reading the variable and printing happen at the edge, in `Command.parse`, which matches the rule that console I/O stays out of the parsing core and keeps the code Native AOT and Fable friendly.
