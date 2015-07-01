using AHPlugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;

namespace AHPlugins
{
    public abstract class AHPlugin
    {
        public abstract string Name { get; }
        public abstract string Author { get; }
        public abstract Version Version { get; }
        public abstract bool IsAsync { get; }

        // All virtual functions are optional

        // Called when three new cards are detected
        // arenadata: The previously detected cards, picked cards and heroes
        // newcards: List of 3 detected cards
        // defaultvalues: List of 3 tier values for the detected cards
        // Return a list of 3 card values and an optional 4th advice value
        public virtual List<string> GetCardValues(ArenaHelper.Plugin.ArenaData arenadata, List<Card> newcards, List<string> defaultvalues) { return null; }
        public virtual Task<List<string>> GetCardValuesAsync(ArenaHelper.Plugin.ArenaData arenadata, List<Card> newcards, List<string> defaultvalues) { return Task.FromResult<List<string>>(null); }

        // Called when a new arena is started
        // arendata: As before
        public virtual void NewArena(ArenaHelper.Plugin.ArenaData arenadata) { }
        public virtual Task NewArenaAsync(ArenaHelper.Plugin.ArenaData arenadata) { return Task.FromResult(true); }

        // Called when the heroes are detected
        // arendata: As before
        // heroname0: name of hero 0
        // heroname1: name of hero 1
        // heroname2: name of hero 2
        public virtual void HeroesDetected(ArenaHelper.Plugin.ArenaData arenadata, string heroname0, string heroname1, string heroname2) { }
        public virtual Task HeroesDetectedAsync(ArenaHelper.Plugin.ArenaData arenadata, string heroname0, string heroname1, string heroname2) { return Task.FromResult(true); }

        // Called when a hero is picked
        // arendata: As before
        // heroname: name of the hero
        public virtual void HeroPicked(ArenaHelper.Plugin.ArenaData arenadata, string heroname) { }
        public virtual Task HeroPickedAsync(ArenaHelper.Plugin.ArenaData arenadata, string heroname) { return Task.FromResult(true); }

        // Called when the cards are detected
        // arendata: As before
        // card0: card 0
        // card1: card 1
        // card2: card 2
        public virtual void CardsDetected(ArenaHelper.Plugin.ArenaData arenadata, Card card0, Card card1, Card card2) { }
        public virtual Task CardsDetectedAsync(ArenaHelper.Plugin.ArenaData arenadata, Card card0, Card card1, Card card2) { return Task.FromResult(true); }

        // Called when a card is picked
        // arendata: As before
        // pickindex: index of the picked card in the range -1 to 2, if -1, no valid pick was detected
        // card: card information, null if invalid card
        public virtual void CardPicked(ArenaHelper.Plugin.ArenaData arenadata, int pickindex, Card card) { }
        public virtual Task CardPickedAsync(ArenaHelper.Plugin.ArenaData arenadata, int pickindex, Card card) { return Task.FromResult(true); }

        // Called when all cards are picked
        // arendata: As before
        public virtual void Done(ArenaHelper.Plugin.ArenaData arenadata) { }
        public virtual Task DoneAsync(ArenaHelper.Plugin.ArenaData arenadata) { return Task.FromResult(true); }

        // Called when Arena Helper window is opened
        // arendata: As before
        // state: the current state of Arena Helper
        public virtual void ResumeArena(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state) { }
        public virtual Task ResumeArenaAsync(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state) { return Task.FromResult(true); }

        // Called when Arena Helper window is closed
        // arendata: As before
        // state: the current state of Arena Helper
        public virtual void CloseArena(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state) { }
        public virtual Task CloseArenaAsync(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state) { return Task.FromResult(true); }
    }
}

namespace ArenaHelper
{
    class Plugins
    {
        List<AHPlugin> plugins = new List<AHPlugin>();

        public Plugins()
        {
        }

