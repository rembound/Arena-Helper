# Arena Helper

Arena Helper is a plugin for [Hearthstone Deck Tracker](https://github.com/Epix37/Hearthstone-Deck-Tracker) that helps drafting Hearthstone arena decks. The plugin tries to visually detect the arena heroes and card choices. Detected cards are displayed alongside the value of the card, that is specified in [ADWCTA's Arena Tier List](http://www.heartharena.com/tierlist). The created deck can be saved to Hearthstone Deck Tracker.

The plugin uses perceptual hashing to detect the Hearthstone arena heroes and cards. The technique is based on the article [Looks Like It](http://www.hackerfactor.com/blog/?/archives/432-Looks-Like-It.html). Implementation details of a similar project can be found here: [Hearthstone Image Recognition](https://github.com/wittenbe/Hearthstone-Image-Recognition).

## How to Install

1) Download the latest release from the [releases page](https://github.com/rembound/Arena-Helper/releases)
2) Unzip the archive to the Plugins directory of Hearthstone Deck Tracker
3) If you've done it correctly, the ArenaHelper directory should be inside the Plugins directory. Inside the ArenaHelper directory, there should be a bunch of files, including a file called ArenaHelper.dll.

## How to use

When you start a new arena run, open up the Arena Helper window from the plugins menu. Arena Helper will try to detect the arena window and the heroes that can be chosen. Keep in mind that the plugin uses visual information. The plugin window can't overlap the heroes or cards that are in the center of the screen. Make your selection slowly and with a single click to allow the plugin to detect the cards. Hovering over the cards while the plugin is still detecting them, will interfere with the detection process.

The plugin has detected the heroes. Select a hero.

![Arena Helper](http://i.imgur.com/H4Of3ps.png)

When you see that a detected hero becomes bigger in the Arena Helper window, you can confirm your selection.

![Arena Helper](http://i.imgur.com/aMFJba9.png)

Wait for the plugin to finish detecting the cards.

![Arena Helper](http://i.imgur.com/ShfMZnw.png)

Arena Helper has detected the cards and displays the value from [ADWCTA's Arena Tier List](http://www.heartharena.com/tierlist).

![Arena Helper](http://i.imgur.com/olNe9D7.png)

All cards are picked. The deck can be saved to Hearthstone Deck Tracker, without needing to use the Import function. The Arena Helper window can be closed.

![Arena Helper](http://i.imgur.com/AnPaN4L.png)

All arena decks are saved in the AppData directory: HearthstoneDeckTracker\ArenaHelper\Decks
If the plugin made a mistake, you can override or reset the cards and card picks manually by editing the .json files in the decks directory.
The position of the Arena Helper window is saved automatically in a config file.