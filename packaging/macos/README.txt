DuelLab for macOS
=================

Yu-Gi-Oh! deck building and opening-hand consistency analysis, running on your own Mac.

Which download?
---------------
- DuelLab-macos-arm64.zip: Apple Silicon Macs (M1, M2, M3, M4 and later). Most Macs from 2020 on.
- DuelLab-macos-x64.zip: Intel Macs.
Not sure? Apple menu > About This Mac: "Chip: Apple M…" means arm64, "Processor: Intel" means x64.

Start
-----
1. Unzip it and move the DuelLab folder anywhere you like (for example your Applications or
   Documents folder). Keep the files together.
2. Double-click DuelLab. A Terminal window opens and your browser opens DuelLab at
   http://127.0.0.1:5180 . Keep the Terminal window open while you use DuelLab; close it to stop.
3. The first start downloads the card list from YGOPRODeck (a few seconds; needs internet).

"DuelLab cannot be opened" / "Apple could not verify..."
--------------------------------------------------------
DuelLab is not notarized by Apple (that needs a paid developer account), so macOS blocks it the
first time. To allow it once:
1. Double-click DuelLab and close the warning.
2. Open System Settings > Privacy & Security, scroll down, and click "Open Anyway" next to DuelLab.
3. Double-click DuelLab again and confirm.
Alternative (Terminal): xattr -dr com.apple.quarantine /path/to/DuelLab-folder

Your data
---------
Decks, simulations and downloaded card images are stored in
  ~/Library/Application Support/DuelLab
(in Finder: Go > Go to Folder…, then paste that path).
- Back up: copy duellab.db from that folder.
- Update DuelLab: delete the old DuelLab folder and unzip the new one. Your decks stay.
- Uninstall: delete the DuelLab folder and ~/Library/Application Support/DuelLab.

Privacy
-------
DuelLab only listens on your own Mac (127.0.0.1): nobody else on your network can open it.
It contacts YGOPRODeck to download card data and card images, nothing else.

Card data and images: YGOPRODeck (ygoprodeck.com). Yu-Gi-Oh! is a trademark of Konami.
DuelLab is an unofficial fan tool.
