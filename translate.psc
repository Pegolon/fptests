module Main where

import Prelude
import Control.Monad.Aff (Aff)
import Control.Monad.Aff.Console (log)
import Control.Monad.Aff.Parallel (parMap)
import Data.Maybe (Maybe(..))
import Data.String.Regex (Regex, regex)
import Effect (Effect)
import Effect.Class (liftEffect)
import Effect.Console (CONSOLE)
import Effect.Exception (error)
import Network.HTTP.Affjax (post, postFormData, RequestData(..), Response(..))
import Network.HTTP.Method (Method(POST))
import Network.HTTP.Status (Status(..))
import Network.HTTP.Types.Header (hContentType)
import Network.HTTP.Types.Method (hMethod)
import Network.HTTP.Types.Status (statusCode)
import Text.JSON (decodeString, JSONValue(..))

splitIntoParagraphs :: Regex -> String -> Array String
splitIntoParagraphs reg str =
  let
    paragraphs = regex reg str
    trimmedParagraphs = map String.trim paragraphs
  in toUnfoldable trimmedParagraphs

translateParagraph :: String -> Aff (Maybe String)
translateParagraph paragraph = do
  let apiUrl = "https://api-free.deepl.com/v2/translate"
  let authKey = "YOUR_AUTH_KEY" -- Setze hier deinen DeepL API-Schlüssel ein
  let targetLang = "TARGET_LANG" -- Setze hier die Zielsprache ein, z.B. "DE" für Deutsch
  let requestData = RequestData
        { method: POST
        , headers: [hContentType "application/x-www-form-urlencoded"]
        , url: apiUrl
        , body: postFormData [("auth_key", authKey), ("text", paragraph), ("target_lang", targetLang)]
        }
  response <- post requestData
  case statusCode response.status of
    200 -> case decodeString response.body of
      Ok json -> pure $ Just $ json.!("translations").!(0).!("text").toString
      Error err -> do
        liftEffect $ log $ "Fehler bei der JSON-Dekodierung: " <> err
        pure Nothing
    _ -> do
      liftEffect $ log $ "Fehler beim Übersetzen: " <> response.status.message
      pure Nothing

translateText :: String -> Aff String
translateText text = do
  let regexPattern = "\n\n"
  let paragraphs = splitIntoParagraphs (regex regexPattern) text
  translations <- parMap translateParagraph paragraphs
  let translatedParagraphs = map (\x -> fromMaybe "" x) translations
  pure $ String.joinWith "\n\n" translatedParagraphs

main :: Effect Unit
main = do
  let text = "Hier ist der zu übersetzende Text.\n\nEr besteht aus mehreren Absätzen."
  result <- translateText text
  case result of
    Left err -> error $ "Fehler beim Übersetzen: " <> err
    Right translatedText -> log translatedText
