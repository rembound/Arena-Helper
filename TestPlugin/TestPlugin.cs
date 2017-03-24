using AHPlugins;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;

// Ignore the warning, not every function needs an 'await'
#pragma warning disable 1998

// A test plugin that does something when cards are detected
// Reference ArenaHelper.dll and Hearthstone Deck Tracker
// Place the dll in ArenaHelper/plugins/ in the HDT plugins directory
// Only one plugin can be activated at a time
namespace TestPlugin
{
    public class TestPlugin : AHPlugin
    {
        private SemaphoreSlim mutex = new SemaphoreSlim(1);

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
            get { return new Version("0.0.5"); }
        }

        public TestPlugin()
        {
            // Plugin constructor
            // Setup stuff
            Log.Info("TestPlugin constructor");
        }

        // Called when three new cards are detected
        // arenadata: The previously detected cards, picked cards and heroes
        // newcards: List of 3 detected cards
        // defaultvalues: List of 3 tier values for the detected cards
        // Return a list of 3 card values and an optional 4th advice value
        public override async Task<List<double>> GetCardValues(ArenaHelper.Plugin.ArenaData arenadata, List<Card> newcards, List<double> defaultvalues)
        {
            List<double> values = new List<double>();

            // Add a test delay to simulate an API call
            await Task.Delay(1000);

            // Add the three card values
            for (int i = 0; i < 3; i++)
            {
                // Add the prefix "p" to the default values as a test
                values.Add(defaultvalues[i]);
            }

            return values;
        }

        // Called when three new cards are detected
        // arenadata: The previously detected cards, picked cards and heroes
        // newcards: List of 3 detected cards
        // defaultvalues: List of 3 tier values for the detected cards
        // Returns advice to diplay. Can be null.
        public override async Task<string> GetCardAdvice(ArenaHelper.Plugin.ArenaData arenadata, List<Card> newcards, List<double> defaultvalues)
        {
            string advice;

            // Add a test delay to simulate an API call
            await Task.Delay(400);

            // Set the advice
            advice = "This is good advice.";

            return advice;
        }

        // Called when a new arena is started
        // arendata: As before
        public override async void NewArena(ArenaHelper.Plugin.ArenaData arenadata)
        {
            // Do something with the information
            Log.Info("New Arena: " + arenadata.deckname);
        }

        // Called when the heroes are detected
        // arendata: As before
        // heroname0: name of hero 0
        // heroname1: name of hero 1
        // heroname2: name of hero 2
        public override async void HeroesDetected(ArenaHelper.Plugin.ArenaData arenadata, string heroname0, string heroname1, string heroname2)
        {
            // Do something with the information
            Log.Info("Heroes Detected: " + heroname0 + ", " + heroname1 + ", " + heroname2);
        }

        // Called when a hero is picked
        // arendata: As before
        // heroname: name of the hero
        public override async void HeroPicked(ArenaHelper.Plugin.ArenaData arenadata, string heroname)
        {
            // Do something with the information
            Log.Info("Hero Picked: " + heroname);
        }

        // Called when the cards are detected
        // arendata: As before
        // card0: card 0
        // card1: card 1
        // card2: card 2
        public override async void CardsDetected(ArenaHelper.Plugin.ArenaData arenadata, Card card0, Card card1, Card card2)
        {
            // Do something with the information
            Log.Info("Cards Detected: " + card0.Name + ", " + card1.Name + ", " + card2.Name);
        }

        // Called when a card is picked
        // arendata: As before
        // pickindex: index of the picked card in the range -1 to 2, if -1, no valid pick was detected
        // card: card information, null if invalid card
        public override async void CardPicked(ArenaHelper.Plugin.ArenaData arenadata, int pickindex, Card card)
        {
            // Ensure cards are added sequentially
            await mutex.WaitAsync();
            try
            {
                // Do something with the information
                string cardname = "";
                if (card != null)
                {
                    cardname = card.Name;
                }

                int cardcount = arenadata.pickedcards.Count;
                await Task.Delay(1000);

                // Be careful when manipulating values on the ArenaData as they might have changed while making your API calls
                bool changed = cardcount != arenadata.pickedcards.Count;

                Log.Info("Card Picked: " + cardname);
            }
            finally
            {
                mutex.Release();
            }
        }

        // Called when all cards are picked
        // arendata: As before
        public override async void Done(ArenaHelper.Plugin.ArenaData arenadata)
        {
            // Do something with the information
            Log.Info("Done");
        }

        // Called when Arena Helper window is opened
        // arendata: As before
        // state: the current state of Arena Helper
        public override async void ResumeArena(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state)
        {
            Log.Info("Resuming Arena");
            foreach (var cardid in arenadata.pickedcards)
            {
                Card card = ArenaHelper.Plugin.GetCard(cardid);
                string cardname = "-";
                if (card != null)
                {
                    cardname = card.Name;
                }
                Log.Info(cardname);
            }

            foreach (var heroname in arenadata.detectedheroes)
            {
                ArenaHelper.Plugin.HeroHashData hero = ArenaHelper.Plugin.GetHero(heroname);
                Log.Info("Detected hero: " + hero.name);
            }

            if (arenadata.pickedhero != null)
            {
                ArenaHelper.Plugin.HeroHashData hero = ArenaHelper.Plugin.GetHero(arenadata.pickedhero.ToString());
                Log.Info("Picked hero: " + hero.name);
            }

            Log.Info("State: " + ArenaHelper.Plugin.GetState().ToString());
        }

        // Called when Arena Helper window is closed
        // arendata: As before
        public override async void CloseArena(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state)
        {
            // Closing the window, to maybe resume at a later time
            Log.Info("Closing");
        }
    }
}