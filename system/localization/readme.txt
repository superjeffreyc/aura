// Aura
// Documentation
// --------------------------------------------------------------------------

Localization in Aura is done through a "gettext"-like system, which is
commonly used for this type of work. The server reads the ".po" file
specified specified in the configuration from the localization folder and
uses the translated lines. If a line is not translated, it uses the original,
English version instead.

Since we frequently add and change lines in the core and NPCs, we don't
put prepared ".po" files into the core ourselves, but you can generate
them from the source files yourself, using various tools. We recommand
the free "Poedit": http://poedit.net/

Here's a short description on how to create a new translation using Poedit:

* Open Poedit

Preparation (this is a one time thing after installing Poedit):

* Click "File > Preferences..."
* Switch to "Extractors"
* Select "C#" and click "Edit"
* In the input field "Command to extract translations:" add the following
  at the end: -k
  It should look something like this now:
  xgettext --language=C# --add-comments=TRANSLATORS: [...] %C %K %F -k
* Close both dialogs with "OK" and continue.

Creating a new translation:

* Click "File > New..."
* Select a language (which one is unimportant for Aura)
* Save file, for example as "user/localization/de-DE.po" for a German
  translation. The name of the file is what you put as "language" in
  localization.conf, in this case "de-DE".
* Click "Extract from sources"
* In the first list click the "New Item" button and enter the path to your
  Aura folder.
* Switch to the "Translation properties", enter any project name, and
  select "UTF-8" as source code charset.
* Switch to the "Source keywords" and add the following new items,
  without quotation:
  "Localization.Get:1"
  "Localization.GetPlural:1,2"
  "Localization.GetParticular:1c,2"
  "Localization.GetParticularPlural:1c,2,3"
  "L:1"
  These are the function names the program will look for to get strings.
  Localization.Get[...] is usually used inside the core, e.g. for the Eweca
  messages. The other methods are used in scripts for various things.
  (Note: Most scripts don't support localization yet.)
* Poedit will now search all files for translatable texts.
* Once it's done searching you can start translating, by selecting a line
  and adding the translation at the bottom.
* Finally, save the file and you're done. It should now use the the strings in
  that file, after you've told Aura to use it in localization.conf.
  You can also create a "en-US.po" file, to replace default phrases,
  without changing the language.

After creating the ".po" file once, you can simply open it with Poedit again,
fix translations, or update the catalogue, which makes it read the source
again, adding new lines to it for you to translate while keeping what you've
done so far.
