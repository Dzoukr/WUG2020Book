module Server.TableStorage

open Microsoft.WindowsAzure.Storage.Table
open System
open FSharp.Control.Tasks.V2

let private partitionKey = "PartitionKey"
let private rowKey = "RowKey"

type OrderDirection =
    | Asc
    | Desc

type OrderBy = string * OrderDirection

type ColumnComparison =
    | Eq of obj
    | Ne of obj
    | Gt of obj
    | Lt of obj
    | Ge of obj
    | Le of obj

module ColumnComparison =
    let toQueryComparison = function
        | Eq _ -> QueryComparisons.Equal
        | Ne _ -> QueryComparisons.NotEqual
        | Gt _ -> QueryComparisons.GreaterThan
        | Lt _ -> QueryComparisons.LessThan
        | Ge _ -> QueryComparisons.GreaterThanOrEqual
        | Le _ -> QueryComparisons.LessThanOrEqual

    let value = function
        | Eq x
        | Ne x
        | Gt x
        | Lt x
        | Ge x
        | Le x -> x


type BinaryOperation =
    | And
    | Or

module BinaryOperation =
    let toTableOperator = function
        | And -> TableOperators.And
        | Or -> TableOperators.Or

type UnaryOperation =
    | Not

type Where =
    | Empty
    | Column of string * ColumnComparison
    | Binary of Where * BinaryOperation * Where
    | Unary of UnaryOperation * Where
    static member (+) (a, b) = Binary(a, And, b)
    static member (*) (a, b) = Binary(a, Or, b)
    static member (!!) a = Unary (Not, a)

type Pagination =
    | Take of take:int

type Query = {
    Table : string
    Where : Where
    Pagination : Pagination option
    Columns : string list
}

type TableQueryBuilder() =
    member __.Yield _ =
        {
            Table = ""
            Where = Where.Empty
            Pagination = None
            Columns = []
        } : Query


    [<CustomOperation "table">]
    member __.Table (state:Query, name) = { state with Table = name }

    [<CustomOperation "columns">]
    member __.Columns (state:Query, cols) = { state with Columns = cols }

    [<CustomOperation "take">]
    member __.Take (state:Query, n:int) = { state with Pagination = Some <| Pagination.Take(n) }

    [<CustomOperation "where">]
    member __.Where (state:Query, w:Where) = { state with Where = w }

let tableQuery = TableQueryBuilder()

let private getColumnComparison field comp =
    let value = ColumnComparison.value comp
    let c = ColumnComparison.toQueryComparison comp
    match value with
    | :? (byte[]) as v -> TableQuery.GenerateFilterConditionForBinary(field, c, v)
    | :? bool as v -> TableQuery.GenerateFilterConditionForBool(field, c, v)
    | :? DateTimeOffset as v -> TableQuery.GenerateFilterConditionForDate(field, c, v)
    | :? double as v -> TableQuery.GenerateFilterConditionForDouble(field, c, v)
    | :? Guid as v -> TableQuery.GenerateFilterConditionForGuid(field, c, v)
    | :? int as v -> TableQuery.GenerateFilterConditionForInt(field, c, v)
    | :? int64 as v -> TableQuery.GenerateFilterConditionForLong(field, c, v)
    | _ -> TableQuery.GenerateFilterCondition(field, c, value.ToString())

let rec private toWhereQuery (f:Where) =
    match f with
    | Empty -> ""
    | Column (field, comp) -> getColumnComparison field comp
    | Binary(w1, comb, w2) ->
        match toWhereQuery w1, toWhereQuery w2 with
        | "", fq | fq , "" -> fq
        | fq1, fq2 -> TableQuery.CombineFilters(fq1, BinaryOperation.toTableOperator comb, fq2)
    | Unary (Not, w) ->
        match toWhereQuery w with
        | "" -> ""
        | v -> sprintf "not (%s)" v

let private applyPagination (p:Pagination option) (query:TableQuery<_>) =
    match p with
    | Some (Take t) ->
        query.TakeCount <- Nullable<int>(t)
        query
    | None -> query

let private applyColumns (cols:string list) (query:TableQuery<_>) =
    if cols.Length > 0 then
        query.SelectColumns <- ResizeArray<string>(cols)
    query

let private applyWhere (f:Where) (query:TableQuery<_>) =
    let filter = f |> toWhereQuery
    if filter.Length > 0 then
        query.Where(filter)
    else query

let private toTableQuery<'a when 'a :> ITableEntity and 'a : (new : unit -> 'a)> (q:Query) : TableQuery<'a> =
    TableQuery<'a>()
    |> applyPagination q.Pagination
    |> applyColumns q.Columns
    |> applyWhere q.Where

