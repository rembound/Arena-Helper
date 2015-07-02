using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.IO;
using System.Windows.Media.Imaging;
using System.Reflection;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Plugins;
using Emgu.CV;
using Emgu.CV.Structure;
using Hearthstone_Deck_Tracker.Hearthstone;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using MahApps.Metro.Controls.Dialogs;
using System.Text.RegularExpressions;

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

            public ConfigData()
            {
                windowx = 100;
                windowy = 100;
                manualclicks = false;
                overlay = true;
                debug = false;
                autosave = false;
            }
        }

        public class CardTierInfo
        {
            public string id;
            public string name;
            public List<string> value;

            public CardTierInfo(string id, string name, List<string> value)
            {
                this.id = id;
                this.name = name;
                this.value = value;
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

        public class DetectedInfo
        {
            public int index;
            public int confirmations;

            public DetectedInfo(int index)
            {
                this.index = index;
                confirmations = 0;
            }

            public void Confirm(int cindex)
            {
                if (index != cindex)
                {
                    // Different index
                    index = cindex;
                    confirmations = 0;
                }
                else
                {
                    // Same index, increase confirmations
                    confirmations++;
                }
            }
        }

        public class ArenaData
        {
            public string deckname;
            public string deckguid;
            public List<string> detectedheroes;
            public string pickedhero;
            public List<Tuple<string, string, string>> detectedcards;
            public List<string> pickedcards;

            public ArenaData()
            {
                deckname = "";
                deckguid = "";
                detectedheroes = new List<string>();
                pickedhero = "";
                detectedcards = new List<Tuple<string, string, string>>();
                pickedcards = new List<string>();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MousePoint
        {
            public readonly int X;
            public readonly int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out MousePoint lpPoint);

        public static Point GetMousePos()
        {
            MousePoint p;
            GetCursorPos(out p);
            return new Point(p.X, p.Y);
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(UInt16 virtualKeyCode);
        private const UInt16 VK_MBUTTON = 0x04; // middle mouse button
        private const UInt16 VK_LBUTTON = 0x01; // left mouse button
        private const UInt16 VK_RBUTTON = 0x02; // right mouse button

        public enum PluginState { Idle, SearchHeroes, SearchBigHero, DetectedHeroes, SearchCards, SearchCardValues, DetectedCards, Done };

        private List<DetectedInfo> detectedcards = new List<DetectedInfo>();
        private List<DetectedInfo> detectedheroes = new List<DetectedInfo>();
        private List<DetectedInfo> detectedbighero = new List<DetectedInfo>();
        private static PluginState state;
        private List<int> mouseindex = new List<int>();
        private ArenaData arenadata = new ArenaData();
        private string currentfilename = "";
        private ConfigData configdata = new ConfigData();
        private bool configinit = false;

        private List<ArenaHelper.Controls.ValueOverlay> valueoverlays = new List<ArenaHelper.Controls.ValueOverlay>();
        private ArenaHelper.Controls.AdviceOverlay adviceoverlay = null;
        private HearthstoneTextBlock testtext = null;
        private List<System.Windows.Controls.Image> testimages = new List<System.Windows.Controls.Image>();
        
        public static List<Card> cardlist = new List<Card>();
        private List<CardHashData> cardhashlist = new List<CardHashData>();
        public static List<HeroHashData> herohashlist = new List<HeroHashData>();
        public static List<CardTierInfo> cardtierlist = new List<CardTierInfo>();

        private Bitmap fullcapture;

        // Left card dimensions
        private const int scalewidth = 1280;
        private const int scaleheight = 960;
        Rectangle cardrect = new Rectangle(100, 152, 260, 393);
        Rectangle cardcroprect = new Rectangle(127, 226, 204, 157);
        private const int cardwidth = 250;

        // Arena detection
        Rectangle arenarect = new Rectangle(305, 0, 349, 69);
        ulong arenahash = 14739256890895383027;
        ulong arenahash2 = 18342314164188135155; // Dark arena hash in PluginState.DetectedHeroes
        bool inarena = false;
        bool stablearena = false;
        Stopwatch arenastopwatch;

        // Configure heroes
        bool configurehero = false;

        // Portrait detection
        Rectangle portraitcroprect = new Rectangle(143, 321, 173, 107);
        private const int portraitwidth = 250;
        ulong[] herohashes = new ulong[7];

        // Big portrait detection
        Rectangle portraitbigcroprect = new Rectangle(381, 376, 471, 291);

        // Updates
        private DateTime lastupdatecheck = DateTime.MinValue;
        private bool hasupdates = false;
        private TimeSpan updatecheckinterval = TimeSpan.FromHours(1);
        private bool showingupdatemessage = false;

        Stopwatch stopwatch;

        protected MenuItem MainMenuItem { get; set; }
        protected static ArenaWindow arenawindow;
        
        private SemaphoreSlim mutex = new SemaphoreSlim(1);

        private const int ArenaDetectionTime = 750;
        private const int MaxCardCount = 30;
        private const int HeroConfirmations = 3;
        private const int CardConfirmations = 3;
        private const int BigHeroConfirmations = 0; // Confirm immediately
        private const int maxcarddistance = 10;
        private const int maxherodistance = 14;
        private const string DetectingArena = "Detecting arena...";
        private const string DetectingHeroes = "Detecting heroes...";
        private const string DetectingCards = "Detecting cards...";
        private const string DetectingValues = "Getting values...";
        private const string DetectionWarning = "Please make sure nothing overlaps the arena heroes and cards and the Hearthstone window has the focus. Don't make a selection yet!";
        private const string DoneMessage = "All cards are picked. You can start a new arena run or save the deck.";
        private const string ConfigFile = "arenahelper.json";

        // TODO: When AH window is open, and you start Hearthstone, sometimes the plugin takes too long and gets suspended.

        private Plugins plugins = new Plugins();

        private string DataDir
        {
            get { return Path.Combine(Config.Instance.DataDir, "ArenaHelper"); }
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
            get { return new Version("0.4.0"); }
        }

        public MenuItem MenuItem
        {
            get { return MainMenuItem; }
        }

        public void OnLoad()
        {
            plugins.LoadPlugins();

            state = PluginState.Idle;

            // Set hashes
            herohashlist.Clear();
            herohashlist.Add(new HeroHashData(0, "Warrior", "warrior_small.png", 13776678289873991291, 13071189497635732127, 12080542990295427731)); // Garrosh, Magni small, Magni big
            herohashlist.Add(new HeroHashData(1, "Shaman", "shaman_small.png", 18366959783178990451));
            herohashlist.Add(new HeroHashData(2, "Rogue", "rogue_small.png", 5643619427529904809));
            herohashlist.Add(new HeroHashData(3, "Paladin", "paladin_small.png", 11505795398351105139));
            herohashlist.Add(new HeroHashData(4, "Hunter", "hunter_small.png", 2294799430464257123, 12942361696967163803, 17552924014479703963)); // Rexxar, Alleria small, Alleria big
            herohashlist.Add(new HeroHashData(5, "Druid", "druid_small.png", 5433851923975358071));
            herohashlist.Add(new HeroHashData(6, "Warlock", "warlock_small.png", 10186248321710093033));
            herohashlist.Add(new HeroHashData(7, "Mage", "mage_small.png", 15770007155810004267, 8631746754340092973, 8343516378188643373)); // Jaina, Medivh small, Medivh big
            herohashlist.Add(new HeroHashData(8, "Priest", "priest_small.png", 15052908377040876499));

            AddMenuItem();

            stopwatch = Stopwatch.StartNew();

            LoadCards();
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

        private async void ActivateArenaWindow()
        {
            if (arenawindow == null)
            {
                await InitializeMainWindow();
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

        protected async Task InitializeMainWindow()
        {
            if (arenawindow == null)
            {
                arenawindow = new ArenaWindow();

                // Load config
                LoadConfig();
                AddElements();
                ApplyConfig();
                arenawindow.Closed += async (sender, args) =>
                {
                    await plugins.CloseArena(arenadata, state);

                    // Save window location
                    if (arenawindow.WindowState != System.Windows.WindowState.Minimized)
                    {
                        // Set window location
                        configdata.windowx = (int)arenawindow.Left;
                        configdata.windowy = (int)arenawindow.Top;
                    }

                    SaveConfig();
                    RemoveElements();
                    arenawindow = null;
                    configinit = false;
                };

                // Init
                InitConfigureHero();

                arenawindow.onbuttonnewarenaclick = new ArenaWindow.OnEvent(OnButtonNewArenaClick);
                arenawindow.onbuttonsaveclick = new ArenaWindow.OnEvent(OnButtonSaveClick);
                arenawindow.onaboutclick = new ArenaWindow.OnEvent(OnAboutClick);

                arenawindow.onheroclick = new ArenaWindow.OnOverrideClick(OnHeroClick);
                arenawindow.oncardclick = new ArenaWindow.OnOverrideClick(OnCardClick);
                arenawindow.onconfigurehero = new ArenaWindow.OnEvent(OnConfigureHero);
                arenawindow.oncheroclick = new ArenaWindow.OnOverrideClick(OnCHeroClick);

                arenawindow.oncheckboxoverlay = new ArenaWindow.OnCheckbox(OnCheckboxOverlay);
                arenawindow.oncheckboxmanual = new ArenaWindow.OnCheckbox(OnCheckboxManual);
                arenawindow.oncheckboxautosave = new ArenaWindow.OnCheckbox(OnCheckboxAutoSave);
                arenawindow.oncheckboxdebug = new ArenaWindow.OnCheckbox(OnCheckboxDebug);

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
                await LoadArenaData(newestfilename);
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
                // Load the data
                configdata = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(filename));

                // Set window position
                arenawindow.Left = configdata.windowx;
                arenawindow.Top = configdata.windowy;

                // Set options
                arenawindow.CheckBoxOverlay.IsChecked = configdata.overlay;
                arenawindow.CheckBoxManual.IsChecked = configdata.manualclicks;
                arenawindow.CheckBoxAutoSave.IsChecked = configdata.autosave;
                arenawindow.CheckBoxDebug.IsChecked = configdata.debug;
            }

            configinit = true;
        }

        private void ApplyConfig()
        {
            // Debug
            if (configdata.debug)
            {
                testtext.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                testtext.Visibility = System.Windows.Visibility.Hidden;
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

        public async void OnButtonNewArenaClick()
        {
            NewArena();
            SaveArenaData();
            await plugins.NewArena(arenadata);
        }

        public void OnAboutClick()
        {
            ShowDialog();
        }

        private async void ShowDialog()
        {
            var settings = new MetroDialogSettings { AffirmativeButtonText = "Yes", NegativeButtonText = "No" };
            try
            {
                if (arenawindow != null)
                {
                    var result = await arenawindow.ShowMessageAsync("About",
                        "Thank you for using my plugin! This plugin was made by Rembound. Do you want to visit my website?",
                        MessageDialogStyle.AffirmativeAndNegative, settings);

                    if (result == MessageDialogResult.Affirmative)
                    {
                        Process.Start(@"http://rembound.com/?from=ArenaHelper");
                    }
                }
            }
            catch
            {

            }
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

        public async void OnCardClick(int index)
        {
            // Override detection
            if (!configdata.manualclicks)
            {
                return;
            }

            if (state == PluginState.DetectedCards)
            {
                // Manually pick a card
                await PickCard(index);
            }

        }

        public void OnConfigureHero()
        {
            // Override hero detection
            configurehero = !configurehero;
            SetState(state);
        }

        // Configure hero click
        public async void OnCHeroClick(int index)
        {
            configurehero = false;

            if (index >= 0 && index < herohashlist.Count)
            {
                await PickHero(index);
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

        private void SaveDeck(bool autosave)
        {
            // Save deck
            Deck deck = new Deck();
            deck.Name = arenadata.deckname;
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

            if (!autosave)
            {
                // Set the new deck
                Helper.MainWindow.SetNewDeck(deck);

                // Activate the window
                Helper.MainWindow.ActivateWindow();
            }
            else
            {
                // Set the new deck in editing mode
                Helper.MainWindow.SetNewDeck(deck, true);

                // Save the deck
                Helper.MainWindow.SaveDeck(true, SerializableVersion.Default, true);

                // Select the deck and make it active
                Helper.MainWindow.SelectDeck(deck, true);
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

        private async Task LoadArenaData(string filename)
        {
            // Init state
            SetState(PluginState.Idle);

            NewArena();

            if (File.Exists(filename))
            {
                // Set current filename
                currentfilename = filename;

                // Load the data
                arenadata = JsonConvert.DeserializeObject<ArenaData>(File.ReadAllText(filename));

                // Make sure there is a guid for legacy arena runs
                if (arenadata.deckguid == "")
                {
                    arenadata.deckguid = Guid.NewGuid().ToString();
                    SaveArenaData();
                }

                if (arenadata.pickedhero != "")
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
                await plugins.ResumeArena(arenadata, state);

                UpdateTitle();
                UpdateHero();
            }
            else
            {
                // No arena found, started a new one
                // Save the arena data
                SaveArenaData();
                await plugins.NewArena(arenadata);
            }
        }

        private void NewArena()
        {
            // Initialize variables
            currentfilename = "";

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
            arenadata.pickedhero = "";
            arenadata.detectedcards.Clear();
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
            detectedcards.Add(new DetectedInfo(-1));
            detectedcards.Add(new DetectedInfo(-1));
            detectedcards.Add(new DetectedInfo(-1));

            detectedheroes.Clear();
            detectedheroes.Add(new DetectedInfo(-1));
            detectedheroes.Add(new DetectedInfo(-1));
            detectedheroes.Add(new DetectedInfo(-1));

            detectedbighero.Clear();
            detectedbighero.Add(new DetectedInfo(-1));
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
            // Check for plugin updates
            CheckUpdate();
            
            await mutex.WaitAsync();
            try
            {
                if (arenawindow != null && state != PluginState.Done)
                {
                    testtext.Text = "";
                    stopwatch.Restart();

                    // Size updates
                    UpdateSize();

                    // Capture the screen
                    var hsrect = Helper.GetHearthstoneRect(false);
                    if (hsrect.Width > 0 && hsrect.Height > 0)
                    {
                        bool needsfocus = true;
                        if (configdata.manualclicks)
                        {
                            // With manual clicks, we don't need the focus
                            needsfocus = false;
                        }
                        fullcapture = Helper.CaptureHearthstone(new Point(0, 0), hsrect.Width, hsrect.Height, default(IntPtr), needsfocus);
                    }
                    else
                    {
                        fullcapture = null;
                    }

                    if (fullcapture != null)
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

                    testtext.Text += "\nElapsed: " + stopwatch.ElapsedMilliseconds;
                }
            }
            finally
            {
                mutex.Release();
            }
        }

        private void UpdateSize()
        {
            //Helper.MainWindow.Overlay.Width;
            //Helper.MainWindow.Overlay.Height;

            var hsrect = Helper.GetHearthstoneRect(false);
            if (hsrect.Width <= 0 || hsrect.Height <= 0)
            {
                return;
            }

            // Position card values
            for (int i = 0; i < valueoverlays.Count; i++)
            {
                Point cardpos = GetHSPos(hsrect, i * cardwidth + cardrect.X, cardrect.Y, scalewidth, scaleheight);
                Point cardsize = GetHSSize(hsrect, cardrect.Width, cardrect.Height - 8, scalewidth, scaleheight);

                Canvas.SetLeft(valueoverlays[i], cardpos.X + cardsize.X / 2 - valueoverlays[i].RenderSize.Width/2);
                Canvas.SetTop(valueoverlays[i], cardpos.Y + cardsize.Y);
            }

            Point advpos = GetHSPos(hsrect, cardrect.X, cardrect.Y + cardrect.Height - 8, scalewidth, scaleheight);
            //Point advsize = GetHSSize(hsrect, cardrect.Width, cardrect.Height, scalewidth, scaleheight);
            Canvas.SetLeft(adviceoverlay, advpos.X);
            Canvas.SetTop(adviceoverlay, advpos.Y + 52);
        }

        // Check if there are plugin updates
        // Code from: Hearthstone Collection Tracker Plugin
        private async void CheckUpdate()
        {
            if (!hasupdates)
            {
                if ((DateTime.Now - lastupdatecheck) > updatecheckinterval)
                {
                    lastupdatecheck = DateTime.Now;
                    var latestversion = await Update.GetLatestVersion();
                    if (latestversion != null)
                    {
                        hasupdates = latestversion > Version;
                    }
                }
            }

            if (hasupdates)
            {
                if (!Game.IsRunning && arenawindow != null && !showingupdatemessage)
                {
                    showingupdatemessage = true;

                    var settings = new MetroDialogSettings { AffirmativeButtonText = "Yes", NegativeButtonText = "Not now" };
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        if (arenawindow != null)
                        {
                            var result = await arenawindow.ShowMessageAsync("New Update available!",
                                "Do you want to download it?",
                                MessageDialogStyle.AffirmativeAndNegative, settings);

                            if (result == MessageDialogResult.Affirmative)
                            {
                                Process.Start(Update.releaseDownloadUrl);
                            }
                            hasupdates = false;
                            lastupdatecheck = DateTime.Now.AddDays(1);
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        showingupdatemessage = false;
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
            } else if (!stablearena && state != PluginState.Done)
            {
                ShowValueOverlay(false);
                SetAdviceText(DetectingArena);
                ShowAdviceOverlay(configdata.overlay);

                SetDetectingText(DetectingArena, DetectionWarning, "");
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
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
            }
            else if (newstate == PluginState.SearchBigHero)
            {
                ShowOverlay(false);
                arenawindow.DetectedHeroesWarning.Text = "";
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.DetectedHeroes)
            {
                ShowOverlay(false);
                arenawindow.DetectedHeroesWarning.Text = "";
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
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
            }
            else if (newstate == PluginState.DetectedCards)
            {
                ShowOverlay(configdata.overlay);
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Visible;
            }
            else if (newstate == PluginState.Done)
            {
                ShowOverlay(false);
                SetDetectingText("Done", DoneMessage, "");
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.ConfigureHeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        private async Task Detect()
        {
            try
            {
                // Detect arena
                ulong arenascreenhash = GetScreenHash(arenarect, scalewidth, scaleheight);

                // Screen is darker when picking a hero
                bool arenacheck2 = false;

                if (state == PluginState.DetectedHeroes || state == PluginState.SearchBigHero)
                {
                    if (GetHashDistance(arenahash2, arenascreenhash) < 10)
                    {
                        arenacheck2 = true;
                        testtext.Text += "Arenacheck2 true\n";
                    }
                }
                testtext.Text += "Arenacheck1: " + GetHashDistance(arenahash, arenascreenhash) + "\n";
                testtext.Text += "Arenacheck2: " + GetHashDistance(arenahash2, arenascreenhash) + "\n";


                if (GetHashDistance(arenahash, arenascreenhash) < 10 || arenacheck2)
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

                // Detect heroes and cards
                List<int> heroindices = DetectHeroes();
                List<int> cardindices = DetectCards();

                if (state == PluginState.SearchHeroes)
                {
                    // Searching for heroes
                    await SearchHeroes(heroindices, cardindices);
                }
                else if (state == PluginState.SearchBigHero)
                {
                    // Heroes detected, searching for big hero selection
                    await SearchBigHero(heroindices, cardindices);
                }
                else if (state == PluginState.DetectedHeroes)
                {
                    // Heroes detected, waiting
                    await WaitHeroPick(heroindices, cardindices);
                }
                else if (state == PluginState.SearchCards)
                {
                    // Searching for cards
                    await SearchCards(cardindices);
                }
                else if (state == PluginState.SearchCardValues)
                {
                    // Get card values
                    await SearchCardValues(cardindices);
                }
                else if (state == PluginState.DetectedCards)
                {
                    // Cards detected, waiting
                    await WaitCardPick(cardindices);
                }
            }
            catch (Exception e)
            {
                if (testtext != null)
                {
                    testtext.Text = "Error: " + e.Message + "\n" + e.ToString();
                }
            }

        }

        private async Task SearchHeroes(List<int> heroindices, List<int> cardindices)
        {
            if (ConfirmDetected(detectedheroes, heroindices, HeroConfirmations) == 3)
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

                await plugins.HeroesDetected(arenadata, hero0.name, hero1.name, hero2.name);

                // Show the heroes
                UpdateDetectedHeroes();

                SetState(PluginState.SearchBigHero);
            }
        }

        private async Task SearchBigHero(List<int> heroindices, List<int> cardindices)
        {
            List<int> bigheroindices = DetectBigHero();
            if (ConfirmDetected(detectedbighero, bigheroindices, BigHeroConfirmations) == 1)
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
                await WaitHeroPick(heroindices, cardindices);
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

        private async Task WaitHeroPick(List<int> heroindices, List<int> cardindices)
        {
            testtext.Text += "\nChoosing: " + herohashlist[detectedbighero[0].index].name + "\n";

            // All heroes detected, wait for pick
            if (GetUndectectedCount(heroindices) == 3 && GetUndectectedCount(cardindices) < 3)
            {
                // No heroes detected, at least one card detected
                // The player picked a hero
                await PickHero(detectedbighero[0].index);
            }
            else if (GetUndectectedCount(heroindices) == 0)
            {
                // Cancelled the choice
                ClearDetected();
                SetState(PluginState.SearchBigHero);

                // Restore gui
                ResetHeroSize();
            }
        }

        private async Task PickHero(int heroindex)
        {
            arenadata.pickedhero = herohashlist[heroindex].name;
            SaveArenaData();

            UpdateHero();

            await plugins.HeroPicked(arenadata, arenadata.pickedhero);

            // Show the card panel
            SetState(PluginState.SearchCards);
        }

        private async Task SearchCards(List<int> cardindices)
        {
            if (ConfirmDetected(detectedcards, cardindices, CardConfirmations) == 3)
            {
                // All cards detected

                // Save detected cards
                Card card0 = cardlist[detectedcards[0].index];
                Card card1 = cardlist[detectedcards[1].index];
                Card card2 = cardlist[detectedcards[2].index];
                arenadata.detectedcards.Add(new Tuple<string, string, string>(card0.Id, card1.Id, card2.Id));
                SaveArenaData();

                // Update the plugin
                await plugins.CardsDetected(arenadata, card0, card1, card2);

                UpdateDetectedCards();

                SetState(PluginState.SearchCardValues);

                // Call it immediately
                await SearchCardValues(cardindices);
            }
        }

        private async Task SearchCardValues(List<int> cardindices)
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
            List<string> values = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                values.Add(GetCardValue(newcards[i].Id));
            }

            // Get the plugin result
            List<string> pvalues = await plugins.GetCardValues(arenadata, newcards, values);

            // Override the values if the plugin has a result
            string advice = "";
            if (pvalues != null)
            {
                if (pvalues.Count >= 3)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        values[i] = pvalues[i];
                    }
                }

                // Set advice text
                if (pvalues.Count >= 4)
                {
                    advice = pvalues[3];
                }
            }

            // Show the card value
            arenawindow.Value0.Content = values[0];
            arenawindow.Value1.Content = values[1];
            arenawindow.Value2.Content = values[2];

            // Get the actual numerical value
            double maxvalue = 0;
            int maxvalueindex = 0;
            for (int i = 0; i < 3; i++)
            {
                double dval = GetNumericalValue(values[i]);
                if (i == 0 || dval > maxvalue)
                {
                    maxvalue = dval;
                    maxvalueindex = i;
                }
            }

            // Set value text
            for (int i = 0; i < valueoverlays.Count; i++)
            {
                SetValueText(i, values[i]);
                if (i == maxvalueindex)
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

            // Call it immediately
            await WaitCardPick(cardindices);
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

        public string GetCardValue(string id)
        {
            string value = "";
            HeroHashData herodata = GetHero(arenadata.pickedhero);
            if (herodata != null)
            {
                CardTierInfo cardtierinfo = GetCardTierInfo(id);
                if (cardtierinfo != null)
                {
                    if (herodata.index >= 0 && herodata.index < cardtierinfo.value.Count)
                    {
                        value = cardtierinfo.value[herodata.index];
                    }
                }
            }
            return value;
        }

        private async Task WaitCardPick(List<int> cardindices)
        {
            // All cards detected, wait for new pick

            int lastindex = arenadata.detectedcards.Count - 1;
            if (lastindex < 0)
                return;

            // Display detected cards
            testtext.Text += "\nPicking card " + (arenadata.pickedcards.Count + 1) + "/" + MaxCardCount;

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
                testtext.Text += "\nDetected " + i + ": " + cardname;
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
                testtext.Text += "\nPicked " + i + ": " + cardname;
            }

            // Skip this if we only allow manual picking
            if (configdata.manualclicks)
            {
                return;
            }

            // Get the click position
            CheckMouse();

            testtext.Text += "\nMouse: ";
            for (int i = 0; i < mouseindex.Count; i++)
            {
                testtext.Text += mouseindex[i] + " ";
            }

            // Check if a new card was detected
            bool newcard = false;
            for (int i = 0; i < 3; i++) {
                int cardindex = cardindices[i];
                if (cardindex != -1)
                {
                    if (dcards[i] != null)
                    {
                        if (dcards[i].Id != cardlist[cardindex].Id)
                        {
                            newcard = true;
                            break;
                        }
                    }
                }
            }

            if ((newcard || GetUndectectedCount(cardindices) == 3))
            {
                if (mouseindex.Count >= 1)
                {
                    // New card or no cards detected, the player picked a card
                    await PickCard(mouseindex[mouseindex.Count - 1]);

                    // Clear the mouse data to avoid double detection of clicks
                    mouseindex.Clear();
                }
                else
                {
                    // No click detected, missed a pick
                    // TODO: Missed a pick
                    Logger.WriteLine("Missed a pick");
                    await PickCard(-1);
                }
            }
        }

        private async Task PickCard(int pickindex)
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
            arenadata.pickedcards.Add(cardid);
            SaveArenaData();

            await plugins.CardPicked(arenadata, pickindex, pickedcard);

            if (arenawindow != null)
            {
                arenawindow.Update();
            }

            if (arenadata.pickedcards.Count == MaxCardCount)
            {
                SetState(PluginState.Done);
                await plugins.Done(arenadata);
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
                for (int i = 0; i < herohashlist.Count; i++)
                {
                    if (herohashlist[i].name == name)
                    {
                        return herohashlist[i];
                    }
                }
            }

            return null;
        }

        public static CardTierInfo GetCardTierInfo(string id)
        {
            if (id != "")
            {
                for (int i = 0; i < cardtierlist.Count; i++)
                {
                    if (cardtierlist[i].id == id)
                    {
                        return cardtierlist[i];
                    }
                }
            }

            return null;
        }

        public static PluginState GetState()
        {
            return state;
        }

        private static int GetUndectectedCount(List<int> indices)
        {
            int undetected = 0;
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] == -1)
                {
                    undetected++;
                }
            }
            return undetected;
        }

        private int ConfirmDetected(List<DetectedInfo> detected, List<int> indices, int confirmations)
        {
            int confirmed = 0;
            for (int i = 0; i < detected.Count; i++)
            {
                detected[i].Confirm(indices[i]);

                if (detected[i].index != -1 && detected[i].confirmations >= confirmations)
                {
                    confirmed++;
                }
            }
            return confirmed;
        }

        private List<int> DetectCards()
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                ulong cardhash = GetScreenCardHash(i);
                List<Tuple<int, int>> cardindices = FindHashIndex(cardhash, cardhashlist, maxcarddistance);

                if (cardindices.Count == 1)
                {
                    indices.Add(cardindices[0].Item1);
                }
                else
                {
                    indices.Add(-1);
                }

                testtext.Text += "\nHash" + i + ": " + string.Format("0x{0:X}", cardhash);
                foreach (Tuple<int, int> cardindex in cardindices)
                {
                    testtext.Text += ", " + cardlist[cardindex.Item1].Name + " (" + cardindex.Item2 + ")";
                }
            }
            return indices;
        }

        private List<int> DetectHeroes()
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                ulong herohash = GetScreenHeroHash(i);
                List<Tuple<int, int>> heroindices = FindHashIndex(herohash, herohashlist, maxherodistance);
                if (heroindices.Count == 1)
                {
                    indices.Add(heroindices[0].Item1);
                }
                else
                {
                    indices.Add(-1);
                }
                testtext.Text += "\nHero Hash" + i + ": " + string.Format("0x{0:X}", herohash);
                foreach (Tuple<int, int> heroindex in heroindices)
                {
                    testtext.Text += ", " + herohashlist[heroindex.Item1].name + " (" + heroindex.Item2 + ")";
                }
            }
            return indices;
        }

        private List<int> DetectBigHero()
        {
            List<int> indices = new List<int>();

            ulong bigherohash = GetScreenHash(portraitbigcroprect, scalewidth, scaleheight);
            List<Tuple<int, int>> bigheroindices = FindHashIndex(bigherohash, herohashlist, maxherodistance);
            if (bigheroindices.Count == 1)
            {
                indices.Add(bigheroindices[0].Item1);
            }
            else
            {
                indices.Add(-1);
            }

            testtext.Text += "\nBig Hero Hash: " + string.Format("0x{0:X}", bigherohash);
            foreach (Tuple<int, int> bigheroindex in bigheroindices)
            {
                testtext.Text += ", " + herohashlist[bigheroindex.Item1].name + " (" + bigheroindex.Item2 + ")";
            }
            testtext.Text += "\n";

            return indices;
        }
        

        private void UpdateTitle()
        {
            arenawindow.Header.Text = "Picking card " + (arenadata.pickedcards.Count + 1) + "/" + MaxCardCount;
            arenawindow.DeckName.Content = arenadata.deckname;
        }

        private void SetDetectingText(string title, string text, string text2)
        {
            arenawindow.DetectingHeader.Text = title;
            arenawindow.DetectingText.Text = text;
            arenawindow.DetectingText2.Text = text2;
        }

        private void UpdateHero()
        {
            HeroHashData hero = GetHero(arenadata.pickedhero);
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

        private void CheckMouse()
        {
            // Mouse events
            Point mousepos = GetMousePos();
            var hsrect = Helper.GetHearthstoneRect(false);
            mousepos.X -= hsrect.X;
            mousepos.Y -= hsrect.Y;

            short mousedown = GetAsyncKeyState(VK_LBUTTON);

            if (mousepos.X < 0 || mousepos.Y < 0 || mousepos.X >= hsrect.Width || mousepos.Y >= hsrect.Height)
            {
                return;
            }
            testtext.Text += "\nMousepos: " + mousepos.X + ", " + mousepos.Y;

            // Testing rectangle intersection clicking
            /*for (int i = 0; i < 3; i++)
            {
                Point testcardpos = GetHSPos(hsrect, i * cardwidth + cardrect.X, cardrect.Y, scalewidth, scaleheight);
                Point testcardsize = GetHSSize(hsrect, cardrect.Width, cardrect.Height, scalewidth, scaleheight);
                Rectangle cardclickrect = new Rectangle(testcardpos.X, testcardpos.Y, testcardsize.X, testcardsize.Y);
                if (cardclickrect.Contains(mousepos))
                {
                    testtext.Text += "\nButton " + i + " hover";
                }
            }*/

            if (mousedown != 0)
            {
                double mindist = 0;
                int closestmouseindex = 0;
                for (int i = 0; i < 3; i++)
                {
                    Point cardpos = GetHSPos(hsrect, i * cardwidth + cardrect.X, cardrect.Y, scalewidth, scaleheight);
                    Point cardsize = GetHSSize(hsrect, cardrect.Width, cardrect.Height, scalewidth, scaleheight);
                    int centerx = cardpos.X + cardsize.X / 2;
                    int centery = cardpos.Y + cardsize.Y / 2;
                    double dist = Math.Sqrt((centerx - mousepos.X) * (centerx - mousepos.X) + (centery - mousepos.Y) * (centery - mousepos.Y));
                    if (dist < mindist || i == 0)
                    {
                        closestmouseindex = i;
                        mindist = dist;
                    }
                }

                mouseindex.Add(closestmouseindex);
                if (mouseindex.Count > 5)
                {
                    mouseindex.RemoveAt(0);
                }
            }
        }

        private ulong GetScreenCardHash(int index)
        {
            // Check for a valid index
            if (index < 0 || index >= 3)
                return 0;

            var hsrect = Helper.GetHearthstoneRect(false);

            // Get the position and size of the card
            Point cardpos = GetHSPos(hsrect, index * cardwidth + cardrect.X, cardrect.Y, scalewidth, scaleheight);
            Point cardsize = GetHSSize(hsrect, cardrect.Width, cardrect.Height, scalewidth, scaleheight);

            // Copy a part of the screen
            Bitmap capture = fullcapture.Clone(new Rectangle(cardpos.X, cardpos.Y, cardsize.X, cardsize.Y), fullcapture.PixelFormat);

            ulong hash = 0;
            if (capture != null)
            {
                try
                {
                    CropBitmapRelative(ref capture, cardrect, cardcroprect);

                    System.Windows.Controls.Image imagecontrol = testimages[index];
                    hash = GetImageHash(capture, ref imagecontrol);
                }
                catch (Exception e)
                {
                    if (testtext != null)
                    {
                        testtext.Text = "Error2: " + e.Message + "\n" + e.ToString();
                    }
                }
            }

            return hash;
        }

        private ulong GetScreenHeroHash(int index)
        {
            // Check for a valid index
            if (index < 0 || index >= 3)
                return 0;

            var hsrect = Helper.GetHearthstoneRect(false);

            // Get the position and size
            Point pos = GetHSPos(hsrect, index * portraitwidth + portraitcroprect.X, portraitcroprect.Y, scalewidth, scaleheight);
            Point size = GetHSSize(hsrect, portraitcroprect.Width, portraitcroprect.Height, scalewidth, scaleheight);

            // Copy a part of the screen
            Bitmap capture = fullcapture.Clone(new Rectangle(pos.X, pos.Y, size.X, size.Y), fullcapture.PixelFormat);

            ulong hash = 0;
            if (capture != null)
            {
                try
                {
                    System.Windows.Controls.Image imagecontrol = testimages[index];
                    hash = GetImageHash(capture, ref imagecontrol);
                }
                catch (Exception e)
                {
                    if (testtext != null)
                    {
                        testtext.Text = "Error3: " + e.Message + "\n" + e.ToString();
                    }
                }
            }

            return hash;
        }

        private ulong GetScreenHash(Rectangle rect, int scalewidth, int scaleheight)
        {
            ulong hash = 0;
            var hsrect = Helper.GetHearthstoneRect(false);

            // Get the position and size of the card
            Point pos = GetHSPos(hsrect, rect.X, rect.Y, scalewidth, scaleheight);
            Point size = GetHSSize(hsrect, rect.Width, rect.Height, scalewidth, scaleheight);

            // Copy a part of the screen
            Bitmap capture = fullcapture.Clone(new Rectangle(pos.X, pos.Y, size.X, size.Y), fullcapture.PixelFormat);

            if (capture != null)
            {
                try
                {
                    System.Windows.Controls.Image imagecontrol = testimages[0];
                    hash = GetImageHash(capture, ref imagecontrol);
                }
                catch (Exception e)
                {
                    if (testtext != null)
                    {
                        testtext.Text = "Error4: " + e.Message + "\n" + e.ToString();
                    }
                }
            }

            return hash;
        }

        // Perceptual hash using the techniques from: http://www.hackerfactor.com/blog/?/archives/432-Looks-Like-It.html
        private ulong GetImageHash(Bitmap bitmap, ref System.Windows.Controls.Image imagecontrol)
        {
            Bitmap sourcebm = new Bitmap(bitmap);

            Image<Gray, float> sourceimage = new Image<Gray, float>(sourcebm);

            // Apply a convolution filter
            CvInvoke.Blur(sourceimage, sourceimage, new Size(4, 4), new Point(-1, -1));

            // Show image for debugging
            Image<Bgra, Byte> convimage = Image<Bgra, Byte>.FromIplImagePtr(sourceimage);
            ShowBitmap(convimage.ToBitmap(), ref imagecontrol);

            // Resize
            Image<Gray, float> resimage = new Image<Gray, float>(new Size(64, 64));
            CvInvoke.Resize(sourceimage, resimage, new Size(64, 64));
            ShowBitmap(resimage.ToBitmap(), ref imagecontrol);

            // DCT
            IntPtr compleximage = CvInvoke.cvCreateImage(resimage.Size, Emgu.CV.CvEnum.IplDepth.IplDepth32F, 1);
            CvInvoke.Dct(resimage, resimage, Emgu.CV.CvEnum.DctType.Forward);

            Image<Gray, float> dctimage = Image<Gray, float>.FromIplImagePtr(resimage);

            // Calculate the mean
            double mean = 0;
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    mean += dctimage[y, x].Intensity;
                }
            }
            mean -= dctimage[0, 0].Intensity;
            mean /= 63;

            // Calculate the hash
            ulong hash = 0;
            ulong index = 1;
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    Gray color = dctimage[y, x];
                    if (color.Intensity > mean)
                    {
                        hash |= index;
                        // For debugging
                        //bitmap.SetPixel(x, y, Color.FromArgb(255, 0, 0));
                    }
                    else
                    {
                        // For debugging
                        //bitmap.SetPixel(x, y, Color.FromArgb(0, 255, 0));
                    }

                    index <<= 1;
                }
            }

            return hash;
        }

        private int GetHashDistance(ulong hash1, ulong hash2)
        {
            ulong index = 1;
            int distance = 0;
            for (int i = 0; i < 64; i++)
            {
                if ((hash1 & index) != (hash2 & index))
                {
                    distance++;
                }

                index <<= 1;
            }

            return distance;
        }

        private List<Tuple<int, int>> FindHashIndex(ulong hash, IEnumerable<HashData> hashlist, int maxdistance)
        {
            int bestindex = -1;
            int bestdistance = 100;

            List<Tuple<int, int>> indices = new List<Tuple<int, int>>();
            int i = 0;
            foreach (var item in hashlist)
            {
                // Check all item hashes
                foreach (var itemhash in item.hashes)
                {
                    int distance = GetHashDistance(hash, itemhash);
                    if (distance < maxdistance)
                    {
                        if (distance < bestdistance)
                        {
                            bestindex = i;
                            bestdistance = distance;

                            indices.Clear();
                            indices.Add(new Tuple<int, int>(i, distance));
                        }
                        else if (bestindex != i && distance == bestdistance)
                        {
                            // Collision
                            indices.Add(new Tuple<int, int>(i, distance));
                        }
                    }
                }
                i++;
            }

            return indices;
        }

        private void CropBitmapRelative(ref Bitmap bm, Rectangle fullrect, Rectangle croprect)
        {
            double cropx = (double)(croprect.X - fullrect.X) / fullrect.Width;
            double cropy = (double)(croprect.Y - fullrect.Y) / fullrect.Height;
            double cropwidth = (double)croprect.Width / fullrect.Width;
            double cropheight = (double)croprect.Height / fullrect.Height;

            bm = bm.Clone(new Rectangle((int)(cropx * bm.Width), (int)(cropy * bm.Height), (int)(cropwidth * bm.Width), (int)(cropheight * bm.Height)), bm.PixelFormat);
        }


        private void ShowBitmap(Bitmap bm, ref System.Windows.Controls.Image imagecontrol)
        {
            if (imagecontrol != null)
            {

                MemoryStream ms = new MemoryStream();
                bm.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.EndInit();

                imagecontrol.Source = bi;
            }
        }

        private void LoadCards()
        {
            string assemblylocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            List<Card> cards = Game.GetActualCards();
            foreach (var card in cards)
            {
                // Add to the list
                cardlist.Add((Card)card.Clone());
            }

            string cardhashesfile = Path.Combine(assemblylocation, "data", "cardhashes.json");
            cardhashlist = JsonConvert.DeserializeObject<List<CardHashData>>(File.ReadAllText(cardhashesfile));

            // Load card tier info
            string cardtierfile = Path.Combine(assemblylocation, "data", "cardtier.json");
            cardtierlist = JsonConvert.DeserializeObject<List<CardTierInfo>>(File.ReadAllText(cardtierfile));
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

        // Reference resolution: 1280 x 960 (4:3)
        private Point GetHSPos(Rectangle hsrect, int x, int y, int width, int height)
        {
            // Get normalized position
            double nx = x / (double)width;
            double ny = y / (double)height;

            // Convert to actual position
            double ratio = ((double)width / (double)height) / ((double)hsrect.Width / hsrect.Height);
            int px = (int)((hsrect.Width * ratio * nx) + (hsrect.Width * (1 - ratio) / 2));
            int py = (int)(ny * hsrect.Height);
            return new Point(px, py);
        }

        // Doesn't work for too wide aspect ratios
        private Point GetHSSize(Rectangle hsrect, int x, int y, int width, int height)
        {
            double scalefactor = (double)hsrect.Height / height;

            return new Point((int)(scalefactor * x), (int)(scalefactor * y));
        }

        // Add overlay elements for debugging
        private void AddElements()
        {
            // Value overlay
            if (valueoverlays.Count == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Helper.MainWindow.Overlay != null)
                    {
                        ArenaHelper.Controls.ValueOverlay valuetext = new ArenaHelper.Controls.ValueOverlay();
                        valuetext.ValueText.Text = "Value";
                        Canvas.SetLeft(valuetext, 5);
                        Canvas.SetTop(valuetext, 5);
                        Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Add(valuetext);
                        valuetext.Visibility = System.Windows.Visibility.Hidden;
                        valueoverlays.Add(valuetext);
                    }
                }
            }

            // Advice overlay
            if (adviceoverlay == null)
            {
                adviceoverlay = new ArenaHelper.Controls.AdviceOverlay();
                adviceoverlay.AdviceText.Text = "";
                Canvas.SetLeft(adviceoverlay, 5);
                Canvas.SetTop(adviceoverlay, 5);
                Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Add(adviceoverlay);
                adviceoverlay.Visibility = System.Windows.Visibility.Hidden;
            }

            // Test text
            if (testtext == null)
            {
                if (Helper.MainWindow.Overlay != null)
                {
                    testtext = new HearthstoneTextBlock();
                    testtext.FontSize = 12;
                    testtext.Text = "Arena Helper";
                    Canvas.SetLeft(testtext, 5);
                    Canvas.SetTop(testtext, 5);

                    //Canvas CanvasInfo = (Canvas)Helper.MainWindow.Overlay.FindName("CanvasInfo");
                    Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Add(testtext);

                    testtext.Visibility = System.Windows.Visibility.Hidden;
                }
            }

            // Test images
            if (testimages.Count == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (Helper.MainWindow.Overlay != null)
                    {
                        System.Windows.Controls.Image testimage = new System.Windows.Controls.Image();

                        Canvas.SetLeft(testimage, 5 + i * 210);
                        Canvas.SetTop(testimage, 550);

                        Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Add(testimage);

                        testimage.Visibility = System.Windows.Visibility.Hidden;
                        testimages.Add(testimage);
                    }
                }
            }
        }

        private void RemoveElements()
        {
            // Value overlay
            for (int i = 0; i < valueoverlays.Count; i++)
            {
                if (Helper.MainWindow.Overlay != null)
                {
                    Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Remove(valueoverlays[i]);
                }
            }
            valueoverlays.Clear();

            // Advice overlay
            if (adviceoverlay != null)
            {
                if (Helper.MainWindow.Overlay != null)
                {
                    Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Remove(adviceoverlay);
                    adviceoverlay = null;
                }
            }

            if (testtext != null)
            {
                if (Helper.MainWindow.Overlay != null)
                {
                    Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Remove(testtext);
                    testtext = null;
                }
            }

            for (int i = 0; i < testimages.Count; i++)
            {
                if (Helper.MainWindow.Overlay != null)
                {
                    Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Remove(testimages[i]);
                }
            }
            testimages.Clear();
        }

    }
}
