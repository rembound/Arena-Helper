# Arena Helper

Arena Helper is a plugin for [Hearthstone Deck Tracker](https://github.com/Epix37/Hearthstone-Deck-Tracker) that helps drafting Hearthstone arena decks. The plugin tries to visually detect the arena heroes and card choices. Detected cards are displayed alongside the value of the card, that is specified in [ADWCTA's Arena Tier List](http://ggoatgaming.com/tierlist). The created deck can be saved to Hearthstone Deck Tracker.

The plugin uses perceptual hashing to detect the Hearthstone arena heroes and cards. The technique is based on the article [Looks Like It](http://www.hackerfactor.com/blog/?/archives/432-Looks-Like-It.html). Implementation details of a similar project can be found here: [Hearthstone Image Recognition](https://github.com/wittenbe/Hearthstone-Image-Recognition).

More technical information about how the plugin uses image recognition and how it calculates the perceptual hashes can be found in my article [Arena Helper](http://rembound.com/projects/arena-helper).

If you like to support me and the continued development of this plugin, any donations are greatly appreciated. Thank you for your support!  
[![Donate](https://www.paypalobjects.com/en_US/i/btn/btn_donate_LG.gif)](https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=info%40rembound%2ecom&lc=NL&item_name=Rembound%2ecom&currency_code=USD&bn=PP%2dDonationsBF%3abtn_donate_LG%2egif%3aNonHosted)

## Plugins For Arena Helper

Arena Helper now has support for plugins within the plugin. If you are a site owner that has an API or if you want to create a website that integrates with Hearthstone to automatically detect the Arena cards and present a value and possible advice to the player, you can create your own plugin. Check out my article [How To Write Plugins For Arena Helper](http://rembound.com/articles/how-to-write-plugins-for-arena-helper) to read a tutorial on how to create such a plugin. If you want to see how it works immediately, you can find a TestPlugin project in the latest source code.

Available plugins:
* [Hearth Arena Plugin (Unofficial)](https://github.com/corlettb/HDTAHPluginHAPlugin) by [@corlettb](https://github.com/corlettb)

## How to Install

1) Download the latest release from the [releases page](https://github.com/rembound/Arena-Helper/releases)  
2) Unblock the zip file before unzipping, by [right-clicking it and choosing properties](http://blogs.msdn.com/b/delay/p/unblockingdownloadedfile.aspx):
![Unblock](http://blogs.msdn.com/cfs-file.ashx/__key/CommunityServer-Blogs-Components-WeblogFiles/00-00-00-60-92-metablogapi/1425.FilePropertiesUnblock.png)  
3) Make sure you remove any old versions of the ArenaHelper directory in the plugins directory of Hearthstone Deck Tracker completely, before upgrading versions.  
4) Unzip the archive to the Plugins directory of Hearthstone Deck Tracker  
5) If you've done it correctly, the ArenaHelper directory should be inside the Plugins directory. Inside the ArenaHelper directory, there should be a bunch of files, including a file called ArenaHelper.dll.  
6) If the plugin is missing MSVCP120.dll, install the following Redistributable Package (Select vcredist_x86.exe):  
[Visual C++ Redistributable Packages for Visual Studio 2013](http://www.microsoft.com/en-us/download/details.aspx?id=40784)  
7) If it is not working you can enable a debug mode in the options window  
8) If all else fails, copy the dlls from the x86 directory to the C:/windows/SysWOW64 directory.

## How to use

When you start a new arena run, open up the Arena Helper window from the plugins menu. Arena Helper will try to detect the arena window and the heroes that can be chosen. Keep in mind that the plugin uses visual information. The plugin window can't overlap the heroes or cards that are in the center of the screen. Make your selection slowly and with a single click to allow the plugin to detect the cards. Hovering over the cards while the plugin is still detecting them, will interfere with the detection process.

The plugin has detected the heroes. Select a hero.

![Arena Helper](http://i.imgur.com/H4Of3ps.png)

When you see that a detected hero becomes bigger in the Arena Helper window, you can confirm your selection.

![Arena Helper](http://i.imgur.com/aMFJba9.png)

If hero detection doesn't work, you can use the manual hero selection override by clicking on the top-left portrait rectangle.

![Arena Helper](http://i.imgur.com/NLMyHbv.png)

Wait for the plugin to finish detecting the cards.

![Arena Helper](http://i.imgur.com/ShfMZnw.png)

Arena Helper has detected the cards and displays the value from [ADWCTA's Arena Tier List](http://ggoatgaming.com/tierlist) in the window and the overlay.

![Arena Helper](http://i.imgur.com/5G7qDQL.png)

All cards are picked. The deck can be saved to Hearthstone Deck Tracker, without needing to use the Import function. The Arena Helper window can be closed. Make sure to check the deck for errors, because sometimes detection is not flawless.

![Arena Helper](http://i.imgur.com/AnPaN4L.png)

All arena decks are saved in the AppData directory: HearthstoneDeckTracker\ArenaHelper\Decks
If the plugin made a mistake, you can override or reset the cards and card picks manually by editing the .json files in the decks directory.
The position of the Arena Helper window is saved automatically in a config file.
