﻿using System;
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

            public ConfigData()
            {
                windowx = 100;
                windowy = 100;
                manualclicks = false;
                overlay = true;
                debug = false;
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
            public ulong hash;

            public HashData(ulong hash)
            {
                this.hash = hash;
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

            public HeroHashData(int index, string name, ulong hash, string image)
                : base(hash)
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
            public List<string> detectedheroes;
            public string pickedhero;
            public List<Tuple<string, string, string>> detectedcards;
            public List<string> pickedcards;

            public ArenaData()
            {
                deckname = "";
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

        public enum PluginState { Idle, SearchHeroes, SearchBigHero, DetectedHeroes, SearchCards, DetectedCards, Done };

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

        System.Windows.Shapes.Rectangle highlightcard = null;

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
        ulong arenahash2 = 16000266435827628787; // Dark arena hash in PluginState.DetectedHeroes
        bool inarena = false;
        bool stablearena = false;
        Stopwatch arenastopwatch;

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

        private const int ArenaDetectionTime = 750;
        private const int MaxCardCount = 30;
        private const int HeroConfirmations = 3;
        private const int CardConfirmations = 3;
        private const int BigHeroConfirmations = 0; // Confirm immediately
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
            get { return new Version("0.2.1"); }
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
            herohashlist.Add(new HeroHashData(0, "Warrior", 13776678289873991291, "warrior_small.png"));
            herohashlist.Add(new HeroHashData(1, "Shaman", 18366959783178990451, "shaman_small.png"));
            herohashlist.Add(new HeroHashData(2, "Rogue", 5643619427529904809, "rogue_small.png"));
            herohashlist.Add(new HeroHashData(3, "Paladin", 11505795398351105139, "paladin_small.png"));
            herohashlist.Add(new HeroHashData(4, "Hunter", 2294799430464257123, "hunter_small.png"));
            herohashlist.Add(new HeroHashData(5, "Druid", 5433851923975358071, "druid_small.png"));
            herohashlist.Add(new HeroHashData(6, "Warlock", 10186248321710093033, "warlock_small.png"));
            herohashlist.Add(new HeroHashData(7, "Mage", 15770007155810004267, "mage_small.png"));
            herohashlist.Add(new HeroHashData(8, "Priest", 15052908377040876499, "priest_small.png"));

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

        private void ActivateArenaWindow()
        {
            if (arenawindow == null)
            {
                InitializeMainWindow();
                arenawindow.Show();
            }
            else
            {
                arenawindow.Activate();
            }
        }

        protected void InitializeMainWindow()
        {
            if (arenawindow == null)
            {
                arenawindow = new ArenaWindow();

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

                arenawindow.onbuttonnewarenaclick = new ArenaWindow.OnEvent(OnButtonNewArenaClick);
                arenawindow.onbuttonsaveclick = new ArenaWindow.OnEvent(OnButtonSaveClick);
                arenawindow.onwindowlocation = new ArenaWindow.OnEvent(OnWindowLocation);
                arenawindow.onaboutclick = new ArenaWindow.OnEvent(OnAboutClick);

                arenawindow.onheroclick = new ArenaWindow.OnOverrideClick(OnHeroClick);
                arenawindow.oncardclick = new ArenaWindow.OnOverrideClick(OnCardClick);

                arenawindow.oncheckboxoverlay = new ArenaWindow.OnCheckbox(OnCheckboxOverlay);
                arenawindow.oncheckboxmanual = new ArenaWindow.OnCheckbox(OnCheckboxManual);

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

            // Overlay
            //ShowOverlay(configdata.overlay);
        }

        private void ShowOverlay(bool show)
        {
            System.Windows.Visibility vis = System.Windows.Visibility.Hidden;
            if (show)
            {
                vis = System.Windows.Visibility.Visible;
            }

            if ( highlightcard != null )
            {
                highlightcard.Visibility = vis;
            }

            foreach (var overlay in valueoverlays)
            {
                overlay.Visibility = vis;
            }
            adviceoverlay.Visibility = vis;
        }

        private void SaveConfig()
        {
            string filename = Path.Combine(DataDir, ConfigFile);
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);

            string json = JsonConvert.SerializeObject(configdata, Formatting.Indented);
            File.WriteAllText(filename, json);
        }

        public void OnButtonNewArenaClick()
        {
            NewArena();
            SaveArenaData();
            plugins.NewArena(arenadata);
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
                        "Thank you for using my plugin! This plugin was made by Rembound. Do you want to visit my site?",
                        MessageDialogStyle.AffirmativeAndNegative, settings);

                    if (result == MessageDialogResult.Affirmative)
                    {
                        Process.Start(@"http://rembound.com");
                    }
                }
            }
            catch
            {

            }
        }

        public void OnButtonSaveClick()
        {
            SaveDeck();
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

        public void OnWindowLocation()
        {
            // Set window location
            configdata.windowx = (int)arenawindow.Left;
            configdata.windowy = (int)arenawindow.Top;

            // Don't save yet
            //SaveConfig();
        }

        private void SaveDeck()
        {
            // Save deck
            Deck deck = new Deck();
            deck.Name = arenadata.deckname;
            deck.IsArenaDeck = true;

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

            // Set the new deck
            Helper.MainWindow.SetNewDeck(deck);

            // Activate the window
            Helper.MainWindow.ActivateWindow();
        }

        private void SaveArenaData()
        {
            if (!Directory.Exists(DeckDataDir))
                Directory.CreateDirectory(DeckDataDir);

            if (currentfilename == "")
            {
                currentfilename = CreateDeckFilename();
            }

            string json = JsonConvert.SerializeObject(arenadata, Formatting.Indented);
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

            if (File.Exists(filename))
            {
                // Set current filename
                currentfilename = filename;

                // Load the data
                arenadata = JsonConvert.DeserializeObject<ArenaData>(File.ReadAllText(filename));

                if (arenadata.pickedhero != "")
                {
                    // Hero is picked
                    if (arenadata.pickedcards.Count == MaxCardCount) {
                        // All cards picked
                        SetState(PluginState.Done);
                    } else {
                        // Not all cards picked
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

            ClearDetected();

            arenawindow.HeroImage0.Source = null;
            arenawindow.HeroImage1.Source = null;
            arenawindow.HeroImage2.Source = null;

            arenawindow.Card0 = null;
            arenawindow.Card1 = null;
            arenawindow.Card2 = null;
            arenawindow.Update();

            // Clear data
            arenadata.deckname = Helper.ParseDeckNameTemplate(Config.Instance.ArenaDeckNameTemplate);
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

        public void OnUpdate()
        {
            // Check for plugin updates
            CheckUpdate();

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
                    Detect();
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
            Canvas.SetTop(adviceoverlay, advpos.Y + 50);
        }

        // Check if there are plugin updates
        // Code from: Hearthstone Collection Tracker Plugin
        private async Task CheckUpdate()
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

            if (!stablearena && state != PluginState.Done)
            {
                ShowOverlay(false);
                SetDetectingText("Detecting arena...", DetectionWarning);
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.SearchHeroes)
            {
                ShowOverlay(false);
                SetDetectingText("Detecting heroes...", DetectionWarning);
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.SearchBigHero)
            {
                ShowOverlay(false);
                arenawindow.DetectedHeroesWarning.Text = "";
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.DetectedHeroes)
            {
                ShowOverlay(false);
                arenawindow.DetectedHeroesWarning.Text = "";
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.SearchCards)
            {
                ShowOverlay(false);
                ClearDetected();
                SetDetectingText("Detecting cards...", DetectionWarning);
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
            }
            else if (newstate == PluginState.DetectedCards)
            {
                ShowOverlay(configdata.overlay);
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Visible;
            }
            else if (newstate == PluginState.Done)
            {
                ShowOverlay(false);
                SetDetectingText("Done", DoneMessage);
                arenawindow.DetectingPanel.Visibility = System.Windows.Visibility.Visible;
                arenawindow.HeroPanel.Visibility = System.Windows.Visibility.Hidden;
                arenawindow.CardPanel.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        private void Detect()
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
                    SearchHeroes(heroindices, cardindices);
                }
                else if (state == PluginState.SearchBigHero)
                {
                    // Heroes detected, searching for big hero selection
                    SearchBigHero(heroindices, cardindices);
                }
                else if (state == PluginState.DetectedHeroes)
                {
                    // Heroes detected, waiting
                    WaitHeroPick(heroindices, cardindices);
                }
                else if (state == PluginState.SearchCards)
                {
                    // Searching for cards
                    SearchCards(cardindices);
                }
                else if (state == PluginState.DetectedCards)
                {
                    // Cards detected, waiting
                    WaitCardPick(cardindices);
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

        private void SearchHeroes(List<int> heroindices, List<int> cardindices)
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

                plugins.HeroesDetected(arenadata, hero0.name, hero1.name, hero2.name);

                // Show the heroes
                UpdateDetectedHeroes();

                SetState(PluginState.SearchBigHero);
            }
        }

        private void SearchBigHero(List<int> heroindices, List<int> cardindices)
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
                WaitHeroPick(heroindices, cardindices);
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
            arenawindow.HeroBorder0.Margin = newmargin;
            arenawindow.HeroBorder1.Margin = newmargin;
            arenawindow.HeroBorder2.Margin = newmargin;

            switch (index)
            {
                case 0:
                    arenawindow.HeroImage0.Width = width;
                    arenawindow.HeroImage0.Height = height;
                    arenawindow.HeroBorder0.Width = width+8;
                    arenawindow.HeroBorder0.Height = height+8;
                    break;
                case 1:
                    arenawindow.HeroImage1.Width = width;
                    arenawindow.HeroImage1.Height = height;
                    arenawindow.HeroBorder1.Width = width+8;
                    arenawindow.HeroBorder1.Height = height+8;

                    break;
                case 2:
                    arenawindow.HeroImage2.Width = width;
                    arenawindow.HeroImage2.Height = height;
                    arenawindow.HeroBorder2.Width = width+8;
                    arenawindow.HeroBorder2.Height = height+8;
                    break;
                default:
                    break;
            }
            arenawindow.Update();
        }

        private void WaitHeroPick(List<int> heroindices, List<int> cardindices)
        {
            testtext.Text += "\nChoosing: " + herohashlist[detectedbighero[0].index].name + "\n";

            // All heroes detected, wait for pick
            if (GetUndectectedCount(heroindices) == 3 && GetUndectectedCount(cardindices) < 3)
            {
                // No heroes detected, at least one card detected
                // The player picked a hero
                arenadata.pickedhero = herohashlist[detectedbighero[0].index].name;
                SaveArenaData();

                UpdateHero();

                plugins.HeroPicked(arenadata, arenadata.pickedhero);

                // Show the card panel
                SetState(PluginState.SearchCards);
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

        private int findBestIndex(List<string> values)
        {
            // High light the best card.
            int bestindex = 0;
            double highestvalue = 0;
            Regex rgx = new Regex(@"([\.\d]+)");
            for (int i = 0; i < 3; i++)
            {
                // strip any junk
                MatchCollection matches = rgx.Matches(values[i]);
                if (matches.Count > 0)
                {
                    double value = 0;
                    try
                    {
                        value = Convert.ToDouble(matches[0].ToString());
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLine("We got an exception " + e.Message);
                    }
                    if (value > highestvalue)
                    {
                        bestindex = i;
                        highestvalue = value;
                    }
                }
                else
                {
                    Logger.WriteLine("Failed to get valid score from: " + values[i]);
                }
            }
            return bestindex;
        }

        private void highLightBestCard(int bestindex)
        {
            var hsrect = Helper.GetHearthstoneRect(false);

            // just to be safe
            if (highlightcard!=null)
            {
                Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Remove(highlightcard);
                highlightcard = null;
            }
            
            // Get the position and size of the card
            Point cardpos = GetHSPos(hsrect, (bestindex * cardwidth) + cardrect.X, cardrect.Y + 40, scalewidth, scaleheight);
            Point cardsize = GetHSSize(hsrect, cardrect.Width, cardrect.Height-50, scalewidth, scaleheight);

            highlightcard = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Red,
                StrokeThickness = 10
            };
            highlightcard.Width = cardsize.X;
            highlightcard.Height = cardsize.Y;

            Canvas.SetLeft(highlightcard, cardpos.X);
            Canvas.SetTop(highlightcard, cardpos.Y);

            Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Add(highlightcard);
        }

        private void SearchCards(List<int> cardindices)
        {
            if (ConfirmDetected(detectedcards, cardindices, CardConfirmations) == 3)
            {
                // All cards detected
                List<Card> newcards = new List<Card>();
                for (int i = 0; i < 3; i++)
                {
                    newcards.Add(cardlist[detectedcards[i].index]);
                }

                // Show the cards
                arenawindow.Card0 = newcards[0];
                arenawindow.Card1 = newcards[1];
                arenawindow.Card2 = newcards[2];

                // Add default values from the tierlist
                List<string> values = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    values.Add(GetCardValue(newcards[i].Id));
                }

                // Get the plugin result
                List<string> pvalues = plugins.GetCardValues(arenadata, newcards, values);

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
                        //adviceoverlay.AdviceText.Text = "Pick one! The quick brown fox jumps over the lazy dog. The quick brown fox jumps over the lazy dog. dsf sThe quick brown fox jumps over the lazy dog.The quick brown fox jumps over the lazy dog. The quick brown fox jumps over the lazy dog. dsf sThe quick brown fox jumps over the lazy dog.";
                        advice = pvalues[3];
                    }
                }

                // Show the card value
                arenawindow.Value0.Content = values[0];
                arenawindow.Value1.Content = values[1];
                arenawindow.Value2.Content = values[2];

                // Set value text
                for (int i=0; i<valueoverlays.Count; i++)
                {
                    valueoverlays[i].ValueText.Text = values[i];
                }
                adviceoverlay.AdviceText.Text = advice;

                int bestindex = findBestIndex(values);
                highLightBestCard(bestindex);

                arenawindow.Update();

                SetState(PluginState.DetectedCards);
            }
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

        private void WaitCardPick(List<int> cardindices)
        {
            // All cards detected, wait for new pick

            // Display detected cards
            testtext.Text += "\nPicking card " + (arenadata.pickedcards.Count + 1) + "/" + MaxCardCount;
            for (int i = 0; i < detectedcards.Count; i++)
            {
                testtext.Text += "\nDetected " + i + ": " + cardlist[detectedcards[i].index].Name;
            }

            // Display picked cards
            for (int i = 0; i < arenadata.pickedcards.Count; i++)
            {
                Card card = GetCard(arenadata.pickedcards[i]);
                if (card != null)
                {
                    testtext.Text += "\nPicked " + i + ": " + card.Name;
                }
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
                    if (detectedcards[i].index != cardindex)
                    {
                        newcard = true;
                        break;
                    }
                }
            }

            if ((newcard || GetUndectectedCount(cardindices) == 3) && mouseindex.Count >= 1)
            {
                // New card or no cards detected, the player picked a card

                int pickindex = -1;
                if (mouseindex.Count > 0)
                {
                    pickindex = mouseindex[mouseindex.Count - 1];
                }
                PickCard(pickindex);

                // Clear the mouse data to avoid double detection of clicks
                mouseindex.Clear();
            }
        }

        private void PickCard(int pickindex)
        {
            if (highlightcard != null)
            {
                Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Remove(highlightcard);
                highlightcard = null;
            }

            string cardid0 = cardlist[detectedcards[0].index].Id;
            string cardid1 = cardlist[detectedcards[1].index].Id;
            string cardid2 = cardlist[detectedcards[2].index].Id;
            arenadata.detectedcards.Add(new Tuple<string, string, string>(cardid0, cardid1, cardid2));

            // Add to pickedcards
            string cardid = "";
            Card pickedcard = null;
            if (pickindex >= 0)
            {
                pickedcard = cardlist[detectedcards[pickindex].index];
                cardid = pickedcard.Id;
            }
            arenadata.pickedcards.Add(cardid);
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
        }

        public static Card GetCard(string id)
        {
            for (int i=0; i<cardlist.Count; i++) {
                if (cardlist[i].Id == id) {
                    return cardlist[i];
                }
            }

            return null;
        }

        public static HeroHashData GetHero(string name)
        {
            for (int i = 0; i < herohashlist.Count; i++)
            {
                if (herohashlist[i].name == name)
                {
                    return herohashlist[i];
                }
            }

            return null;
        }

        public static CardTierInfo GetCardTierInfo(string id)
        {
            for (int i = 0; i < cardtierlist.Count; i++)
            {
                if (cardtierlist[i].id == id)
                {
                    return cardtierlist[i];
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
                List<Tuple<int, int>> cardindices = FindHashIndex(cardhash, cardhashlist);

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
                List<Tuple<int, int>> heroindices = FindHashIndex(herohash, herohashlist);
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
            List<Tuple<int, int>> bigheroindices = FindHashIndex(bigherohash, herohashlist);
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

        private void SetDetectingText(string title, string text)
        {
            arenawindow.DetectingHeader.Text = title;
            arenawindow.DetectingText.Text = text;
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

            // All heroes detected
            HeroHashData hero0 = GetHero(arenadata.detectedheroes[0]);
            HeroHashData hero1 = GetHero(arenadata.detectedheroes[1]);
            HeroHashData hero2 = GetHero(arenadata.detectedheroes[2]);

            if (hero0 == null || hero1 == null || hero2 == null)
                return;

            // Show the heroes
            arenawindow.HeroImage0.Source = new BitmapImage(new Uri(@"/HearthstoneDeckTracker;component/Resources/" + hero0.image, UriKind.Relative));
            arenawindow.HeroImage1.Source = new BitmapImage(new Uri(@"/HearthstoneDeckTracker;component/Resources/" + hero1.image, UriKind.Relative));
            arenawindow.HeroImage2.Source = new BitmapImage(new Uri(@"/HearthstoneDeckTracker;component/Resources/" + hero2.image, UriKind.Relative));

            arenawindow.Hero0.Text = hero0.name;
            arenawindow.Hero1.Text = hero1.name;
            arenawindow.Hero2.Text = hero2.name;
            arenawindow.Update();
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
                catch (Exception)
                {
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
            Matrix<float> kernel = ((double)1 / 16) * new Matrix<float>(new float[4, 4] { { 1, 1, 1, 1 }, { 1, 1, 1, 1 }, { 1, 1, 1, 1 }, { 1, 1, 1, 1 } });
            Point anchor = new Point(-1, -1);
            CvInvoke.cvFilter2D(sourceimage, sourceimage, kernel, anchor);

            // Show image for debugging
            Image<Bgra, Byte> convimage = Image<Bgra, Byte>.FromIplImagePtr(sourceimage);
            ShowBitmap(convimage.ToBitmap(), ref imagecontrol);

            // Resize
            sourceimage = sourceimage.Resize(64, 64, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR);

            // DCT
            IntPtr compleximage = CvInvoke.cvCreateImage(sourceimage.Size, Emgu.CV.CvEnum.IPL_DEPTH.IPL_DEPTH_32F, 1);
            CvInvoke.cvDCT(sourceimage, compleximage, Emgu.CV.CvEnum.CV_DCT_TYPE.CV_DXT_FORWARD);
            Image<Gray, float> dctimage = Image<Gray, float>.FromIplImagePtr(compleximage);

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

        private List<Tuple<int, int>> FindHashIndex(ulong hash, IEnumerable<HashData> hashlist)
        {
            int bestindex = -1;
            int bestdistance = 100;

            List<Tuple<int, int>> indices = new List<Tuple<int, int>>();
            int i = 0;
            foreach (var item in hashlist)
            {
                //int distance = GetHashDistance(hash, hashlist[i].hash);
                int distance = GetHashDistance(hash, item.hash);
                if (distance < 10)
                {
                    if (distance < bestdistance)
                    {
                        bestindex = i;
                        bestdistance = distance;

                        indices.Clear();
                        indices.Add(new Tuple<int, int>(i, distance));
                    }
                    else if (distance == bestdistance)
                    {
                        // Collision
                        indices.Add(new Tuple<int, int>(i, distance));
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

            if (highlightcard != null)
            {
                if (Helper.MainWindow.Overlay != null)
                {
                    Hearthstone_Deck_Tracker.API.Overlay.OverlayCanvas.Children.Remove(highlightcard);
                    highlightcard = null;
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
