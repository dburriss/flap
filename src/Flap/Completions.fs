namespace Flap

type Shell =
    | Bash
    | Zsh
    | Fish
    | PowerShell

module Completions =

    let private flagsOf (cmd: Command) : string list =
        (Help.builtins cmd @ cmd.Args)
        |> List.filter (fun a -> a.Index.IsNone && not a.Hidden)
        |> List.collect (fun a ->
            (a.Short |> Option.map (fun c -> "-" + string c) |> Option.toList)
            @ (a.Long |> Option.map (fun l -> "--" + l) |> Option.toList))

    let private subNames (cmd: Command) =
        cmd.Subcommands |> List.filter (fun s -> not s.HiddenCommand) |> List.collect (fun s -> s.Name :: s.CommandAliases)

    /// Flatten into (path-id, command) pairs, ids like `app__sub`.
    let rec private walk (id: string) (cmd: Command) : (string * Command) list =
        (id, cmd) :: (cmd.Subcommands |> List.collect (fun s -> walk (id + "__" + s.Name) s))

    let private ident (s: string) = s.Replace('-', '_')

    let private words (cmd: Command) = String.concat " " (flagsOf cmd @ subNames cmd)

    let private bash (cmd: Command) =
        let all = walk (ident cmd.Name) cmd
        let transitions =
            all
            |> List.collect (fun (id, c) ->
                c.Subcommands
                |> List.collect (fun s ->
                    (s.Name :: s.CommandAliases)
                    |> List.map (fun n -> "                " + id + "," + n + ") cmd=\"" + id + "__" + ident s.Name + "\" ;;")))
        let cases =
            all |> List.map (fun (id, c) -> "        " + id + ") opts=\"" + words c + "\" ;;")
        let f = "_" + ident cmd.Name
        String.concat
            "\n"
            ([ f + "() {"
               "    local cur=\"${COMP_WORDS[COMP_CWORD]}\" cmd=\"" + ident cmd.Name + "\" opts w i"
               "    for ((i=1; i<COMP_CWORD; i++)); do"
               "        w=\"${COMP_WORDS[i]}\""
               "        case \"$cmd,$w\" in" ]
             @ transitions
             @ [ "        esac"; "    done"; "    case \"$cmd\" in" ]
             @ cases
             @ [ "    esac"
                 "    COMPREPLY=( $(compgen -W \"$opts\" -- \"$cur\") )"
                 "}"
                 "complete -F " + f + " -o bashdefault -o default " + cmd.Name
                 "" ])

    let private zsh (cmd: Command) =
        let all = walk (ident cmd.Name) cmd
        let transitions =
            all
            |> List.collect (fun (id, c) ->
                c.Subcommands
                |> List.collect (fun s ->
                    (s.Name :: s.CommandAliases)
                    |> List.map (fun n -> "            " + id + "," + n + ") cmd=\"" + id + "__" + ident s.Name + "\" ;;")))
        let cases =
            all |> List.map (fun (id, c) -> "        " + id + ") opts=(" + words c + ") ;;")
        let f = "_" + ident cmd.Name
        String.concat
            "\n"
            ([ "#compdef " + cmd.Name
               f + "() {"
               "    local cmd=\"" + ident cmd.Name + "\" w"
               "    local -a opts"
               "    for w in ${words[2,CURRENT-1]}; do"
               "        case \"$cmd,$w\" in" ]
             @ transitions
             @ [ "        esac"; "    done"; "    case \"$cmd\" in" ]
             @ cases
             @ [ "    esac"; "    compadd -- $opts"; "}"; "compdef " + f + " " + cmd.Name; "" ])

    let private escQ (s: string) = s.Replace("'", "'\\''")

    let private fish (cmd: Command) =
        let rec go (parents: string list) (c: Command) : string list =
            let cond =
                match parents with
                | [] -> "__fish_use_subcommand"
                | _ -> "__fish_seen_subcommand_from " + List.last parents
            let opts =
                (Help.builtins c @ c.Args)
                |> List.filter (fun a -> a.Index.IsNone && not a.Hidden)
                |> List.map (fun a ->
                    "complete -c " + cmd.Name + " -n \"" + cond + "\""
                    + (a.Short |> Option.map (fun s -> " -s " + string s) |> Option.defaultValue "")
                    + (a.Long |> Option.map (fun l -> " -l " + l) |> Option.defaultValue "")
                    + (if Arg.takesValue a then " -r" else "")
                    + (match a.HelpText with
                       | Some h -> " -d '" + escQ h + "'"
                       | None -> ""))
            let subs =
                c.Subcommands
                |> List.filter (fun s -> not s.HiddenCommand)
                |> List.map (fun s ->
                    "complete -c " + cmd.Name + " -n \"" + cond + "\" -f -a \"" + s.Name + "\""
                    + (match s.About with
                       | Some h -> " -d '" + escQ h + "'"
                       | None -> ""))
            opts @ subs @ (c.Subcommands |> List.collect (fun s -> go (parents @ [ s.Name ]) s))
        String.concat "\n" (go [] cmd) + "\n"

    let private powershell (cmd: Command) =
        let all = walk (ident cmd.Name) cmd
        let rec transitions (id: string) (c: Command) : string list =
            c.Subcommands
            |> List.collect (fun s ->
                let child = id + "__" + ident s.Name
                ("        '" + id + "," + s.Name + "' { $cmd = '" + child + "' }") :: transitions child s)
        String.concat
            "\n"
            ([ "Register-ArgumentCompleter -Native -CommandName '" + cmd.Name + "' -ScriptBlock {"
               "    param($wordToComplete, $commandAst, $cursorPosition)"
               "    $cmd = '" + ident cmd.Name + "'"
               "    foreach ($e in $commandAst.CommandElements | Select-Object -Skip 1) {"
               "        $v = $e.ToString()"
               "        switch (\"$cmd,$v\") {" ]
             @ (transitions (ident cmd.Name) cmd)
             @ [ "        }"; "    }"; "    $words = switch ($cmd) {" ]
             @ (all |> List.map (fun (id, c) ->
                    let key = id
                    let items = (flagsOf c @ subNames c) |> List.map (fun w -> "'" + w + "'") |> String.concat ", "
                    "        '" + key + "' { @(" + items + ") }"))
             @ [ "    }"
                 "    $words | Where-Object { $_ -like \"$wordToComplete*\" } |"
                 "        ForEach-Object { [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_) }"
                 "}"
                 "" ])

    /// Generate a completion script for the given shell.
    let generate (shell: Shell) (cmd: Command) : string =
        match shell with
        | Bash -> bash cmd
        | Zsh -> zsh cmd
        | Fish -> fish cmd
        | PowerShell -> powershell cmd
