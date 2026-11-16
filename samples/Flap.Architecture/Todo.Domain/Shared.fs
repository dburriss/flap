namespace Todo.Domain

type Priority =
    | Low
    | Normal
    | High

type Todo =
    { Id: int
      Title: string
      Priority: Priority
      Done: bool }

type DomainError =
    | EmptyTitle
    | NotFound of id: int
    | AlreadyDone of id: int

/// A port: the domain says what storage it needs, the host (the CLI) supplies it.
type Store =
    { Load: unit -> Todo list
      Save: Todo list -> unit }
