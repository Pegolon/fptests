open System
open System.IO
open System.Security.Cryptography
open Amazon
open Amazon.S3
open Amazon.S3.Transfer
open System.Threading.Tasks
open FsToolkit.ErrorHandling


let calculateSha256Hash (input: byte[]) =
    use sha256 = SHA256.Create()
    let hashBytes = sha256.ComputeHash(input)
    Convert.ToBase64String(hashBytes)

let getFileMetadata (bucketName: string) (path: string) =
    async {
        use client = new AmazonS3Client()
        let request = new GetObjectMetadataRequest(BucketName = bucketName, Key = path)
        try
            let! response = client.GetObjectMetadataAsync(request)
            return response.Metadata
        with
            | :? AmazonS3Exception as ex ->
                if ex.ErrorCode = "NoSuchKey" then
                    return System.Collections.Generic.Dictionary<string, string>()
                else
                    failwithf "Fehler beim Abrufen der Metadaten: %s" ex.Message
            | ex ->
                failwithf "Fehler beim Abrufen der Metadaten: %s" ex.Message
    }

let uploadToS3 (bucketName: string) (path: string) (data: byte[]) =
    async {
        use client = new AmazonS3Client()
        let request = new Amazon.S3.Model.PutObjectRequest(BucketName = bucketName, Key = path, InputStream = new MemoryStream(data))
        let! response = client.PutObjectAsync(request)
        return response.HttpStatusCode
    }

let waitForFileInS3 (bucketName: string) (path: string) =
    async {
        use client = new AmazonS3Client()
        while true do
            let request = new Amazon.S3.Model.ListObjectsV2Request(BucketName = bucketName, Prefix = path)
            let! response = client.ListObjectsV2Async(request)
            let exists = response.S3Objects |> Seq.exists (fun obj -> obj.Key = path)
            if exists then
                return ()
            else
                do! Task.Delay(TimeSpan.FromSeconds(1.0)) |> Async.AwaitTask
    }

let downloadFromS3 (bucketName: string) (path: string) =
    async {
        use client = new AmazonS3Client()
        let request = new Amazon.S3.Model.GetObjectRequest(BucketName = bucketName, Key = path)
        let! response = client.GetObjectAsync(request)
        use stream = response.ResponseStream
        use reader = new StreamReader(stream)
        return reader.ReadToEnd()
    }

let processInput (input: byte[]) =
    async {
        let hash = calculateSha256Hash input
        let fileName = hash + ".txt"
        let uploadPath = "requests/" + fileName
        let downloadPath = "responses/" + fileName

        do! uploadToS3 "Processing" uploadPath input
        do! waitForFileInS3 "Processing" downloadPath
        let! result = downloadFromS3 "Processing" downloadPath
        return result
    }


type FileMetadata =
    { LastModified: DateTime
      ETag: string
      Size: int64 }

let checkS3FileMetadata (bucketName: string) (fileName: string) (callback: FileMetadata -> unit) =
    let rec checkMetadataLoop (lastMetadata: FileMetadata option) =
        async {
            try
                use client = new AmazonS3Client()
                let! metadata = client.GetObjectMetadataAsync(bucketName, fileName)

                match lastMetadata with
                | Some prevMetadata when prevMetadata = metadata ->
                    return! Async.Sleep 1000 // Keine Änderung, warte und überprüfe erneut
                | _ ->
                    let currentMetadata =
                        { LastModified = metadata.LastModified
                          ETag = metadata.ETag
                          Size = metadata.ContentLength }
                    callback currentMetadata // Aufruf der Callback-Funktion mit den geänderten Metadaten
                    return! Async.Sleep 1000 // Warte und überprüfe erneut

            with
            | ex ->
                printfn "Fehler beim Überprüfen der Metadaten: %s" ex.Message
                return! Async.Sleep 1000 // Bei einem Fehler warte und überprüfe erneut
        }

    Async.StartImmediate (checkMetadataLoop None)

[<EntryPoint>]
let main argv =
    let input = // Hier das Byte-Array für den Eingabeparameter festlegen
    let result = Async.RunSynchronously (processInput input)
    printfn "%s" result
    0
