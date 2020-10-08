module Index

open System
open Elmish
open Fable.Remoting.Client
open Shared

type Model = {
    Messages: DateTimeItem<Message> list
    InputMessage: Message
    IsLoading : bool
}

type Msg =
    | LoadMessages
    | MessagesLoaded of DateTimeItem<Message> list
    | InputMessageChanged of Message
    | SendMessage

let messagesApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<MessagesApi>

let init(): Model * Cmd<Msg> =
    { Messages = []; InputMessage = Message.empty; IsLoading = false }, Cmd.ofMsg LoadMessages

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    match msg with
    | LoadMessages -> { model with IsLoading = true }, Cmd.OfAsync.perform messagesApi.GetMessages () MessagesLoaded
    | MessagesLoaded msgs -> { model with IsLoading = false; Messages = msgs }, Cmd.none
    | InputMessageChanged msg -> { model with InputMessage = msg }, Cmd.none
    | SendMessage -> { model with IsLoading = true }, Cmd.OfAsync.perform messagesApi.AddMessage model.InputMessage (fun _ -> LoadMessages)

open Fable.React
open Fable.React.Props
open Feliz
open Feliz.Bulma

let navBrand =
    Bulma.navbarBrand.div [
        Bulma.navbarItem.a [
            prop.src "https://safe-stack.github.io/"
            navbarItem.isActive
            prop.children [
                Html.img [
                    prop.src "/favicon.png"
                ]
            ]
        ]
    ]

let formatDate (dt:DateTime) = dt.ToString("dd. MM. yyyy (HH:mm:ss)")

let containerBox (model : Model) (dispatch : Msg -> unit) =
    let canSend = (Message.isValid model.InputMessage && not model.IsLoading)
    Bulma.box [
        Bulma.content [
            for msg in model.Messages do
                Html.div [
                    sprintf "%s %s napsal(a): %s" (formatDate msg.UtcDateTime) msg.Item.Author msg.Item.Text |> prop.text
                ]
        ]
        Bulma.field.div [
            field.isGrouped
            prop.children [
                Bulma.control.p [
                    Bulma.input.text [
                        prop.valueOrDefault model.InputMessage.Author
                        prop.placeholder "Jméno"
                        prop.onTextChange (fun (x:string) -> { model.InputMessage with Author = x } |> InputMessageChanged |> dispatch)
                    ]
                ]
                Bulma.control.p [
                    Bulma.input.text [
                        prop.valueOrDefault model.InputMessage.Text
                        prop.placeholder "Text"
                        prop.onTextChange (fun (x:string) -> { model.InputMessage with Text = x } |> InputMessageChanged |> dispatch)
                    ]

                ]
                Bulma.button.a [
                    color.isPrimary
                    prop.text (if model.IsLoading then "..." else "Publikovat")
                    prop.disabled (not canSend)
                    if canSend then
                        prop.onClick (fun _ -> dispatch SendMessage)
                ]
                Bulma.button.a [
                    prop.style [ style.marginLeft 5 ]
                    color.isInfo
                    prop.text "Reload"
                    prop.onClick (fun _ -> dispatch LoadMessages)
                ]
            ]
        ]
    ]

let view (model : Model) (dispatch : Msg -> unit) =
    Bulma.hero [
        color.isPrimary
        hero.isFullHeight
        prop.style [
            style.backgroundImage """linear-gradient(rgba(0, 0, 0, 0.5), rgba(0, 0, 0, 0.5)), url("https://unsplash.it/1200/900?random")"""
            style.backgroundSize.cover
        ]
        prop.children [
            Bulma.heroHead [ Bulma.container navBrand ]
            Bulma.heroBody [
                Bulma.container [
                    Bulma.column [
                        column.is6
                        column.isOffset3
                        prop.children [
                            Bulma.title.h1 [ text.hasTextCentered; prop.text "WUG 2020 Book" ]
                            containerBox model dispatch
                        ]
                    ]
                ]
            ]
        ]
    ]