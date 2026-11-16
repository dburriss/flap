namespace Todo.Domain

/// Slice: mark a todo as done.
module CompleteTodo =

    type Command = { Id: int }

    let handle (store: Store) (cmd: Command) : Result<Todo, DomainError> =
        let todos = store.Load()

        match todos |> List.tryFind (fun t -> t.Id = cmd.Id) with
        | None -> Error(NotFound cmd.Id)
        | Some t when t.Done -> Error(AlreadyDone t.Id)
        | Some t ->
            let done' = { t with Done = true }
            store.Save(todos |> List.map (fun x -> if x.Id = t.Id then done' else x))
            Ok done'
