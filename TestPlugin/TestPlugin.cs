using AHPlugins;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;
using System;
using System.Collections.Generic;

// A test plugin that does something when cards are detected
// Reference ArenaHelper.dll and Hearthstone Deck Tracker
// Place the dll in ArenaHelper/plugins/ in the HDT plugins directory
// Only one plugin can be activated at a time
namespace TestPlugin
{
    public class TestPlugin : AHPlugin
    {
        public override string Name
        {
            get { return "TestPlugin"; }
        }

        public override string Author
        {
            get { return "Rembound.com"; }
        }

        public override Version Version
        {
            get { return new Version("0.0.4"); }
        }

        public TestPlugin()
        {
            // Plugin constructor
            // Setup stuff
            Logger.WriteLine("TestPlugin constructor");
        }

        // Called when three new cards are detected
        // arenadata: The previously detected cards, picked cards and heroes
        // newcards: List of 3 detected cards
        // defaultvalues: List of 3 tier values for the detected cards
        // Return a list of 3 card values and an optional 4th advice value
        public override List<string> GetCardValues(ArenaHelper.Plugin.ArenaData arenadata, List<Card> newcards, List<string> defaultvalues)
        {
            List<string> values = new List<string>();

            // Add the three card values
            for (int i = 0; i < 3; i++)
            {
                // Add the prefix "p" to the default values as a test
                values.Add("p" + defaultvalues[i]);
            }

            // Optionally add an advice as a 4th list element
            values.Add("I don't know, pick one!");

            return values;
        }

        // Called when a new arena is started
        // arendata: As before
        public override void NewArena(ArenaHelper.Plugin.ArenaData arenadata)
        {
            // Do something with the information
            Logger.WriteLine("New Arena: " + arenadata.deckname);
        }

        // Called when the heroes are detected
        // arendata: As before
        // heroname0: name of hero 0
        // heroname1: name of hero 1
        // heroname2: name of hero 2
        public override void HeroesDetected(ArenaHelper.Plugin.ArenaData arenadata, string heroname0, string heroname1, string heroname2)
        {
            // Do something with the information
            Logger.WriteLine("Heroes Detected: " + heroname0 + ", " + heroname1 + ", " + heroname2);
        }

        // Called when a hero is picked
        // arendata: As before
        // heroname: name of the hero
        public override void HeroPicked(ArenaHelper.Plugin.ArenaData arenadata, string heroname)
        {
            // Do something with the information
            Logger.WriteLine("Hero Picked: " + heroname);
        }

        // Called when the cards are detected
        // arendata: As before
        // card0: card 0
        // card1: card 1
        // card2: card 2
        public override void CardsDetected(ArenaHelper.Plugin.ArenaData arenadata, Card card0, Card card1, Card card2)
        {
            // Do something with the information
            Logger.WriteLine("Cards Detected: " + card0.Name + ", " + card1.Name + ", " + card2.Name);
        }

        // Called when a card is picked
        // arendata: As before
        // pickindex: index of the picked card in the range -1 to 2, if -1, no valid pick was detected
        // card: card information, null if invalid card
        public override void CardPicked(ArenaHelper.Plugin.ArenaData arenadata, int pickindex, Card card)
        {
            // Do something with the information
            string cardname = "";
            if (card != null)
            {
                cardname = card.Name;
            }
            Logger.WriteLine("Card Picked: " + cardname);
        }

        // Called when all cards are picked
        // arendata: As before
        public override void Done(ArenaHelper.Plugin.ArenaData arenadata)
        {
            // Do something with the information
            Logger.WriteLine("Done");
        }

        // Called when Arena Helper window is opened
        // arendata: As before
        // state: the current state of Arena Helper
        public override void ResumeArena(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state)
        {
            Logger.WriteLine("Resuming Arena");
            foreach (var cardid in arenadata.pickedcards)
            {
                Card card = ArenaHelper.Plugin.GetCard(cardid);
                string cardname = "-";
                if (card != null)
                {
                    cardname = card.Name;
                }
                Logger.WriteLine(cardname);
            }

            foreach (var heroname in arenadata.detectedheroes)
            {
                ArenaHelper.Plugin.HeroHashData hero = ArenaHelper.Plugin.GetHero(heroname);
                Logger.WriteLine("Detected hero: " + hero.name);
            }

            if (arenadata.pickedhero != "")
            {
                ArenaHelper.Plugin.HeroHashData hero = ArenaHelper.Plugin.GetHero(arenadata.pickedhero);
                Logger.WriteLine("Picked hero: " + hero.name);
            }

            Logger.WriteLine("State: " + ArenaHelper.Plugin.GetState().ToString());
        }

        // Called when Arena Helper window is closed
        // arendata: As before
        public override void CloseArena(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state)
        {
            // Closing the window, to maybe resume at a later time
            Logger.WriteLine("Closing");
        }
    }
}