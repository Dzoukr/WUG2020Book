namespace Shared

open System

type Message = {
    Author : string
    Text : string
}

type DateTimeItem<'a> = {
    Item : 'a
    UtcDateTime : DateTime
}

module Message =
    let empty = { Author = ""; Text = "" }

    let isValid m =
        String.IsNullOrWhiteSpace m.Author |> not
        &&
        String.IsNullOrWhiteSpace m.Text |> not

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

type MessagesApi = {
    GetMessages : unit -> Async<DateTimeItem<Message> list>
    AddMessage : Message -> Async<unit>
}