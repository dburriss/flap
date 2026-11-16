# How to customize help output

## Longer text for `--help` only

`Command.about` shows in both `-h` and `--help`. Use `Command.longAbout` for text shown by `--help` only:

```fsharp
Command.create "myapp"
|> Command.about "Does things"
|> Command.longAbout "Does things, in much more detail than fits in -h."
```

## Add examples or notes around the help

```fsharp
|> Command.beforeHelp "Note: requires a config file."
|> Command.afterHelp "Examples:\n  myapp build --release"
```

`beforeHelp` prints above the help sections, `afterHelp` below.

## Group options under your own heading

```fsharp
Arg.create "token" |> Arg.long "token" |> Arg.helpHeading "Auth"
```

Options with the same heading appear together under `Auth:` instead of `Options:`.

## Hide things

`Arg.hidden` and `Command.hidden` omit an argument or subcommand from help (and from completions).
