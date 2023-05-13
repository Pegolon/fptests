(ns translation-program.core
  (:require [clj-http.client :as http]
            [clojure.core.async :as async]
            [clojure.string :as string]))

(defn- split-into-paragraphs [text]
  (->> text
       (string/split #"\n\n")
       (mapv string/trim)))

(defn- translate-paragraph [paragraph]
  (let [api-url "https://api-free.deepl.com/v2/translate"
        auth-key "YOUR_AUTH_KEY" ; Setze hier deinen DeepL API-Schlüssel ein
        params {:auth_key auth-key
                :text paragraph
                :target_lang "TARGET_LANG"} ; Setze hier die Zielsprache ein, z.B. "DE" für Deutsch
        response (http/post api-url {:form-params params})]
    (-> response
        :body
        (json/read-str :key-fn keyword)
        :translations
        first
        :text)))

(defn translate-text [text]
  (let [paragraphs (split-into-paragraphs text)
        translation-chan (async/chan)]
    (doseq [paragraph paragraphs]
      (async/go
        (>! translation-chan (translate-paragraph paragraph))))
    (->> (async/into [] translation-chan)
         (async/alts!)
         (mapv identity)
         (string/join "\n\n"))))

(defn -main []
  (let [text "Hier ist der zu übersetzende Text.\n\nEr besteht aus mehreren Absätzen."]
    (println (translate-text text))))
