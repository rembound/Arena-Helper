# Arena Helper

Arena Helper is a plugin for [Hearthstone Deck Tracker](https://github.com/HearthSim/Hearthstone-Deck-Tracker) that helps drafting Hearthstone arena decks by showing an overlay with card values from a tier list. The plugin tries to detect the arena heroes and card choices. Detected cards are displayed alongside the value of the card, that is specified in [The Lightforge: Hearthstone Arena Tier List](http://thelightforge.com/TierList). The created deck can be saved to Hearthstone Deck Tracker. Check out the [How To Install](https://github.com/rembound/Arena-Helper#how-to-install) guide below to download and install the plugin.

![Arena Helper](images/arena-helper-4.png?raw=true)
Arena Helper uses [HearthMirror](https://github.com/HearthSim/HearthMirror) to extract the hero and card data from Hearthstone. Older versions of the plugin used perceptual hashing to detect the Hearthstone arena heroes and cards. The technique is based on the article [Looks Like It](http://www.hackerfactor.com/blog/?/archives/432-Looks-Like-It.html). Implementation details of a similar project can be found here: [Hearthstone Image Recognition](https://github.com/wittenbe/Hearthstone-Image-Recognition).

More technical information about how the plugin used image recognition and how it calculated the perceptual hashes can be found in my article [Arena Helper](http://rembound.com/projects/arena-helper).

If you like to support me and the continued development of this plugin, any donations are greatly appreciated. Thank you for your support!  
[![Donate](https://www.paypalobjects.com/en_US/i/btn/btn_donate_LG.gif)](https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=info%40rembound%2ecom&lc=NL&item_name=Rembound%2ecom&currency_code=USD&bn=PP%2dDonationsBF%3abtn_donate_LG%2egif%3aNonHosted)

## Plugins For Arena Helper

Arena Helper has support for plugins within the plugin. Plugins allow you to use the card detection system while drafting a Hearthstone arena deck for your own purposes. It also allows you to override the tier list values and can present an advice to the player. Check out my article [How To Write Plugins For Arena Helper](http://rembound.com/articles/how-to-write-plugins-for-arena-helper) to read a tutorial on how to create such a plugin. If you want to see how it works immediately, you can find a TestPlugin project in the latest source code. If you have made a plugin and want to be featured on this page, contact me.

Available plugins:
* [Hearth Arena Plugin (Unofficial)](https://github.com/corlettb/HDTAHPluginHAPlugin) by [@corlettb](https://github.com/corlettb)  
*(Note: This plugin is currently outdated and will not work)*

## How To Install

1. [Click here](https://github.com/rembound/Arena-Helper/releases) to download the latest ArenaHelper.vX.Y.Z.zip from the [releases page](https://github.com/rembound/Arena-Helper/releases).  
2. Unblock the zip file before unzipping, by [right-clicking it and choosing properties](http://blogs.msdn.com/b/delay/p/unblockingdownloadedfile.aspx):
![Unblock](images/unblock.png?raw=true)  
3. Make sure you remove any old versions of the ArenaHelper directory in the plugins directory of Hearthstone Deck Tracker completely, before upgrading versions.  
4. Unzip the archive to `%AppData%/HearthstoneDeckTracker/Plugins` To find this directory, you can click the following button in the Hearthstone Deck Tracker options menu: `Options -> Tracker -> Plugins -> Plugins Folder`
5. If you've done it correctly, the ArenaHelper directory should be inside the Plugins directory. Inside the ArenaHelper directory, there should be a bunch of files, including a file called ArenaHelper.dll.  
6. If the plugin is missing MSVCP120.dll, install the following Redistributable Package (Select vcredist_x86.exe):  
[Visual C++ Redistributable Packages for Visual Studio 2013](http://www.microsoft.com/en-us/download/details.aspx?id=40784)  
7. Launch Hearthstone Deck Tracker. Enable the plugin in `Options -> Tracker -> Plugins`.  
8. If it is not working you can enable a debug mode in the options window  
9. If all else fails, copy the dlls from the x86 directory to the C:/windows/SysWOW64 directory.

## How To Use

When you start a new arena run, open up the Arena Helper window from the plugins menu. Arena Helper will try to detect the arena window and the heroes that can be chosen.

The plugin has detected the heroes. Select a hero.

![Arena Helper](images/arena-helper-1.png?raw=true)

When you see that a detected hero becomes bigger in the Arena Helper window, you can confirm your selection.

![Arena Helper](images/arena-helper-2.png?raw=true)

If hero detection doesn't work, you can use the manual hero selection override by clicking on the top-left portrait rectangle.

![Arena Helper](images/arena-helper-hero-selection.png?raw=true)

Wait for the plugin to finish detecting the cards.

![Arena Helper](images/arena-helper-3.png?raw=true)

Arena Helper has detected the cards and displays the value from [The Lightforge: Hearthstone Arena Tier List](http://thelightforge.com/TierList) in the window and the overlay.

![Arena Helper](images/arena-helper-4.png?raw=true)

All cards are picked. The deck can be saved to Hearthstone Deck Tracker, without needing to use the Import function. The Arena Helper window can be closed. Make sure to check the deck for errors, because sometimes detection is not flawless.

![Arena Helper](images/arena-helper-5.png?raw=true)

All arena decks are saved in the AppData directory: HearthstoneDeckTracker\ArenaHelper\Decks
If the plugin made a mistake, you can override or reset the cards and card picks manually by editing the .json files in the decks directory.
The position of the Arena Helper window is saved automatically in a config file.
