namespace Flap

module Help =

    let private builtinHelp (long: bool) : Arg =
        { Arg.create "help" with
            Short = Some 'h'
            Long = Some "help"
            Action = Help
            MinValues = 0
            MaxValues = 0
            HelpText = Some(if long then "Print help (see a summary with '-h')" else "Print help (see more with '--help')") }

    let private builtinVersion : Arg =
        { Arg.create "version" with
            Short = Some 'V'
            Long = Some "version"
            Action = Version
            MinValues = 0
            MaxValues = 0
            HelpText = Some "Print version" }

    /// Built-in help/version flags for a command.
    let builtins (cmd: Command) : Arg list =
        (if cmd.HelpFlag then [ builtinHelp false ] else [])
        @ (if cmd.VersionFlag && cmd.VersionText.IsSome then [ builtinVersion ] else [])

    let valueNameOf (a: Arg) =
        match a.ValueName with
        | Some n -> n
        | None -> a.Id.ToUpperInvariant()

    /// Display name of an arg in messages, e.g. `--name <NAME>` or `<FILE>`.
    let argDisplay (a: Arg) : string =
        match a.Index, a.Long, a.Short with
        | Some _, _, _ -> "<" + valueNameOf a + ">"
        | None, Some l, _ -> "--" + l
        | None, None, Some s -> "-" + string s
        | _ -> a.Id

    let private argLabel (a: Arg) : string =
        match a.Index with
        | Some _ -> "<" + valueNameOf a + (if a.MaxValues > 1 then ">..." else ">")
        | None ->
            let names =
                match a.Short, a.Long with
                | Some s, Some l -> "-" + string s + ", --" + l
                | Some s, None -> "-" + string s
                | None, Some l -> "    --" + l
                | None, None -> a.Id
            if Arg.takesValue a then names + " <" + valueNameOf a + ">" else names

    /// `Usage: app [OPTIONS] <ARG> [COMMAND]`
    let usage (path: string) (cmd: Command) : string =
        let hasOpts = (builtins cmd @ cmd.Args) |> List.exists (fun a -> a.Index.IsNone && not a.Hidden)
        let positionals =
            cmd.Args
            |> List.filter (fun a -> a.Index.IsSome)
            |> List.sortBy (fun a -> a.Index.Value)
            |> List.map (fun a ->
                let n = "<" + valueNameOf a + (if a.MaxValues > 1 then ">..." else ">")
                if a.Required then n else "[" + n.TrimEnd('.') + "]" + (if a.MaxValues > 1 then "..." else ""))
        let sub =
            if cmd.Subcommands.IsEmpty then []
            elif cmd.SubcommandRequired then [ "<COMMAND>" ]
            else [ "[COMMAND]" ]
        let parts = [ path ] @ (if hasOpts then [ "[OPTIONS]" ] else []) @ positionals @ sub
        "Usage: " + String.concat " " parts

    let private table (rows: (string * string) list) : string list =
        let w = if rows.IsEmpty then 0 else rows |> List.map (fun (l, _) -> l.Length) |> List.max
        rows
        |> List.map (fun (l, h) -> if h = "" then "  " + l else "  " + l.PadRight w + "  " + h)

    let private annotate (a: Arg) : string =
        let h = defaultArg a.HelpText ""
        let extra =
            (match a.Default with
             | Some d when Arg.takesValue a -> [ "[default: " + d + "]" ]
             | _ -> [])
            @ (match a.Env with
               | Some e -> [ "[env: " + e + "]" ]
               | None -> [])
            @ (if a.PossibleValues.IsEmpty then [] else [ "[possible values: " + String.concat ", " a.PossibleValues + "]" ])
        String.concat " " (List.filter (fun s -> s <> "") (h :: extra))

    /// Full help text for a command. `globals` are inherited global args.
    let render (path: string) (cmd: Command) (globals: Arg list) (long: bool) : string =
        let visible = cmd.Args |> List.filter (fun a -> not a.Hidden)
        let positionals =
            visible |> List.filter (fun a -> a.Index.IsSome) |> List.sortBy (fun a -> a.Index.Value)
        let options =
            (visible |> List.filter (fun a -> a.Index.IsNone))
            @ (globals |> List.filter (fun a -> not a.Hidden))
            @ (builtins cmd |> List.map (fun a -> if a.Id = "help" then builtinHelp long else a))
        let subs = cmd.Subcommands |> List.filter (fun s -> not s.HiddenCommand)
        let sections =
            [ (match cmd.About with
               | Some a -> [ a; "" ]
               | None -> [])
              [ usage path cmd; "" ]
              (if subs.IsEmpty then []
               else
                   "Commands:"
                   :: table (
                       (subs |> List.map (fun s -> String.concat ", " (s.Name :: s.CommandAliases), defaultArg s.About ""))
                       @ [ "help", "Print this message or the help of the given subcommand(s)" ]
                   )
                   @ [ "" ])
              (if positionals.IsEmpty then []
               else "Arguments:" :: table (positionals |> List.map (fun a -> argLabel a, annotate a)) @ [ "" ])
              (if options.IsEmpty then []
               else "Options:" :: table (options |> List.map (fun a -> argLabel a, annotate a)) @ [ "" ]) ]
        (sections |> List.concat |> String.concat "\n").TrimEnd() + "\n"

    let versionText (path: string) (cmd: Command) : string =
        path + " " + defaultArg cmd.VersionText "" + "\n"
