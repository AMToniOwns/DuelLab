DuelLab for Windows
===================

Yu-Gi-Oh! deck building and opening-hand consistency analysis, running on your own PC.

Start
-----
1. Unzip this folder anywhere (for example Documents\DuelLab). Keep the files together.
2. Double-click DuelLab.exe. Your browser opens DuelLab at http://127.0.0.1:5180
   A small window stays open while DuelLab runs; close it to stop DuelLab.
3. The first start downloads the card list from YGOPRODeck (a few seconds; needs internet).

"Windows protected your PC"?
----------------------------
DuelLab is not signed with a paid certificate, so Windows may warn the first time.
Click "More info", then "Run anyway".

Your data
---------
Decks, simulations and downloaded card images are stored in
  %LOCALAPPDATA%\DuelLab
(paste that into the File Explorer address bar to open it).
- Back up: copy duellab.db from that folder.
- Update DuelLab: delete the old DuelLab folder and unzip the new one. Your decks stay.
- Uninstall: delete the DuelLab folder and %LOCALAPPDATA%\DuelLab.

Privacy
-------
DuelLab only listens on your own PC (127.0.0.1): nobody else on your network can open it.
It contacts YGOPRODeck to download card data and card images, nothing else.

Card data and images: YGOPRODeck (ygoprodeck.com). Yu-Gi-Oh! is a trademark of Konami.
DuelLab is an unofficial fan tool.
