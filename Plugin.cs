using ArenaHelper.CardInfo;
using ArenaHelper.Enums;
using HearthMirror;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Logging;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ArenaHelper
{
    public class Plugin: IPlugin
    {

        public class ConfigData
        {
            public int windowx;
            public int windowy;
            public bool manualclicks;
            public bool overlay;
            public bool debug;
            public bool autosave;
            public bool updated;

            public ConfigData()
            {
                ResetWindow();
                manualclicks = false;
                overlay = true;
                debug = false;
                autosave = false;
                updated = false;
            }

            public void ResetWindow()
            {
                windowx = 100;
                windowy = 100;
            }
        }

        public class HashData
        {
            public List<ulong> hashes;

            public HashData(params ulong[] hashes)
            {
                this.hashes = new List<ulong>();

                // Store hashes
                for (int i = 0; i < hashes.Length; i++)
                {
                    this.hashes.Add(hashes[i]);
                }
            }
        }

        public class CardHashData : HashData
        {
            public string id;

            public CardHashData(string id, ulong hash)
                : base(hash)
            {
                this.id = id;
            }
        }

        public class HeroHashData : HashData
        {
            public int index;
            public string name;
            public string image;

            public HeroHashData(int index, string name, string image, params ulong[] hashes)
                : base(hashes)
            {
                this.index = index;
                this.name = name;
                this.image = image;
            }
        }

        public class ArenaData
        {
            public string deckname;
            public string deckguid;
            public List<string> detectedheroes;
            public HeroClass? pickedhero;
            public List<Tuple<string, string, string>> detectedcards;
            public List<Tuple<double, double, double>> cardrating;
            public List<string> pickedcards;

            public ArenaData()
            {
                deckname = "";
                deckguid = "";
                detectedheroes = new List<string>();
                pickedhero = null;
                detectedcards = new List<Tuple<string, string, string>>();
                cardrating = new List<Tuple<double, double, double>>();
                pickedcards = new List<string>();
            }
        }

        public enum PluginState { Idle, SearchHeroes, SearchBigHero, DetectedHeroes, SearchCards, SearchCardValues, DetectedCards, Done };

        private List<Detection.DetectedInfo> detectedcards = new List<Detection.DetectedInfo>();
        private List<Detection.DetectedInfo> detectedheroes = new List<Detection.DetectedInfo>();
        private List<Detection.DetectedInfo> detectedbighero = new List<Detection.DetectedInfo>();
        private List<double> currentcardvalues = new List<double>();
        private static PluginState state;
        private List<int> mouseindex = new List<int>();
        private ArenaData arenadata = new ArenaData();
        private string currentfilename = "";
        private ConfigData configdata = new ConfigData();
        private bool configinit = false;

        private List<Controls.ValueOverlay> valueoverlays = new List<Controls.ValueOverlay>();
        private Controls.AdviceOverlay adviceoverlay = null;
        private Controls.DebugTextBlock debugtext = null;
        private List<System.Windows.Controls.Image> debugimages = new List<System.Windows.Controls.Image>();

        private Update.AHDataVersion dataversion;
        private const string DataVersionFile = "version.json";
        private const string HashListFile = "cardhashes.json";
        private const string TierListFile = "tierlist.json";
        public static List<Card> cardlist = new List<Card>();
        private List<CardHashData> cardhashlist = new List<CardHashData>();
        public static List<HeroHashData> herohashlist = new List<HeroHashData>();
        public static Dictionary<string,CardInfo.CardTierInfo> cardtierlist = new Dictionary<string, CardInfo.CardTierInfo>();

        private Detection detection = new Detection();

        // Arena detection
        bool inarena = false;
        bool stablearena = false;
        Stopwatch arenastopwatch;

        // Configure heroes
        bool configurehero = false;

        // Log reader
        private DateTime loglasttime = DateTime.MinValue;
        private string loglastline = "";
        private DateTime loglastchoice = DateTime.MinValue;
        private string loglastheroname = "";
        private string loglastcardid = "";
        //"DraftManager.OnChosen(): hero=HERO_03 premium=STANDARD"
        public static readonly Regex HeroChosenRegex = new Regex(@"DraftManager\.OnChosen\(\): hero=(?<id>(.+)) .*");

        // HearthMirror
        private bool usehearthmirror = true;
        private List<Card> previouscards = new List<Card>();
        private bool samecarddelay;
        private DateTime samecardtime = DateTime.MinValue;
        private const int samecardmaxtime = 4000;

        // Updates
        private DateTime lastpluginupdatecheck = DateTime.MinValue;
        private Update.GithubRelease latestrelease = null;
        private bool haspluginupdates = false;
        private TimeSpan pluginupdatecheckinterval = TimeSpan.FromHours(1);
        private bool showingupdatemessage = false;
        private DateTime lastdataupdatecheck = DateTime.MinValue;
        private bool hasdataupdates = false;
        private TimeSpan dataupdatecheckinterval = TimeSpan.FromHours(1);

        Stopwatch stopwatch;

        protected MenuItem MainMenuItem { get; set; }
        internal static ArenaWindow arenawindow = null;
        
        private SemaphoreSlim mutex = new SemaphoreSlim(1);

        private const int ArenaDetectionTime = 750;
        private const int MaxCardCount = 30;
        private const int HeroConfirmations = 3;
        private const int CardConfirmations = 3;
        private const int BigHeroConfirmations = 0; // Confirm immediately
        private const string DetectingArena = "Detecting arena...";
        private const string DetectingHeroes = "Detecting heroes...";
        private const string DetectingCards = "Detecting cards...";
        private const string DetectingValues = "Getting values...";
        private const string DetectionWarning = "Please make sure nothing overlaps the arena heroes and cards and the Hearthstone window has the focus. Don't make a selection yet!";
        private const string DoneMessage = "All cards are picked. You can start a new arena run or save the deck.";
        private const string ConfigFile = "arenahelper.json";

        private Plugins plugins = new Plugins();

        private string DataDir
        {
            get { return Path.Combine(Config.Instance.DataDir, "ArenaHelper"); }
        }

        private string DataDataDir
        {
            get { return Path.Combine(Config.Instance.DataDir, "ArenaHelper", "Data"); }
        }

        private string DeckDataDir
        {
            get { return Path.Combine(Config.Instance.DataDir, "ArenaHelper", "Decks"); }
        }

        public string Name
        {
            get { return "Arena Helper"; }
        }

        public string Description
        {
            get { return "Arena Helper is a plugin for Hearthstone Deck Tracker that tries to detect heroes and cards when drafting a Hearthstone arena deck. Detected cards are displayed alongside the value of the card, that is specified in ADWCTA's Arena Tier List. The created deck can be saved to Hearthstone Deck Tracker.\n\nFor more information and updates, check out:\nhttps://github.com/rembound/Arena-Helper\nhttp://rembound.com"; }
        }

        public string ButtonText
        {
            get { return "Arena Helper"; }
        }

        public string Author
        {
            get { return "Rembound.com"; }
        }

        public Version Version
        {
            get { return new Version("0.8.5"); }
        }

        public MenuItem MenuItem
        {
            get { return MainMenuItem; }
        }

        public void OnLoad()
        {
            try
            {
                // Load plugins
                plugins.LoadPlugins();

                state = PluginState.Idle;

                // Set hashes
                herohashlist.Clear();
                herohashlist.Add(new HeroHashData(0, "Druid", "druid_small.png", 5433711186487002743));
                herohashlist.Add(new HeroHashData(1, "Hunter", "hunter_small.png", 2294799430464257123, 1975465933826505957, 813537221374590069, 12942361696967163803, 17552924014479703963)); // Rexxar, Rexxar golden small, Rexxar golden big, Alleria small, Alleria big
                herohashlist.Add(new HeroHashData(2, "Mage", "mage_small.png", 15770007155810004267, 8631746754340092973, 8343516378188643373, 4200442357624021949, 6507833410481791487)); // Jaina, Medivh small, Medivh big, Khadgar small, Khadgar big
                herohashlist.Add(new HeroHashData(3, "Rogue", "rogue_small.png", 5643619427529904809, 11263619176753353643, 10111770795730096795)); // Valeera, Valeera golden small, Valeera golden big
                herohashlist.Add(new HeroHashData(4, "Paladin", "paladin_small.png", 11505795398351105139, 11848119152778072427, 11846995451926976873)); // Uther Lightbringer, Lady Liadrin small, Lady Liadrin big
                herohashlist.Add(new HeroHashData(5, "Priest", "priest_small.png", 15052908377040876499));
                herohashlist.Add(new HeroHashData(6, "Shaman", "shaman_small.png", 18366959783178990451, 15841665550811421911, 15769608231248747991)); // Thrall, Morgl small, Morgl big
                herohashlist.Add(new HeroHashData(7, "Warlock", "warlock_small.png", 10186248321718481641));
                herohashlist.Add(new HeroHashData(8, "Warrior", "warrior_small.png", 13776678289873991291, 10236917153841177209, 15929423101315404409, 13071189497635732127, 13237968094529645459)); // Garrosh, Garrosh golden small, Garrosh golden big, Magni small, Magni big

                AddMenuItem();

                stopwatch = Stopwatch.StartNew();

                // Init card list
                List<Card> cards = Database.GetActualCards();
                foreach (var card in cards)
                {
                    // Add to the list
                    cardlist.Add((Card)card.Clone());
                }

                // Load data
                LoadData();

                // Add log events
                Hearthstone_Deck_Tracker.API.LogEvents.OnArenaLogLine.Add(OnArenaLogLine);
            }
            catch (Exception e)
            {
                string errormsg = "OnLoad Error: " + e.Message + "\n" + e.ToString();
                Log.Info(errormsg);
            }
        }

        private void AddMenuItem()
        {
            MainMenuItem = new MenuItem()
            {
                Header = "Arena Helper"
            };

            MainMenuItem.Click += (sender, args) =>
            {
                ActivateArenaWindow();
            };
        }

        private void ActivateArenaWindow()
        {
            try
            {
                if (arenawindow == null)
                {
                    InitializeMainWindow();

                    // Show about page when auto updated
                    if (configdata.updated)
                    {
                        // Clean auto updater
                        Update.CleanAutoUpdate();

                        arenawindow.FlyoutAbout.IsOpen = true;
                        configdata.updated = false;
                        SaveConfig();
                    }
                    arenawindow.Show();
                }
                else
                {
                    // Reset window position when reactivating
                    arenawindow.Left = 100;
                    arenawindow.Top = 100;
                    arenawindow.WindowState = System.Windows.WindowState.Normal;
                    arenawindow.Activate();
                }
            }
            catch (Exception e)
            {
                string errormsg = "ActivateArenaWindow: " + e.Message + "\n" + e.ToString();
                Log.Info(errormsg);
            }

        }

        protected void InitializeMainWindow()
        {
            if (arenawindow == null)
            {
                arenawindow = new ArenaWindow();
                arenawindow.StringVersion = "v" + Version.ToString();

                // Load config
                LoadConfig();
                AddElements();
                ApplyConfig();
                arenawindow.Closed += (sender, args) =>
                {
                    plugins.CloseArena(arenadata, state);

                    SaveConfig();
                    RemoveElements();
                    arenawindow = null;
                    configinit = false;
                };

                // Init
                InitConfigureHero();

                arenawindow.onbuttonnewarenaclick = new ArenaWindow.OnEvent(OnButtonNewArenaClick);
                arenawindow.onbuttonsaveclick = new ArenaWindow.OnEvent(OnButtonSaveClick);
                arenawindow.onwindowlocation = new ArenaWindow.OnEvent(OnWindowLocation);

                arenawindow.onheroclick = new ArenaWindow.OnOverrideClick(OnHeroClick);
                arenawindow.oncardclick = new ArenaWindow.OnOverrideClick(OnCardClick);
                arenawindow.onconfigurehero = new ArenaWindow.OnEvent(OnConfigureHero);
                arenawindow.oncheroclick = new ArenaWindow.OnOverrideClick(OnCHeroClick);

                arenawindow.oncheckboxoverlay = new ArenaWindow.OnCheckbox(OnCheckboxOverlay);
                arenawindow.oncheckboxmanual = new ArenaWindow.OnCheckbox(OnCheckboxManual);
                arenawindow.oncheckboxautosave = new ArenaWindow.OnCheckbox(OnCheckboxAutoSave);
                arenawindow.oncheckboxdebug = new ArenaWindow.OnCheckbox(OnCheckboxDebug);

                arenawindow.onupdatedownloadclick = new ArenaWindow.OnEvent(OnUpdateDownloadClick);

                // Get the latest arena data
                string newestfilename = "";
                if (Directory.Exists(DeckDataDir))
                {
                    var newest = Directory.GetFiles(DeckDataDir).Select(x => new FileInfo(x)).OrderByDescending(x => x.CreationTime).FirstOrDefault();
                    if (newest != null)
                    {
                        newestfilename = newest.FullName;
                    }
                }
                LoadArenaData(newestfilename);
            }
        }

        private void InitConfigureHero()
        {
            configurehero = false;

            SetHeroControl(arenawindow.CHero0, herohashlist[0].name);
            SetHeroControl(arenawindow.CHero1, herohashlist[1].name);
            SetHeroControl(arenawindow.CHero2, herohashlist[2].name);
            SetHeroControl(arenawindow.CHero3, herohashlist[3].name);
            SetHeroControl(arenawindow.CHero4, herohashlist[4].name);
            SetHeroControl(arenawindow.CHero5, herohashlist[5].name);
            SetHeroControl(arenawindow.CHero6, herohashlist[6].name);
            SetHeroControl(arenawindow.CHero7, herohashlist[7].name);
            SetHeroControl(arenawindow.CHero8, herohashlist[8].name);

            arenawindow.CHero9.HeroName.Text = "Cancel";
            arenawindow.Update();
        }

        private void LoadConfig()
        {
            string filename = Path.Combine(DataDir, ConfigFile);
            if (File.Exists(filename))
            {
                try
                {
                    // Load the data
                    ConfigData loadedconfigdata = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(filename));

                    if (loadedconfigdata != null)
                    {
                        // Set data
                        configdata = loadedconfigdata;

                        // Fix window position for legacy configs
                        if (!(configdata.windowx > -32000 && configdata.windowy > -32000))
                        {
                            configdata.ResetWindow();
                        }

                        // Set window position
                        arenawindow.Left = configdata.windowx;
                        arenawindow.Top = configdata.windowy;
                    }
                    else
                    {
                        Log.Info("Arena Helper: Error loading config, null");
                    }
                }
                catch (Exception e)
                {
                    Log.Info("Arena Helper: Error loading config");
                }
            }

            // Set options
            arenawindow.CheckBoxOverlay.IsChecked = configdata.overlay;
            arenawindow.CheckBoxManual.IsChecked = configdata.manualclicks;
            arenawindow.CheckBoxAutoSave.IsChecked = configdata.autosave;
            arenawindow.CheckBoxDebug.IsChecked = configdata.debug;

            configinit = true;
        }

        private void ApplyConfig()
        {
            // Debug
            if (configdata.debug)
            {
                debugtext.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                debugtext.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        private void ShowOverlay(bool show)
        {
            ShowValueOverlay(show);
            ShowAdviceOverlay(show);
        }

        private void ShowValueOverlay(bool show)
        {
            System.Windows.Visibility vis = System.Windows.Visibility.Hidden;
            if (show)
            {
                vis = System.Windows.Visibility.Visible;
            }

            foreach (var overlay in valueoverlays)
            {
                overlay.Visibility = vis;
            }
        }

        private void ShowAdviceOverlay(bool show)
        {
            System.Windows.Visibility vis = System.Windows.Visibility.Hidden;
            if (show)
            {
                vis = System.Windows.Visibility.Visible;
            }

            adviceoverlay.Visibility = vis;
        }

        private void SaveConfig()
        {
            string filename = Path.Combine(DataDir, ConfigFile);
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);

            string json = JsonConvert.SerializeObject(configdata, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filename, json);
        }

        public void OnButtonNewArenaClick()
        {
            NewArena();
            SaveArenaData();
            plugins.NewArena(arenadata);
        }

        public void OnButtonSaveClick()
        {
            SaveDeck(false);
        }

        public void OnHeroClick(int index)
        {
            // Override detection
            if (configdata.manualclicks)
            {

            }
        }

        public void OnCardClick(int index)
        {
            // Override detection
            if (!configdata.manualclicks)
            {
                return;
            }

            if (state == PluginState.DetectedCards)
            {
                // Manually pick a card
                PickCard(index);
            }

        }

        public void OnConfigureHero()
        {
            // Override hero detection
            configurehero = !configurehero;
            SetState(state);
        }

        // Configure hero click
        public void OnCHeroClick(int index)
        {
            configurehero = false;

            if (index >= 0 && index < herohashlist.Count)
            {
                PickHero(herohashlist[index].name);
            }
            else
            {
                SetState(state);
            }
        }

        public void OnCheckboxOverlay(bool check)
        {
            if (configinit)
            {
                configdata.overlay = check;
                SaveConfig();
                ApplyConfig();
                SetState(state); // Set state to update overlay
            }
        }

        public void OnCheckboxManual(bool check)
        {
            if (configinit)
            {
                configdata.manualclicks = check;
                SaveConfig();
                ApplyConfig();
            }
        }

        public void OnCheckboxAutoSave(bool check)
        {
            if (configinit)
            {
                configdata.autosave = check;
                SaveConfig();
                ApplyConfig();
            }
        }

        public void OnCheckboxDebug(bool check)
        {
            if (configinit)
            {
                configdata.debug = check;
                SaveConfig();
                ApplyConfig();
            }
        }

        public void OnWindowLocation()
        {
            // Set window location if not minimized
            if (arenawindow.Left > -32000 && arenawindow.Top > -32000)
            {
                configdata.windowx = (int)arenawindow.Left;
                configdata.windowy = (int)arenawindow.Top;
            }
        }

        // Auto update
        public async void OnUpdateDownloadClick()
        {
            Log.Info("Auto Updating Arena Helper");
            if (latestrelease != null)
            {
                arenawindow.UpdateText.Text = "Updating Arena Helper, please wait...";
                arenawindow.UpdateButtons.Visibility = System.Windows.Visibility.Hidden;
                configdata.updated = true;
                SaveConfig();

                // Update
                bool status = await Update.AutoUpdate(latestrelease);
                if (status)
                {
                    // Start the new process
                    Process.Start(System.Windows.Application.ResourceAssembly.Location);

                    // Shutdown the old process
                    Hearthstone_Deck_Tracker.API.Core.MainWindow.Close();
                    System.Windows.Application.Current.Shutdown();
                }
                else
                {
                    arenawindow.UpdateText.Text = "There was a problem while auto-updating. Press \"website\" to visit the project page and update manually.";

                    arenawindow.UpdateDownload.IsEnabled = false;
                    arenawindow.UpdateButtons.Visibility = System.Windows.Visibility.Visible;
                }
            }
        }

        private void SaveDeck(bool autosave)
        {
            // Save deck
            Deck deck = new Deck();
            deck.IsArenaDeck = true;

            if (autosave)
            {
                deck.DeckId = new Guid(arenadata.deckguid);
            }

            foreach (var cardid in arenadata.pickedcards)
            {
                Card pickedcard = GetCard(cardid);

                if (pickedcard == null)
                    continue;

                Card card = (Card)pickedcard.Clone();
                card.Count = 1;

                // Find out hero
                if (string.IsNullOrEmpty(deck.Class) && card.PlayerClass != "Neutral")
                    deck.Class = card.PlayerClass;

                if (deck.Cards.Contains(card))
                {
                    // Increase count
                    var deckCard = deck.Cards.First(c => c.Equals(card));
                    deck.Cards.Remove(deckCard);
                    deckCard.Count++;
                    deck.Cards.Add(deckCard);
                }
                else
                {
                    deck.Cards.Add(card);
                }
            }

            // Add tag
            deck.Tags.Add("Arena");

            // Deck name based on class
            deck.Name = Helper.ParseDeckNameTemplate(Config.Instance.ArenaDeckNameTemplate, deck);

            if (!autosave)
            {
                // Set the new deck
                Hearthstone_Deck_Tracker.API.Core.MainWindow.SetNewDeck(deck);

                // Activate the window
                Hearthstone_Deck_Tracker.API.Core.MainWindow.ActivateWindow();
            }
            else
            {
                // Set the new deck in editing mode
                Hearthstone_Deck_Tracker.API.Core.MainWindow.SetNewDeck(deck, true);

                // Save the deck
                Hearthstone_Deck_Tracker.API.Core.MainWindow.SaveDeck(true, SerializableVersion.Default, true);

                // Select the deck and make it active
                Hearthstone_Deck_Tracker.API.Core.MainWindow.SelectDeck(deck, true);
            }
        }

        private void SaveArenaData()
        {
            if (!Directory.Exists(DeckDataDir))
                Directory.CreateDirectory(DeckDataDir);

            if (currentfilename == "")
            {
                currentfilename = CreateDeckFilename();
            }

            string json = JsonConvert.SerializeObject(arenadata, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(currentfilename, json);
        }

        private string CreateDeckFilename()
        {
            string filename = string.Format("Arena-{0}", DateTime.Now.ToString("yyyyMMdd-HHmm"));
            return Helper.GetValidFilePath(DeckDataDir, filename, ".json");
        }

        private void LoadArenaData(string filename)
        {
            // Init state
            SetState(PluginState.Idle);

            NewArena();

            bool validarenadata = false;
            ArenaData loadedarenadata = null;
            if (File.Exists(filename))
            {
                try
                {
                    Log.Info("Arena Helper: loading data");
                    // Load the data
                    loadedarenadata = JsonConvert.DeserializeObject<ArenaData>(File.ReadAllText(filename));
                    if (loadedarenadata != null)
                    {
                        // Set the data
                        arenadata = loadedarenadata;
                        validarenadata = true;

                        Log.Info("Arena Helper: Card Rating: " + arenadata.cardrating.Count);
                    }
                    else
                    {
                        Log.Info("Arena Helper: Error loading arena data, null");
                    }
                }
                catch (Exception e)
                {
                    Log.Info("Arena Helper: Error loading arena data");
                }
            }

            if (validarenadata)
            {
                // Set current filename
                currentfilename = filename;

                // Make sure there is a guid for legacy arena runs
                if (arenadata.deckguid == "")
                {
                    arenadata.deckguid = Guid.NewGuid().ToString();
                    SaveArenaData();
                }

                if (arenadata.pickedhero != null)
                {
                    // Hero is picked
                    if (arenadata.pickedcards.Count == MaxCardCount)
                    {
                        // All cards picked
                        SetState(PluginState.Done);
                    }
                    else if ((arenadata.detectedcards.Count - 1) == arenadata.pickedcards.Count)
                    {
                        // Cards detected, but not picked
                        UpdateDetectedCards();
                        SetState(PluginState.SearchCardValues);
                    }
                    else
                    {
                        // Not all cards picked, not picking a card
                        // Search for new cards
                        SetState(PluginState.SearchCards);
                    }
                }
                else
                {
                    // No hero picked
                    if (arenadata.detectedheroes.Count == 3)
                    {
                        // Heroes detected

                        // Show the heroes
                        UpdateDetectedHeroes();
                        SetState(PluginState.SearchBigHero);
                    }
                    else
                    {
                        // No heroes detected
                        SetState(PluginState.SearchHeroes);
                    }
                }

                // Resume arena
                plugins.ResumeArena(arenadata, state);

                UpdateTitle();
                UpdateHero();
            }
            else
            {
                // No arena found, started a new one
                // Save the arena data
                SaveArenaData();
                plugins.NewArena(arenadata);
            }
        }

        private void NewArena()
        {
            // Initialize variables
            currentfilename = "";
            loglasttime = DateTime.MinValue;
            loglastline = "";
            loglastchoice = DateTime.MinValue;
            loglastheroname = "";
            loglastcardid = "";

            previouscards.Clear();
            samecarddelay = false;

            ClearDetected();

            arenawindow.Hero0.HeroImage.Source = null;
            arenawindow.Hero1.HeroImage.Source = null;
            arenawindow.Hero2.HeroImage.Source = null;

            arenawindow.Card0 = null;
            arenawindow.Card1 = null;
            arenawindow.Card2 = null;
            arenawindow.Update();

            // Clear data
            arenadata.deckname = Helper.ParseDeckNameTemplate(Config.Instance.ArenaDeckNameTemplate);
            arenadata.deckguid = Guid.NewGuid().ToString();
            arenadata.detectedheroes.Clear();
            arenadata.pickedhero = null;
            arenadata.detectedcards.Clear();
            arenadata.cardrating.Clear();
            arenadata.pickedcards.Clear();

            // Invalidate arena
            inarena = false;
            stablearena = false;

            // Init state
            SetState(PluginState.SearchHeroes);

            UpdateTitle();
            UpdateHero();
            ResetHeroSize();
        }

        private void ClearDetected()
        {
            detectedcards.Clear();
            detectedcards.Add(new Detection.DetectedInfo(-1));
            detectedcards.Add(new Detection.DetectedInfo(-1));
            detectedcards.Add(new Detection.DetectedInfo(-1));

            detectedheroes.Clear();
            detectedheroes.Add(new Detection.DetectedInfo(-1));
            detectedheroes.Add(new Detection.DetectedInfo(-1));
            detectedheroes.Add(new Detection.DetectedInfo(-1));

            detectedbighero.Clear();
            detectedbighero.Add(new Detection.DetectedInfo(-1));
        }

        public void OnUnload()
        {
            //RemoveElements();

            if (arenawindow != null)
            {
                if (arenawindow.IsVisible)
                {
                    arenawindow.Close();
                }
                arenawindow = null;
            }

        }

        public void OnButtonPress()
        {
            ActivateArenaWindow();
        }

        public async void OnUpdate()
        {
            try
            {
                // Check for plugin updates
                CheckUpdate();
            }
            catch (Exception e)
            {
                string errormsg = "CheckUpdate: " + e.Message + "\n" + e.ToString();
                Log.Info(errormsg);
            }
            
            await mutex.WaitAsync();
            try
            {
                if (arenawindow != null && state != PluginState.Done)
                {
                    Debug.Log("");
                    stopwatch.Restart();

                    // Size updates
                    UpdateSize();

                    // Capture the screen
                    var hsrect = Helper.GetHearthstoneRect(false);
                    if (hsrect.Width > 0 && hsrect.Height > 0)
                    {
                        if (Config.Instance.AlternativeScreenCapture)
                        {
                            detection.fullcapture = ScreenCapture.CaptureWindow(User32.GetHearthstoneWindow(), new Point(0, 0), hsrect.Width, hsrect.Height);
                        }
                        else
                        {
                            detection.fullcapture = ScreenCapture.CaptureScreen(User32.GetHearthstoneWindow(), new Point(0, 0), hsrect.Width, hsrect.Height);
                        }
                    }
                    else
                    {
                        detection.fullcapture = null;
                    }

                    if (detection.fullcapture != null)
                    {
                        await Detect();
                    }
                    else
                    {
                        // Invalidate arena
                        inarena = false;
                        stablearena = false;
                        SetState(state);
                    }

                    Debug.AppendLog("\nElapsed: " + stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception e)
            {
                string errormsg = "OnUpdate Error: " + e.Message + "\n" + e.ToString();
                Log.Info(errormsg);
            }
            finally
            {
                mutex.Release();
            }
        }

        public void OnArenaLogLine(string logline)
        {
            if (arenawindow == null)
                return;

            try
            {
                //Log.Info("AH LogLine: " + logline);

                // Only process new lines
                DateTime loglinetime;
                if (logline.Length > 20 && DateTime.TryParse(logline.Substring(2, 16), out loglinetime))
                {
                    if (loglinetime > DateTime.Now)
                    {
                        loglinetime = loglinetime.AddDays(-1);
                    }

                    if (loglinetime < loglasttime || (loglinetime == loglasttime && logline == loglastline))
                    {
                        // Skip old lines and the previous line
                        // Lines with the same timestamp could be needed, if it is not the same as the previous
                        return;
                    }

                    //Logger.WriteLine("AH LogLine process");
                    // Set new time
                    loglasttime = loglinetime;
                    loglastline = logline;
                }
                else
                {
                    return;
                }

                // Modified from ArenaHandler.cs
                var heromatch = HeroChosenRegex.Match(logline);
                var match = Hearthstone_Deck_Tracker.LogReader.HsLogReaderConstants.NewChoiceRegex.Match(logline);
                if (heromatch.Success)
                {
                    // Hero chosen
                    string heroname = Database.GetHeroNameFromId(heromatch.Groups["id"].Value, false);
                    if (heroname != null)
                    {
                        HeroHashData hero = GetHero(heroname);
                        if (hero != null)
                        {
                            // Hero choice detection, final
                            Log.Info("AH Hero chosen: " + heroname);
                            PickHero(hero.name);
                        }
                    }
                }
                else if (match.Success)
                {
                    string heroname = Database.GetHeroNameFromId(match.Groups["id"].Value, false);
                    if (heroname != null)
                    {
                        if (GetHero(heroname) != null)
                        {
                            // Hero choice detection, not final
                            Log.Info("AH Hero choice: " + heroname);
                            loglastheroname = heroname;
                        }
                    }
                    else
                    {
                        // Card choice detection
                        var cardid = match.Groups["id"].Value;
                        var dtime = DateTime.Now.Subtract(loglastchoice).TotalMilliseconds;

                        // This should not be necessary, but HDT does it
                        if (loglastcardid == cardid && dtime < 1000)
                        {
                            Log.Info(string.Format("AH Card with the same ID ({0}) was chosen less {1} ms ago. Ignoring.", cardid, dtime));
                            return;
                        }

                        Log.Info("AH Card choice: " + cardid);

                        loglastchoice = DateTime.Now;
                        loglastcardid = cardid;
                    }
                }
            }
            catch (Exception e)
            {
                string errormsg = "OnArenaLogLine: " + e.Message + "\n" + e.ToString();
                Log.Info(errormsg);
            }
        }

        private void UpdateSize()
        {
            var hsrect = Helper.GetHearthstoneRect(true);
            if (hsrect.Width <= 0 || hsrect.Height <= 0)
            {
                return;
            }

            // Position card values
            for (int i = 0; i < valueoverlays.Count; i++)
            {
                Point cardpos = Detection.GetHSPos(hsrect, i * Detection.cardwidth + Detection.cardrect.X, Detection.cardrect.Y, Detection.scalewidth, Detection.scaleheight);
                Point cardsize = Detection.GetHSSize(hsrect, Detection.cardrect.Width, Detection.cardrect.Height - 8, Detection.scalewidth, Detection.scaleheight);

                Canvas.SetLeft(valueoverlays[i], cardpos.X + cardsize.X / 2 - valueoverlays[i].RenderSize.Width/2);
                Canvas.SetTop(valueoverlays[i], cardpos.Y + cardsize.Y);
            }

            Point advpos = Detection.GetHSPos(hsrect, Detection.cardrect.X, Detection.cardrect.Y + Detection.cardrect.Height - 8, Detection.scalewidth, Detection.scaleheight);
            //Point advsize = GetHSSize(hsrect, cardrect.Width, cardrect.Height, scalewidth, scaleheight);
            Canvas.SetLeft(adviceoverlay, advpos.X);
            Canvas.SetTop(adviceoverlay, advpos.Y + 52);
        }

        // Check if there are plugin updates
        // Update code adapted from Hearthstone Collection Tracker Plugin
        private async void CheckUpdate()
        {
            await CheckPluginUpdate();
            await CheckDataUpdate();
        }

        private async Task CheckPluginUpdate()
        {
            if (!haspluginupdates)
            {
                if ((DateTime.Now - lastpluginupdatecheck) > pluginupdatecheckinterval)
                {
                    lastpluginupdatecheck = DateTime.Now;
                    latestrelease = await Update.GetLatestRelease();
                    if (latestrelease != null)
                    {
                        Version latestversion = latestrelease.GetVersion();
                        if (latestversion != null)
                        {
                            haspluginupdates = latestversion > Version;
                            Log.Info(latestrelease.Assets[0].Url);
                        }
                    }
                }
            }

            if (haspluginupdates)
            {
                if (arenawindow != null && !showingupdatemessage)
                {
                    showingupdatemessage = true;
                    try
                    {
                        if (arenawindow != null)
                        {
                            arenawindow.FlyoutUpdate.IsOpen = true;
                            haspluginupdates = false;
                            lastpluginupdatecheck = DateTime.Now.AddDays(1);
                        }
                    }
                    catch(Exception e)
                    {
                        string errormsg = "CheckPluginUpdate: " + e.Message + "\n" + e.ToString();
                        Log.Info(errormsg);
                    }
                    finally
                    {
                        showingupdatemessage = false;
                    }
                }
            }
        }

        private async Task CheckDataUpdate()
        {
            if (!hasdataupdates && arenawindow != null && !showingupdatemessage)
            {
                if ((DateTime.Now - lastdataupdatecheck) > dataupdatecheckinterval)
                {
                    lastdataupdatecheck = DateTime.Now;
                    Update.AHDataVersion latestdataversion = await Update.GetDataVersion();
                    Update.LightForgeTierVersion latesttierversion = await Update.GetLightForgeVersion();
                    if (latestdataversion != null && latesttierversion != null)
                    {
                        if (latestdataversion.hashlist > dataversion.hashlist)
                        {
                            // Hash list updated, download the new hash list
                            string hashliststr = await Update.DownloadString(Update.HashListUrl);
                            if (hashliststr != null)
                            {
                                string hashlistfile = Path.Combine(DataDataDir, HashListFile);
                                File.WriteAllText(hashlistfile, hashliststr);
                            }

                            hasdataupdates = true;
                        }

                        if (latesttierversion.tierlist > dataversion.tierlist)
                        {
                            latestdataversion.tierlist = latesttierversion.tierlist;
                            // Tier list updated, download the new tier list
                            string tierliststr = await Update.DownloadString(Update.TierListUrl);
                            if (tierliststr != null)
                            {
                                string tierlistfile = Path.Combine(DataDataDir, TierListFile);
                                File.WriteAllText(tierlistfile, tierliststr);
                            }
                            hasdataupdates = true;
                        }

                        if (hasdataupdates)
                        {
                            // Set the new data version
                            dataversion = latestdataversion;

                            // Write the new version file
                            string dataversionfile = Path.Combine(DataDataDir, DataVersionFile);
                            string json = JsonConvert.SerializeObject(dataversion, Newtonsoft.Json.Formatting.Indented, new VersionConverter());
                            File.WriteAllText(dataversionfile, json);

                            // Load the new data
                            LoadData();

                            if (arenawindow != null)
                            {
                                // Show update message
                                arenawindow.FlyoutDataUpdate.IsOpen = true;
                                haspluginupdates = false;
                                lastpluginupdatecheck = DateTime.Now.AddDays(1);
                            }

                            // Delay checking for updates
                            hasdataupdates = false;
                            lastdataupdatecheck = DateTime.Now.AddDays(1);
                        }
                    }
                }
            }
        }

        private void SetState(PluginState newstate)
        {
            state = newstate;

            if (configurehero)
            {
                ShowOverlay(false);
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.DonePanel.Visibility = System.Windows.Visibility.Hidden;
            } else if (!stablearena && state != PluginState.Done)
            {
                ShowOverlay(false);

                SetDetectingText(DetectingArena, DetectionWarning, "");
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.DonePanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.SearchHeroes)
            {
                ShowValueOverlay(false);
                SetAdviceText(DetectingHeroes);
                ShowAdviceOverlay(configdata.overlay);

                SetDetectingText(DetectingHeroes, DetectionWarning, "Override hero selection by clicking the rectangle in the top-left corner.");
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.DonePanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.SearchBigHero)
            {
                ShowOverlay(false);
                arenawindow.DetectedHeroesWarning.Text = "";
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.DonePanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.DetectedHeroes)
            {
                ShowOverlay(false);
                arenawindow.DetectedHeroesWarning.Text = "";
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.DonePanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.SearchCards)
            {
                ShowValueOverlay(false);
                SetAdviceText(DetectingCards);
                ShowAdviceOverlay(configdata.overlay);

                ClearDetected();
                SetDetectingText(DetectingCards, DetectionWarning, "");
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.DonePanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.SearchCardValues)
            {
                ShowValueOverlay(false);
                SetAdviceText(DetectingValues);
                ShowAdviceOverlay(configdata.overlay);

                SetDetectingText(DetectingValues, DetectionWarning, "");
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.DonePanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.DetectedCards)
            {
                ShowOverlay(configdata.overlay);
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.DonePanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.Done)
            {
                arenawindow.DeckRatingControl1.DeckRatingText.Text = GetDeckRating().ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

                ShowOverlay(false);
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.DonePanel.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private async Task Detect()
        {
            try
            {
                // Detect arena
                ulong arenascreenhash = detection.GetScreenHash(Detection.arenarect, Detection.scalewidth, Detection.scaleheight);

                // Screen is darker when picking a hero
                bool arenacheck2 = false;

                if (state == PluginState.DetectedHeroes || state == PluginState.SearchBigHero)
                {
                    if (detection.GetHashDistance(Detection.arenahash2, arenascreenhash) < 10)
                    {
                        arenacheck2 = true;
                        Debug.AppendLog("Arenacheck2 true\n");
                    }
                }
                //Debug.AppendLog("Arenacheck1: " + detection.GetHashDistance(Detection.arenahash, arenascreenhash) + "\n");
                Debug.AppendLog("Arenacheck2: " + detection.GetHashDistance(Detection.arenahash2, arenascreenhash) + "\n");

                
                //if (detection.GetHashDistance(Detection.arenahash, arenascreenhash) < 10 || arenacheck2)
                if (Hearthstone_Deck_Tracker.API.Core.Game.CurrentMode == Hearthstone_Deck_Tracker.Enums.Hearthstone.Mode.DRAFT)
                {
                    // In arena
                    // If previously not in arena, wait for stable arena screen
                    if (!inarena)
                    {
                        // Wait for stable arena screen
                        arenastopwatch = Stopwatch.StartNew();
                        inarena = true;
                    }

                    if (!stablearena)
                    {
                        if (arenastopwatch.ElapsedMilliseconds > ArenaDetectionTime)
                        {
                            // Stable arena
                            arenastopwatch.Stop();
                            stablearena = true;
                            SetState(state);
                        }
                        else
                        {
                            // Arena not stable
                            return;
                        }
                    }
                }
                else
                {
                    // Invalidate arena
                    inarena = false;
                    stablearena = false;
                    SetState(state);
                    return;
                }

                // In arena

                // Detect heroes
                Tuple<List<int>, List<Tuple<ulong, List<Tuple<int, int>>>>> detectedheroes = detection.DetectHeroes(herohashlist);
                List<int> heroindices = detectedheroes.Item1;

                // Show debug info
                for (int i = 0; i < detectedheroes.Item2.Count; i++)
                {
                    ulong herohash = detectedheroes.Item2[i].Item1;
                    List<Tuple<int, int>> detectedindices = detectedheroes.Item2[i].Item2;

                    Debug.AppendLog("\nHero Hash" + i + ": " + string.Format("0x{0:X}", herohash));
                    foreach (Tuple<int, int> heroindex in detectedindices)
                    {
                        Debug.AppendLog(", " + herohashlist[heroindex.Item1].name + " (" + heroindex.Item2 + ")");
                    }
                }

                // Detect cards
                Tuple<List<int>, List<Tuple<ulong, List<Tuple<int, int>>>>> detectedcards = detection.DetectCards(cardhashlist);
                List<int> cardindices = detectedcards.Item1;

                // Show debug info
                for (int i = 0; i < detectedcards.Item2.Count; i++)
                {
                    ulong cardhash = detectedcards.Item2[i].Item1;
                    List<Tuple<int, int>> detectedindices = detectedcards.Item2[i].Item2;

                    Debug.AppendLog("\nHash" + i + ": " + string.Format("0x{0:X}", cardhash));
                    foreach (Tuple<int, int> cardindex in detectedindices)
                    {
                        Debug.AppendLog(", " + cardlist[cardindex.Item1].Name + " (" + cardindex.Item2 + ")");
                    }
                }


                if (state == PluginState.SearchHeroes)
                {
                    // Searching for heroes
                    SearchHeroes(heroindices);
                }
                else if (state == PluginState.SearchBigHero)
                {
                    // Heroes detected, searching for big hero selection
                    SearchBigHero(heroindices);
                }
                else if (state == PluginState.DetectedHeroes)
                {
                    // Heroes detected, waiting
                    WaitHeroPick(heroindices);
                }
                else if (state == PluginState.SearchCards)
                {
                    // Searching for cards
                    await SearchCards(cardindices);
                }
                else if (state == PluginState.SearchCardValues)
                {
                    // Get card values
                    await SearchCardValues();
                }
                else if (state == PluginState.DetectedCards)
                {
                    // Cards detected, waiting
                    WaitCardPick();
                }
            }
            catch (Exception e)
            {
                Debug.Log("Error: " + e.Message + "\n" + e.ToString());
            }
        }



        private void SearchHeroes(List<int> heroindices)
        {
            if (usehearthmirror)
            {
                Log.Info("GetArenaDraftChoices");
                List<HearthMirror.Objects.Card> choices = Reflection.GetArenaDraftChoices();
                if (choices != null)
                {
                    if (choices.Count == 3)
                    {
                        List<string> heronames = new List<string>();
                        for (int i = 0; i < 3; i++)
                        {
                            Log.Info("Hero Choice: " + choices[i].Id);
                            string heroname = Database.GetHeroNameFromId(choices[i].Id, false);
                            if (heroname != null)
                            {
                                heronames.Add(heroname);
                            }
                        }
                        if (heronames.Count == 3)
                        {
                            arenadata.detectedheroes.Clear();
                            arenadata.detectedheroes.Add(heronames[0]);
                            arenadata.detectedheroes.Add(heronames[1]);
                            arenadata.detectedheroes.Add(heronames[2]);
                            SaveArenaData();

                            plugins.HeroesDetected(arenadata, heronames[0], heronames[1], heronames[2]);

                            // Show the heroes
                            UpdateDetectedHeroes();

                            SetState(PluginState.SearchBigHero);
                        }
                    }
                }
            }
            else if (detection.ConfirmDetected(detectedheroes, heroindices, HeroConfirmations) == 3)
            {
                // All heroes detected
                HeroHashData hero0 = herohashlist[detectedheroes[0].index];
                HeroHashData hero1 = herohashlist[detectedheroes[1].index];
                HeroHashData hero2 = herohashlist[detectedheroes[2].index];

                arenadata.detectedheroes.Clear();
                arenadata.detectedheroes.Add(hero0.name);
                arenadata.detectedheroes.Add(hero1.name);
                arenadata.detectedheroes.Add(hero2.name);
                SaveArenaData();

                plugins.HeroesDetected(arenadata, hero0.name, hero1.name, hero2.name);

                // Show the heroes
                UpdateDetectedHeroes();

                SetState(PluginState.SearchBigHero);
            }
        }

        private void SearchBigHero(List<int> heroindices)
        {
            if (usehearthmirror)
            {
                // Big hero detected

                // Update gui
                string bigheroname = loglastheroname;
                int bigheroindex = -1;
                for (int i = 0; i < arenadata.detectedheroes.Count; i++)
                {
                    if (arenadata.detectedheroes[i] == bigheroname)
                    {
                        bigheroindex = i;
                        break;
                    }
                }

                if (bigheroindex != -1)
                {
                    ResetHeroSize();
                    ChangeHeroSize(bigheroindex, 56, 56, 4);

                    //SetState(PluginState.DetectedHeroes);
                }
            }
            else
            {
                Tuple<List<int>, List<Tuple<ulong, List<Tuple<int, int>>>>> detectedbigheroes = detection.DetectBigHero(herohashlist);
                List<int> bigheroindices = detectedbigheroes.Item1;

                // Show debug info
                for (int i = 0; i < detectedbigheroes.Item2.Count; i++)
                {
                    ulong bigherohash = detectedbigheroes.Item2[i].Item1;
                    List<Tuple<int, int>> detectedindices = detectedbigheroes.Item2[i].Item2;

                    Debug.AppendLog("\nBig Hero Hash: " + string.Format("0x{0:X}", bigherohash));
                    foreach (Tuple<int, int> bigheroindex in detectedindices)
                    {
                        Debug.AppendLog(", " + herohashlist[bigheroindex.Item1].name + " (" + bigheroindex.Item2 + ")");
                    }
                    Debug.AppendLog("\n");
                }

                if (detection.ConfirmDetected(detectedbighero, bigheroindices, BigHeroConfirmations) == 1)
                {
                    // Big hero detected

                    // Update gui
                    string bigheroname = herohashlist[detectedbighero[0].index].name;
                    int bigheroindex = -1;
                    for (int i = 0; i < arenadata.detectedheroes.Count; i++)
                    {
                        if (arenadata.detectedheroes[i] == bigheroname)
                        {
                            bigheroindex = i;
                            break;
                        }
                    }
                    ChangeHeroSize(bigheroindex, 56, 56, 4);

                    SetState(PluginState.DetectedHeroes);

                    // Call it immediately
                    WaitHeroPick(heroindices);
                }
            }
        }

        private void ResetHeroSize()
        {
            for (int i = 0; i < 3; i++)
            {
                ChangeHeroSize(i, 32, 32, 16);
            }
        }

        private void ChangeHeroSize(int index, int width, int height, int margin)
        {
            var newmargin = new System.Windows.Thickness(0, margin, 0, 0);
            arenawindow.Hero0.HeroBorder.Margin = newmargin;
            arenawindow.Hero1.HeroBorder.Margin = newmargin;
            arenawindow.Hero2.HeroBorder.Margin = newmargin;

            switch (index)
            {
                case 0:
                    arenawindow.Hero0.HeroImage.Width = width;
                    arenawindow.Hero0.HeroImage.Height = height;
                    arenawindow.Hero0.HeroBorder.Width = width + 8;
                    arenawindow.Hero0.HeroBorder.Height = height + 8;
                    break;
                case 1:
                    arenawindow.Hero1.HeroImage.Width = width;
                    arenawindow.Hero1.HeroImage.Height = height;
                    arenawindow.Hero1.HeroBorder.Width = width + 8;
                    arenawindow.Hero1.HeroBorder.Height = height + 8;

                    break;
                case 2:
                    arenawindow.Hero2.HeroImage.Width = width;
                    arenawindow.Hero2.HeroImage.Height = height;
                    arenawindow.Hero2.HeroBorder.Width = width + 8;
                    arenawindow.Hero2.HeroBorder.Height = height + 8;
                    break;
                default:
                    break;
            }
            arenawindow.Update();
        }

        private void WaitHeroPick(List<int> heroindices)
        {
            if (!usehearthmirror)
            {
                Debug.AppendLog("\nChoosing: " + herohashlist[detectedbighero[0].index].name + "\n");

                // All heroes detected, wait for pick
                if (detection.GetUndetectedCount(heroindices) == 0)
                {
                    // Cancelled the choice
                    ClearDetected();
                    SetState(PluginState.SearchBigHero);

                    // Restore gui
                    ResetHeroSize();
                }
            }
        }

        private void PickHero(string heroname)
        {
            if (state == PluginState.Done)
                return;

            arenadata.pickedhero = (HeroClass)Enum.Parse(typeof(HeroClass), heroname);
            SaveArenaData();

            UpdateHero();

            plugins.HeroPicked(arenadata, arenadata.pickedhero.ToString());

            // Show the card panel
            SetState(PluginState.SearchCards);
        }

        private async Task SearchCards(List<int> cardindices)
        {
            // TODO: Using HearthMirror. Also has reference to HearthMirror.
            Card[] cards = new Card[3];
            bool valid = false;

            if (usehearthmirror)
            {
                Log.Info("GetArenaDraftChoices");
                List<HearthMirror.Objects.Card> choices = Reflection.GetArenaDraftChoices();
                if (choices != null)
                {
                    if (choices.Count == 3)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            cards[i] = GetCard(choices[i].Id);
                            Log.Info("Choice: " + choices[i].Id);
                        }

                        // Check for same cards
                        bool samecardsdetected = false;
                        if (previouscards.Count == 3)
                        {
                            int samecards = 0;
                            for (int i = 0; i < 3; i++)
                            {
                                if (previouscards[i].Id == cards[i].Id)
                                {
                                    samecards++;
                                }
                            }

                            if (samecards == 3)
                            {
                                // All the same cards, can be valid but unlikely
                                samecardsdetected = true;
                            }
                        }

                        // Save card choices
                        previouscards.Clear();
                        for (int i = 0; i < 3; i++)
                        {
                            previouscards.Add(cards[i]);
                        }

                        // Wait for confirmations when same cards detected
                        if (samecardsdetected)
                        {
                            if (!samecarddelay)
                            {
                                Log.Info("Same cards detected: delaying");
                                samecardtime = DateTime.Now;
                                samecarddelay = true;
                            }
                            else if (DateTime.Now.Subtract(samecardtime).TotalMilliseconds >= samecardmaxtime)
                            {
                                samecarddelay = false;
                                valid = true;
                            }

                        }
                        else
                        {
                            samecarddelay = false;
                            valid = true;
                        }
                    }
                }
            }
            else if (detection.ConfirmDetected(detectedcards, cardindices, CardConfirmations) == 3)
            {
                // All cards detected

                // Save detected cards
                for (int i = 0; i < 3; i++)
                {
                    cards[i] = cardlist[detectedcards[i].index];
                }
                valid = true;
            }

            if (valid)
            {
                // All cards detected

                // Save detected cards
                arenadata.detectedcards.Add(new Tuple<string, string, string>(cards[0].Id, cards[1].Id, cards[2].Id));
                SaveArenaData();

                // Update the plugin
                plugins.CardsDetected(arenadata, cards[0], cards[1], cards[2]);

                UpdateDetectedCards();

                SetState(PluginState.SearchCardValues);

                // Call it immediately
                await SearchCardValues();
            }
        }

        private async Task SearchCardValues()
        {
            int lastindex = arenadata.detectedcards.Count - 1;
            if (lastindex < 0)
            {
                // This shouldn't happen
                return;
            }

            List<Card> newcards = new List<Card>();
            newcards.Add(GetCard(arenadata.detectedcards[lastindex].Item1));
            newcards.Add(GetCard(arenadata.detectedcards[lastindex].Item2));
            newcards.Add(GetCard(arenadata.detectedcards[lastindex].Item3));

            // Add default values from the tierlist
            List<double> values = new List<double>();
            for (int i = 0; i < 3; i++)
            {
                values.Add(GetCardValue(newcards[i].Id));
            }

            // Get the plugin result
            List<double> pvalues = await plugins.GetCardValues(arenadata, newcards, values);
            string advice = await plugins.GetCardAdvice(arenadata, newcards, values);

            // Override the values if the plugin has a result
            if (pvalues != null)
            {
                if (pvalues.Count >= 3)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        values[i] = pvalues[i];
                    }
                }
            }

            // Check if values are missing
            bool missing = false;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == double.NaN)
                {
                    values[i] = double.NaN;
                    missing = true;
                }
            }

            // Show the card value
            arenawindow.Value0.Content = values[0];
            arenawindow.Value1.Content = values[1];
            arenawindow.Value2.Content = values[2];

            // Get the actual numerical value
            double maxvalue = 0;
            currentcardvalues.Clear();
            for (int i = 0; i < 3; i++)
            {
                double dval = values[i];
                currentcardvalues.Add(dval);

                if (i == 0 || dval > maxvalue)
                {
                    maxvalue = dval;
                }
            }

            // Set value text
            for (int i = 0; i < valueoverlays.Count; i++)
            {
                SetValueText(i, values[i].ToString());
                
                // Highlight the card with the highest value, if no cards are missing
                if (!missing && currentcardvalues[i] == maxvalue)
                {
                    valueoverlays[i].GradientStop1.Color = System.Windows.Media.Color.FromArgb(0xFF, 0xf5, 0xdb, 0x4c);
                    valueoverlays[i].GradientStop2.Color = System.Windows.Media.Color.FromArgb(0xFF, 0x8b, 0x68, 0x11);
                }
                else
                {
                    valueoverlays[i].GradientStop1.Color = System.Windows.Media.Color.FromArgb(0xFF, 0x51, 0x51, 0x51);
                    valueoverlays[i].GradientStop2.Color = System.Windows.Media.Color.FromArgb(0xFF, 0x2D, 0x2D, 0x2D);
                }

                valueoverlays[i].UpdateLayout();
            }
            UpdateSize(); // Update size to center the labels

            SetAdviceText(advice);

            arenawindow.Update();

            SetState(PluginState.DetectedCards);

            // Reset choice
            loglastcardid = "";

            // Call it immediately
            WaitCardPick();
        }

        private void SetValueText(int index, string value)
        {
            if (index >= 0 && index < valueoverlays.Count)
            {
                valueoverlays[index].ValueText.Text = value;
            }
        }

        private void SetAdviceText(string advice)
        {
            adviceoverlay.AdviceText.Text = advice;
        }

        public double GetNumericalValue(string str)
        {
            // Ignore everything after the first space
            int space = str.IndexOf(' ');
            if (space != -1)
            {
                str = str.Substring(0, space);
            }

            // Strip everything except numbers and dots
            var nstr = Regex.Replace(str, "[^0-9.]", "");
            double dvalue = 0;
            try
            {
                dvalue = System.Xml.XmlConvert.ToDouble(nstr);
            }
            catch (Exception)
            {
            }

            return dvalue;
        }

        public double GetCardValue(string id)
        {
            double value = 0.0;
            HeroHashData herodata = GetHero(arenadata.pickedhero.ToString());
            if (herodata != null)
            {
                CardTierInfo cardtierinfo = GetCardTierInfo(id);
                if (cardtierinfo != null)
                {
                    ClassTierScore tierScore = GetClassTierScore(cardtierinfo, arenadata.pickedhero);
                    value = tierScore.Score;
                }
            }
            return value;
        }

        private void WaitCardPick()
        {
            // All cards detected, wait for new pick

            int lastindex = arenadata.detectedcards.Count - 1;
            if (lastindex < 0)
                return;

            // Display detected cards
            Debug.AppendLog("\nPicking card " + (arenadata.pickedcards.Count + 1) + "/" + MaxCardCount);

            List<Card> dcards = new List<Card>();
            dcards.Add(GetCard(arenadata.detectedcards[lastindex].Item1));
            dcards.Add(GetCard(arenadata.detectedcards[lastindex].Item2));
            dcards.Add(GetCard(arenadata.detectedcards[lastindex].Item3));
            for (int i = 0; i < dcards.Count; i++)
            {
                string cardname = "";
                if (dcards[i] != null)
                {
                    cardname = dcards[i].Name;
                }
                Debug.AppendLog("\nDetected " + i + ": " + cardname);
            }

            // Display picked cards
            for (int i = 0; i < arenadata.pickedcards.Count; i++)
            {
                Card card = GetCard(arenadata.pickedcards[i]);
                string cardname = "";
                if (card != null)
                {
                    cardname = card.Name;
                }
                Debug.AppendLog("\nPicked " + i + ": " + cardname);
            }

            // Skip this if we only allow manual picking
            if (configdata.manualclicks)
            {
                return;
            }
            else
            {
                // Logreader
                if (loglastcardid != "")
                {
                    Log.Info("AH Card choice pick: " + loglastcardid);
                    PickCard(loglastcardid);
                    loglastcardid = "";
                }
            }
        }

        // PickCard using the cardid
        private void PickCard(string cardid)
        {
            int lastindex = arenadata.detectedcards.Count - 1;
            if (lastindex < 0)
                return;

            // Determine the index of the picked card
            int pickindex = -1;
            var dc = arenadata.detectedcards[lastindex];
            if (cardid == dc.Item1)
            {
                pickindex = 0;
            }
            else if (cardid == dc.Item2)
            {
                pickindex = 1;
            }
            else if (cardid == dc.Item3)
            {
                pickindex = 2;
            }

            PickCard(pickindex);
        }

        // PickCard using the pickindex
        private void PickCard(int pickindex)
        {
            int lastindex = arenadata.detectedcards.Count - 1;
            if (lastindex < 0)
                return;

            // Add to pickedcards
            string cardid = "";
            Card pickedcard = null;
            if (pickindex >= 0 && pickindex < 3)
            {
                List<Card> dcards = new List<Card>();
                dcards.Add(GetCard(arenadata.detectedcards[lastindex].Item1));
                dcards.Add(GetCard(arenadata.detectedcards[lastindex].Item2));
                dcards.Add(GetCard(arenadata.detectedcards[lastindex].Item3));

                pickedcard = dcards[pickindex];
                if (pickedcard != null)
                {
                    cardid = pickedcard.Id;
                }
                else
                {
                    pickindex = -1;
                }
            }
            else
            {
                pickindex = -1;
            }

            if (pickindex == -1)
            {
                Log.Info("AH: Missed a pick");
            }

            // Add picked card
            arenadata.pickedcards.Add(cardid);

            // Add card rating
            double cardval0 = 0;
            double cardval1 = 0;
            double cardval2 = 0;
            if (currentcardvalues.Count == 3)
            {
                cardval0 = currentcardvalues[0];
                cardval1 = currentcardvalues[1];
                cardval2 = currentcardvalues[2];
            }
            arenadata.cardrating.Add(new Tuple<double, double, double>(cardval0, cardval1, cardval2));

            // Save
            SaveArenaData();

            plugins.CardPicked(arenadata, pickindex, pickedcard);

            if (arenawindow != null)
            {
                arenawindow.Update();
            }

            if (arenadata.pickedcards.Count == MaxCardCount)
            {
                SetState(PluginState.Done);
                plugins.Done(arenadata);
            }
            else
            {
                SetState(PluginState.SearchCards);
            }

            UpdateTitle();

            // Save the deck when auto saving
            if (configdata.autosave)
            {
                SaveDeck(true);
            }
        }

        public static Card GetCard(string id)
        {
            if (id != "")
            {
                for (int i = 0; i < cardlist.Count; i++)
                {
                    if (cardlist[i].Id == id)
                    {
                        return cardlist[i];
                    }
                }
            }

            return null;
        }

        public static HeroHashData GetHero(string name)
        {
            if (name != "")
            {
                return herohashlist.First(hhd => hhd.name == name);
            }

            return null;
        }

        public static CardTierInfo GetCardTierInfo(string id)
        {
            if (id != "")
            {
                return cardtierlist[id];
            }

            return null;
        }

        public static ClassTierScore GetClassTierScore(CardTierInfo cardTierInfo, HeroClass? heroClass)
        {
            var heroScore = cardTierInfo.Scores.First(cts => cts.Hero == heroClass);
            var generalScore = cardTierInfo.Scores.First(cts => cts.Hero == null);

            return heroScore ?? generalScore;
        }

        public static PluginState GetState()
        {
            return state;
        }

        private void UpdateTitle()
        {
            arenawindow.Header.Text = "Picking card " + (arenadata.pickedcards.Count + 1) + "/" + MaxCardCount;
            arenawindow.DeckName.Content = arenadata.deckname;
            arenawindow.DeckRatingControl0.DeckRatingText.Text = GetDeckRating().ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        private double GetDeckRating()
        {
            int pcount = arenadata.pickedcards.Count;
            int dcount = arenadata.detectedcards.Count;
            int ccount = arenadata.cardrating.Count;

            // Check if valid counts
            if (!(pcount > 0 && pcount <= dcount && pcount == ccount))
            {
                return 0;
            }

            // Calculate total rating
            double totalrating = 0;
            for (int i = 0; i < pcount; i++)
            {
                string curcard = arenadata.pickedcards[i];
                double curcardval = 0;
                if (curcard == arenadata.detectedcards[i].Item1)
                {
                    curcardval = arenadata.cardrating[i].Item1;
                }
                else if (curcard == arenadata.detectedcards[i].Item2)
                {
                    curcardval = arenadata.cardrating[i].Item2;
                }
                else if (curcard == arenadata.detectedcards[i].Item3)
                {
                    curcardval = arenadata.cardrating[i].Item3;
                }

                totalrating += curcardval;
            }


            // Return average
            double deckrating = totalrating / (double)pcount;
            return deckrating;
        }

        private void SetDetectingText(string title, string text, string text2)
        {
            arenawindow.DetectingHeader.Text = title;
            arenawindow.DetectingText.Text = text;
            arenawindow.DetectingText2.Text = text2;
        }

        private void UpdateHero()
        {
            HeroHashData hero = GetHero(arenadata.pickedhero.ToString());
            if (hero != null)
            {
                arenawindow.PickedHeroImage.Source = new BitmapImage(new Uri(@"/HearthstoneDeckTracker;component/Resources/" + hero.image, UriKind.Relative));
            }
            else
            {
                arenawindow.PickedHeroImage.Source = null;
            }
        }

        private void UpdateDetectedHeroes()
        {
            if (arenadata.detectedheroes.Count != 3)
                return;

            // All heroes detected, show them
            SetHeroControl(arenawindow.Hero0, arenadata.detectedheroes[0]);
            SetHeroControl(arenawindow.Hero1, arenadata.detectedheroes[1]);
            SetHeroControl(arenawindow.Hero2, arenadata.detectedheroes[2]);

            // Update window
            arenawindow.Update();
        }

        private void UpdateDetectedCards()
        {
            int lastindex = arenadata.detectedcards.Count - 1;
            if (lastindex < 0)
                return;

            // All cards detected, show them
            arenawindow.Card0 = GetCard(arenadata.detectedcards[lastindex].Item1);
            arenawindow.Card1 = GetCard(arenadata.detectedcards[lastindex].Item2);
            arenawindow.Card2 = GetCard(arenadata.detectedcards[lastindex].Item3);

            // Update window
            arenawindow.Update();
        }

        private void SetHeroControl(Controls.Hero herocontrol, string heroname)
        {
            HeroHashData hero = GetHero(heroname);

            if (hero == null)
                return;

            herocontrol.HeroImage.Source = new BitmapImage(new Uri(@"/HearthstoneDeckTracker;component/Resources/" + hero.image, UriKind.Relative));
            herocontrol.HeroName.Text = hero.name;
        }

        private void LoadData()
        {
            string assemblylocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (!Directory.Exists(DataDataDir))
                Directory.CreateDirectory(DataDataDir);

            // Data files
            string dataversionfile = Path.Combine(assemblylocation, "data", DataVersionFile);
            string userdataversionfile = Path.Combine(DataDataDir, DataVersionFile);
            string cardhashesfile = Path.Combine(assemblylocation, "data", HashListFile);
            string usercardhashesfile = Path.Combine(DataDataDir, HashListFile);
            string cardtierfile = Path.Combine(assemblylocation, "data", TierListFile);
            string usercardtierfile = Path.Combine(DataDataDir, TierListFile);

            // Get default data version
            dataversion = LoadDataVersion(dataversionfile);

            // Check user data version
            if (File.Exists(userdataversionfile))
            {
                Update.AHDataVersion userdataversion = LoadDataVersion(userdataversionfile);
                if (userdataversion.hashlist > dataversion.hashlist)
                {
                    if (File.Exists(usercardhashesfile))
                    {
                         // Use userdata version
                        Log.Info("Arena Helper: Using userdata version of hashlist");
                         dataversion.hashlist = userdataversion.hashlist;
                         cardhashesfile = usercardhashesfile;
                     }
                 }

                if (userdataversion.tierlist > dataversion.tierlist)
                {
                    if (File.Exists(usercardtierfile))
                    {
                        // Use userdata version
                        Log.Info("Arena Helper: Using userdata version of tierlist");
                        dataversion.tierlist = userdataversion.tierlist;
                        cardtierfile = usercardtierfile;
                    }
                }
            }

            // Load card hashes
            cardhashlist = JsonConvert.DeserializeObject<List<CardHashData>>(File.ReadAllText(cardhashesfile));

            // Load card tier info
            cardtierlist = LoadCardTierList(cardtierfile);
        }

        private Dictionary<string, CardTierInfo> LoadCardTierList(string tierListFile)
        {
            var lightforgeData = JsonConvert.DeserializeObject<CardData>(File.ReadAllText(tierListFile));
            // Remove duplicate keys due to tri-class cards
            return lightforgeData.Cards.GroupBy(cti => cti.CardId).Select(cti => cti.First()).ToDictionary(cti => cti.CardId, cti => cti);

        }

        private Update.AHDataVersion LoadDataVersion(string filename)
        {
            return JsonConvert.DeserializeObject<Update.AHDataVersion>(File.ReadAllText(filename), new VersionConverter());
        }

        private Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

        // Add overlay elements for debugging
        private void AddElements()
        {
            // Value overlay
            if (valueoverlays.Count == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    Controls.ValueOverlay valuetext = new Controls.ValueOverlay();
                    valuetext.ValueText.Text = "Value";
                    Canvas.SetLeft(valuetext, 5);
                    Canvas.SetTop(valuetext, 5);
                    Hearthstone_Deck_Tracker.API.Core.OverlayCanvas.Children.Add(valuetext);
                    valuetext.Visibility = System.Windows.Visibility.Hidden;
                    valueoverlays.Add(valuetext);
                }
            }

            // Advice overlay
            if (adviceoverlay == null)
            {
                adviceoverlay = new Controls.AdviceOverlay();
                adviceoverlay.AdviceText.Text = "";
                Canvas.SetLeft(adviceoverlay, 5);
                Canvas.SetTop(adviceoverlay, 5);
                Hearthstone_Deck_Tracker.API.Core.OverlayCanvas.Children.Add(adviceoverlay);
                adviceoverlay.Visibility = System.Windows.Visibility.Hidden;
            }

            // Test text
            if (debugtext == null)
            {
                debugtext = new Controls.DebugTextBlock();
                debugtext.TextWrapping = System.Windows.TextWrapping.Wrap;
                debugtext.FontSize = 12;
                debugtext.Text = "Arena Helper";
                Canvas.SetLeft(debugtext, 5);
                Canvas.SetTop(debugtext, 5);

                Hearthstone_Deck_Tracker.API.Core.OverlayCanvas.Children.Add(debugtext);

                debugtext.Visibility = System.Windows.Visibility.Hidden;

                Debug.SetTextControl(debugtext);
            }

            // Test images
            if (debugimages.Count == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    System.Windows.Controls.Image debugimage = new System.Windows.Controls.Image();

                    Canvas.SetLeft(debugimage, 5 + i * 210);
                    Canvas.SetTop(debugimage, 550);

                    Hearthstone_Deck_Tracker.API.Core.OverlayCanvas.Children.Add(debugimage);

                    debugimage.Visibility = System.Windows.Visibility.Hidden;
                    debugimages.Add(debugimage);
                }

                Debug.SetImageControls(debugimages);
            }
        }

        private void RemoveElements()
        {
            // Value overlay
            for (int i = 0; i < valueoverlays.Count; i++)
            {
                Hearthstone_Deck_Tracker.API.Core.OverlayCanvas.Children.Remove(valueoverlays[i]);
            }
            valueoverlays.Clear();

            // Advice overlay
            if (adviceoverlay != null)
            {
                Hearthstone_Deck_Tracker.API.Core.OverlayCanvas.Children.Remove(adviceoverlay);
                adviceoverlay = null;
            }

            if (debugtext != null)
            {
                Hearthstone_Deck_Tracker.API.Core.OverlayCanvas.Children.Remove(debugtext);
                Debug.SetTextControl(null);
                debugtext = null;
            }

            for (int i = 0; i < debugimages.Count; i++)
            {
                Hearthstone_Deck_Tracker.API.Core.OverlayCanvas.Children.Remove(debugimages[i]);
            }
            Debug.SetImageControls(null);
            debugimages.Clear();
        }

    }
}