        public void LoadPlugins()
        {
            plugins.Clear();

            // Find dlls
            string assemblylocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string pluginpath = Path.Combine(assemblylocation, "plugins");

            string[] dllFileNames = null;
            if (Directory.Exists(pluginpath))
            {
                dllFileNames = Directory.GetFiles(pluginpath, "*.dll", SearchOption.AllDirectories);
            }
            else
            {
                return;
            }

            if (dllFileNames.Length <= 0)
            {
                return;
            }

            // Load assemblies
            ICollection<Assembly> assemblies = new List<Assembly>(dllFileNames.Length);
            foreach (string dllFile in dllFileNames)
            {
                //Logger.WriteLine("Loading plugin: " + dllFile);
                AssemblyName an = AssemblyName.GetAssemblyName(dllFile);
                Assembly assembly = Assembly.Load(an);
                assemblies.Add(assembly);
            }

            // Find valid plugins
            Type pluginType = typeof(AHPlugin);
            ICollection<Type> pluginTypes = new List<Type>();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly != null)
                {
                    Type[] types = assembly.GetTypes();
                    foreach (Type type in types)
                    {
                        if (type.IsInterface || type.IsAbstract)
                        {
                            continue;
                        }
                        else
                        {
                            if (type.IsSubclassOf(pluginType))
                            {
                                pluginTypes.Add(type);
                            }
                        }
                    }
                }
            }

            // Create instances
            foreach (Type type in pluginTypes)
            {
                AHPlugin plugin = (AHPlugin)Activator.CreateInstance(type);
                plugins.Add(plugin);
                Logger.WriteLine("Found: " + plugin.Name);
            }
        }

        public async Task<List<string>> GetCardValues(ArenaHelper.Plugin.ArenaData arenadata, List<Card> newcards, List<string> defaultvalues)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.IsAsync)
                {
                    return await plugin.GetCardValuesAsync(arenadata, newcards, defaultvalues);
                }
                return plugin.GetCardValues(arenadata, newcards, defaultvalues);
            }
            return null;
        }

        public async Task NewArena(ArenaHelper.Plugin.ArenaData arenadata)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.IsAsync)
                {
                    await plugin.NewArenaAsync(arenadata);
                    return;
                }
                plugin.NewArena(arenadata);
                return;
            }
        }

        public async Task HeroesDetected(ArenaHelper.Plugin.ArenaData arenadata, string heroname0, string heroname1, string heroname2)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.IsAsync)
                {
                    await plugin.HeroesDetectedAsync(arenadata, heroname0, heroname1, heroname2);
                    return;
                }
                plugin.HeroesDetected(arenadata, heroname0, heroname1, heroname2);
                return;
            }
        }

        public async Task HeroPicked(ArenaHelper.Plugin.ArenaData arenadata, string heroname)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.IsAsync)
                {
                    await plugin.HeroPickedAsync(arenadata, heroname);
                    return;
                }
                plugin.HeroPicked(arenadata, heroname);
                return;
            }
        }

        public async Task CardsDetected(ArenaHelper.Plugin.ArenaData arenadata, Card card0, Card card1, Card card2)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.IsAsync)
                {
                    await plugin.CardsDetectedAsync(arenadata, card0, card1, card2);
                    return;
                }
                plugin.CardsDetected(arenadata, card0, card1, card2);
                return;
            }
        }

        public async Task CardPicked(ArenaHelper.Plugin.ArenaData arenadata, int pickindex, Card card)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.IsAsync)
                {
                    await plugin.CardPickedAsync(arenadata, pickindex, card);
                    return;
                }
                plugin.CardPicked(arenadata, pickindex, card);
                return;
            }
        }

        public async Task Done(ArenaHelper.Plugin.ArenaData arenadata)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.IsAsync)
                {
                    await plugin.DoneAsync(arenadata);
                    return;
                }
                plugin.Done(arenadata);
                return;
            }
        }

        public async Task ResumeArena(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.IsAsync)
                {
                    await plugin.ResumeArenaAsync(arenadata, state);
                    return;
                }
                plugin.ResumeArena(arenadata, state);
                return;
            }
        }

        public async Task CloseArena(ArenaHelper.Plugin.ArenaData arenadata, ArenaHelper.Plugin.PluginState state)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.IsAsync)
                {
                    await plugin.CloseArenaAsync(arenadata, state);
                    return;
                }
                plugin.CloseArena(arenadata, state);
                return;
            }
        }
    }
}
