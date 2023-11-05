open FSharp.Data
open Npgsql
open System.Threading.Tasks
module Providers =    
    [<Literal>]
    let ResolutionFolder = __SOURCE_DIRECTORY__
    type DHsysDefaultProductProvider = CsvProvider<"CSV/products.csv",Separators = ",",HasHeaders=true,ResolutionFolder = ResolutionFolder>
    let DHsysDefaultProducts = DHsysDefaultProductProvider.GetSample()        

module Extract =
    open System.IO
    let load (stream:Stream) : Runtime.CsvFile<CsvRow> = CsvFile.Load(stream)
    let get  offset limit (csv:Runtime.CsvFile<CsvRow>)=            
        let row = csv.Skip(offset).Take(limit)
        row
    let count (csv:Runtime.CsvFile<CsvRow>) =        
        csv.Rows |> Seq.length
    let parse map (csv:Runtime.CsvFile<CsvRow>) =        
        csv.Map(fun e -> map e)    
module Product =

    type Dto = {
        UniqueCode:string
        Barcode:string
        RegistryCode:string
        Name:string
        LotNumber:string
        Sal:string
        LaboratoryCode:string
        LaboratoryName:string
        EndCustomerPrice:decimal
        CostPrice:decimal
        StockQuantity:int
        Prcdse:string
        Section:string
        MaxDiscountPercentage:decimal
        ActivePrinciple:string
    }
    let fromCsv (row:CsvRow) =
        {
            UniqueCode = row.Item "UniqueCode"
            //"Barcode",RegistryCode,Name,LotNumber,Sal,LaboratoryCode,LaboratoryName,EndCustomerPrice,CostPrice,StockQuantity,Prcdse,Section,MaxDiscountPercentage,ActivePrinciple"
            Barcode = row.Item "Barcode"
            RegistryCode = row.Item "RegistryCode"
            Name = row.Item "Name" 
            LotNumber = row.Item "LotNumber"
            Sal = row.Item "Sal"
            LaboratoryCode = row.Item "LaboratoryCode"
            LaboratoryName = row.Item "LaboratoryName"
            EndCustomerPrice = decimal (row.Item "EndCustomerPrice")
            CostPrice = decimal (row.Item "CostPrice")
            StockQuantity = int (row.Item "StockQuantity")
            Prcdse = row.Item "Prcdse"
            Section = row.Item "Section"
            MaxDiscountPercentage = decimal(row.Item "MaxDiscountPercentage")
            ActivePrinciple = row.Item "ActivePrinciple"
        }    
    let toCsv (product:Dto) =
        let row = Providers.DHsysDefaultProductProvider.Row(
            product.UniqueCode |> int,
            product.Barcode |> int64,
            product.RegistryCode |> int64,
            product.Name,
            product.LotNumber,
            product.Sal |> int,
            product.LaboratoryCode |> int,
            product.LaboratoryName,
            product.EndCustomerPrice,
            product.CostPrice,
            product.StockQuantity,
            product.Prcdse |> int,
            product.Section,
            product.MaxDiscountPercentage |> int,
            product.ActivePrinciple)
        row
    let batchSaveAsync (products:Dto seq) createConn = async {
        let awaitTask (task:Task) = task |> Async.AwaitTask |> Async.Ignore
        let conn:NpgsqlConnection = createConn ()
        use! writer = 
            conn.BeginBinaryImportAsync(@"COPY ""Product""(""UniqueCode"",""Barcode"",""RegistryCode"",""Name"",""LotNumber"",""Sal"",""LaboratoryCode"",""LaboratoryName"",""EndCustomerPrice"",""CostPrice"",""StockQuantity"",""Prcdse"",""Section"",""MaxDiscountPercentage"",""ActivePrinciple"") ") 
            |> Async.AwaitTask
        let bulkWrites = 
            products |> Seq.map (fun product -> async {
                do! writer.StartRowAsync() |> awaitTask
                do! writer.WriteAsync(product.UniqueCode,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                do! writer.WriteAsync(product.Barcode,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                do! writer.WriteAsync(product.RegistryCode,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                do! writer.WriteAsync(product.Name,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                do! writer.WriteAsync(product.LotNumber,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                do! writer.WriteAsync(product.Sal,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                do! writer.WriteAsync(product.LaboratoryCode,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                do! writer.WriteAsync(product.LaboratoryName,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                do! writer.WriteAsync(product.EndCustomerPrice,NpgsqlTypes.NpgsqlDbType.Money) |> awaitTask
                do! writer.WriteAsync(product.CostPrice,NpgsqlTypes.NpgsqlDbType.Money) |> awaitTask
                do! writer.WriteAsync(product.StockQuantity,NpgsqlTypes.NpgsqlDbType.Integer) |> awaitTask
                do! writer.WriteAsync(product.Prcdse,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                do! writer.WriteAsync(product.Section,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                do! writer.WriteAsync(product.MaxDiscountPercentage,NpgsqlTypes.NpgsqlDbType.Numeric) |> awaitTask
                do! writer.WriteAsync(product.ActivePrinciple,NpgsqlTypes.NpgsqlDbType.Text) |> awaitTask
                return ()
            })
        do! writer.CompleteAsync().AsTask() |> Async.AwaitTask |> Async.Ignore
        return 
            bulkWrites
            |> Async.Sequential
            |> Async.RunSynchronously                
    }
let main () = 
    use csvFile = System.IO.File.OpenRead("./CSV/products")
    let csv = Extract.load csvFile
    Extract.get 0 15 csv
    |> Extract.parse 
        (fun row ->             
            let recordRow = Product.fromCsv row
            row)

printfn "Hello from F#"
