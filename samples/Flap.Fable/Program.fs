module Flap.FableSample

open Flap

let cmd =
    Command.create "fable-sample"
    |> Command.version "0.1.0"
    |> Command.arg (Arg.create "name" |> Arg.short 'n' |> Arg.long "name" |> Arg.defaultValue "world")
    |> Command.arg (Arg.create "loud" |> Arg.long "loud" |> Arg.flag)
    |> Command.arg (Arg.create "greeting" |> Arg.long "greeting" |> Arg.env "FABLE_GREETING" |> Arg.defaultValue "hello")
    |> Command.subcommand (Command.create "build" |> Command.arg (Arg.create "release" |> Arg.long "release" |> Arg.flag))

// Fable has no portable environment access, so supply your own lookup.
// Here it is a plain map; on Node you could use `fun name -> Option.ofObj (emitJsExpr ...)`,
// e.g. via Fable.Core: `[<Emit("process.env[$0]")>] let private processEnv (name: string) : string = jsNative`.
let fakeEnv : Map<string, string> = Map.ofList [ "FABLE_GREETING", "hi from env" ]
let env (name: string) : string option = Map.tryFind name fakeEnv

for argv in [ [ "-n"; "fable"; "--loud" ]; [ "build"; "--release" ]; [ "--bogus" ]; [ "--help" ] ] do
    match Command.tryParseWith env argv cmd with
    | Ok m ->
        let sub =
            match Matches.subcommand m with
            | Some(n, sm) -> n + " release=" + string (Matches.flag "release" sm)
            | None -> "-"
        System.Console.WriteLine("ok name=" + Matches.getString "name" m + " greeting=" + Matches.getString "greeting" m + " loud=" + string (Matches.flag "loud" m) + " sub=" + sub)
    | Error e -> System.Console.WriteLine(ParseError.render e)
