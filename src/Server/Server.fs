module Server.Program

open System
open System.IO
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Hosting
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open FsToolkit.ErrorHandling
open Shared
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table

let wwwRoot = "public"

module Configuration =
    type Config = {
        StorageConnectionString : string
    }

    let load () =
            (ConfigurationBuilder())
#if DEBUG
                .AddJsonFile("appsettings.development.json", true)
#endif
                .AddEnvironmentVariables()
                .Build()
            |> (fun cfg -> { StorageConnectionString = cfg.["StorageConnectionString"] })

module MessagesApi =
    open Server.TableStorage

    let [<Literal>] private tableName = "Messages"
    let [<Literal>] private partitionKey = "wug2020"

    type MessageEntity() =
        inherit TableEntity()
        member val Author = "" with get, set
        member val Text = "" with get, set

    let private toEntity (msg:Message) =
        let e = new MessageEntity()
        e.PartitionKey <- partitionKey
        e.RowKey <- Guid.NewGuid().ToString()
        e.Author <- msg.Author
        e.Text <- msg.Text
        e.ETag <- "*"
        e

    let private toDateTimeMessage (e:MessageEntity) =
        {
            UtcDateTime = e.Timestamp.UtcDateTime
            Item = { Author = e.Author; Text = e.Text }
        }

    let private getAll (client:CloudTableClient) () =
        tableQuery {
            table tableName
            where (pk partitionKey)
        }
        |> executeQuery<MessageEntity> client
        |> Task.map (List.map toDateTimeMessage >> List.sortByDescending (fun x -> x.UtcDateTime))

    let private add (client:CloudTableClient) (msg:Message) =
        tableCommand {
            table tableName
            insert (msg |> toEntity)
        }
        |> executeCommand client
        |> Task.map ignore


    let api (client:CloudTableClient) = {
        GetMessages = getAll client >> Async.AwaitTask
        AddMessage = add client >> Async.AwaitTask
    }

let webApp (cfg:Configuration.Config) =
    let client = CloudStorageAccount.Parse(cfg.StorageConnectionString).CreateCloudTableClient()
    let remoting =
        Remoting.createApi()
        |> Remoting.withRouteBuilder Route.builder
        |> Remoting.fromValue (MessagesApi.api client)
        |> Remoting.buildHttpHandler
    choose [
        remoting
        htmlFile <| Path.Combine(wwwRoot, "index.html")
    ]

let configureApp cfg (app:IApplicationBuilder) =
    app
        .UseStaticFiles()
        .UseGiraffe (webApp cfg)

let configureServices (services:IServiceCollection) =
    services
        .AddGiraffe() |> ignore

[<EntryPoint>]
let main _ =
    let cfg = Configuration.load()

    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(configureApp cfg)
                    .ConfigureServices(configureServices)
                    .UseUrls([|"http://0.0.0.0:8085"|])
                    .UseWebRoot(wwwRoot)
                    |> ignore)
        .Build()
        .Run()
    0