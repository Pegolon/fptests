open System
open FSharp.Data
open FSharp.Control

let splitIntoParagraphs (text: string) =
    text.Split([|"\n\n"|], StringSplitOptions.RemoveEmptyEntries)
    |> Array.map String.Trim
    |> Array.toList

let translateParagraph (paragraph: string) =
    let apiUrl = "https://api-free.deepl.com/v2/translate"
    let authKey = "YOUR_AUTH_KEY" // Setze hier deinen DeepL API-Schlüssel ein
    let targetLang = "TARGET_LANG" // Setze hier die Zielsprache ein, z.B. "DE" für Deutsch
    let postData = [
        "auth_key", authKey
        "text", paragraph
        "target_lang", targetLang
    ]
    Http.RequestString(apiUrl, httpMethod = "POST", formData = postData)
    |> JsonValue.Parse
    |> fun json -> json.["translations"].[0].["text"].ToString()

let translateText (text: string) =
    let paragraphs = splitIntoParagraphs text
    asyncSeq {
        for paragraph in paragraphs do
            yield async {
                return! Async.FromContinuations(fun (cont, _, _) ->
                    translateParagraph paragraph |> cont)
            }
    }
    |> Async.Parallel
    |> AsyncSeq.toArray
    |> Array.map id
    |> String.Join("\n\n")

[<EntryPoint>]
let main argv =
    let text = "Hier ist der zu übersetzende Text.\n\nEr besteht aus mehreren Absätzen."
    printfn "%s" (translateText text)
    0