let getTableOrCreate (client:CloudTableClient) tableName =
    let table = client.GetTableReference tableName
    task {
        match! table.ExistsAsync() with
        | true -> return table
        | false ->
            let! _ = table.CreateIfNotExistsAsync()
            return table
    }

let executeQuery<'a when 'a :> ITableEntity and 'a : (new : unit -> 'a)> (client:CloudTableClient) (q:Query) =
    task {
        let! table = q.Table |> getTableOrCreate client
        let query = q |> toTableQuery
        let values = Collections.Generic.List<'a>()
        let mutable token = TableContinuationToken()
        while token |> isNull |> not do
            let! res = table.ExecuteQuerySegmentedAsync(query, token)
            do values.AddRange(res.Results)
            token <- res.ContinuationToken
        return values |> Seq.toList
    }

type Operation<'a> =
    | Insert of 'a
    | InsertOrMerge of 'a
    | InsertOrReplace of 'a
    | Merge of 'a
    | Replace of 'a
    | Delete of 'a

type Command<'a> = {
    Table : string
    Operations : Operation<'a> list
}

type TableCommandBuilder() =
    member __.Yield _ =
        {
            Table = ""
            Operations = []
        } : Command<_>


    [<CustomOperation "table">]
    member __.Table (state:Command<_>, name) = { state with Table = name }

    [<CustomOperation "operation">]
    member __.Operation (state:Command<_>, op) = { state with Operations = state.Operations @ [op] }

    [<CustomOperation "operations">]
    member __.Operations (state:Command<_>, ops) = { state with Operations = state.Operations @ ops }

    [<CustomOperation "insert">]
    member __.Insert (state:Command<_>, entity) = { state with Operations = state.Operations @ [Insert entity] }

    [<CustomOperation "insertOrMerge">]
    member __.InsertOrMerge (state:Command<_>, entity) = { state with Operations = state.Operations @ [InsertOrMerge entity] }

    [<CustomOperation "insertOrReplace">]
    member __.InsertOrReplace (state:Command<_>, entity) = { state with Operations = state.Operations @ [InsertOrReplace entity] }

    [<CustomOperation "merge">]
    member __.Merge (state:Command<_>, entity) = { state with Operations = state.Operations @ [Merge entity] }

    [<CustomOperation "replace">]
    member __.Replace (state:Command<_>, entity) = { state with Operations = state.Operations @ [Replace entity] }

    [<CustomOperation "delete">]
    member __.Delete (state:Command<_>, entity) = { state with Operations = state.Operations @ [Delete entity] }

let tableCommand = TableCommandBuilder()

let private toTableOperation = function
    | Insert v -> TableOperation.Insert(v)
    | InsertOrMerge v -> TableOperation.InsertOrMerge(v)
    | InsertOrReplace v -> TableOperation.InsertOrReplace(v)
    | Merge v -> TableOperation.Merge(v)
    | Replace v -> TableOperation.Replace(v)
    | Delete v -> TableOperation.Delete(v)

let private addToBatch (tbo:TableBatchOperation) = function
    | Insert v -> tbo.Insert(v)
    | InsertOrMerge v -> tbo.InsertOrMerge(v)
    | InsertOrReplace v -> tbo.InsertOrReplace(v)
    | Merge v -> tbo.Merge(v)
    | Replace v -> tbo.Replace(v)
    | Delete v -> tbo.Delete(v)

let executeCommand<'a when 'a :> ITableEntity and 'a : (new : unit -> 'a)> (client:CloudTableClient) (q:Command<'a>) =
    task {
        let! table = q.Table |> getTableOrCreate client
        let batch = TableBatchOperation()
        q.Operations |> List.iter (addToBatch batch)
        return! table.ExecuteBatchAsync(batch)
    }

/// Creates WHERE condition for column
let column name whereComp = Where.Column(name, whereComp)
/// WHERE column value equals to
let eq name (o:obj) = column name (Eq o)
/// WHERE column value not equals to
let ne name (o:obj) = column name (Ne o)
/// WHERE column value greater than
let gt name (o:obj) = column name (Gt o)
/// WHERE column value lower than
let lt name (o:obj) = column name (Lt o)
/// WHERE column value greater/equals than
let ge name (o:obj) = column name (Ge o)
/// WHERE column value lower/equals than
let le name (o:obj) = column name (Le o)
/// WHERE PK equals
let pk (value:string) = column "PartitionKey" (Eq value)
/// WHERE RK equals
let rk (value:string) = column "RowKey" (Eq value)