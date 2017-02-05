# CM3D2.AutoTranslate
This is a plugin for UnityInjector for CM3D2 that catches all untranslated text and uses a machine translator to translate it. The result replaces then the ingame text. You can find the support thread in the Honfire forum [here](http://www.hongfire.com/forum/forum/hentai-lair/hf-modding-translation/custom-maid-3d-2-mods/5756478-plugin-automatic-google-translate-v1-1-0).

## Json Interface
The plugin provides an tcp interface to provide your own translation support. Your translation program should listen for an tcp connection (default port: 9586). All translation requests are sent over one connection.
A packet consists of a 4 byte integer (in network byteorder) followed by a JSON string. The integer contains the size of the JSON string in bytes.
The JSON can contain the following fields:

Field | Description
----- | -----------
"method" | Either "translate", "translation" or "quit"
"id" | A unique identifier for that translation request
"text" | The text that should be translated, currently this text uses \uXXXX encoding for non ascii characters
"translation" | The translation that your program provides
"success" | A boolen value that indicates iff the translation was successfull


A standart communication looks like this (the preceding size integers are here omitted):

The Plugin sends a translation request:
  ```JSON
    {"method":"translate","id":0,"text":"\u304C\u30B7\u30E7\u30C3\u30D7\u306B\u8FFD\u52A0\u3055\u308C\u307E\u3057\u305F\u3002"}
  ```
Your program translates the text and sends the answer. It is important, that the "id" field contains the same id as the corresponding request:
  ```JSON
    {"id":0,"success":true,"method":"translation","translation":"But, it was added to a shop."}
  ```
When the game is closed, it sends a last packet with method "quit"
