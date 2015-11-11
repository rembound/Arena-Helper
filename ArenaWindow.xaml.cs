#region

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Hearthstone_Deck_Tracker;
using Point = System.Drawing.Point;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using Hearthstone_Deck_Tracker.Hearthstone;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Navigation;
using System.Diagnostics;

#endregion

namespace ArenaHelper
{
    public partial class ArenaWindow
    {
        private Card _card0;
        private Card _card1;
        private Card _card2;

        public delegate void OnEvent();
        public OnEvent onbuttonnewarenaclick = null;
        public OnEvent onbuttonsaveclick = null;
        public OnEvent onwindowlocation = null;
        public OnEvent onconfigurehero = null;

        public delegate void OnOverrideClick(int index);
        public OnOverrideClick onheroclick = null;
        public OnOverrideClick oncardclick = null;
        public OnOverrideClick oncheroclick = null;

        public delegate void OnCheckbox(bool check);
        public OnCheckbox oncheckboxoverlay = null;
        public OnCheckbox oncheckboxmanual = null;
        public OnCheckbox oncheckboxautosave = null;
        public OnCheckbox oncheckboxdebug = null;

        public bool initconfig = false;

        public ArenaWindow()
        {
            InitializeComponent();

            DataContext = this;

            // Add handlers
            CHero0.heromouseup += CHero0MouseUp;
            CHero1.heromouseup += CHero1MouseUp;
            CHero2.heromouseup += CHero2MouseUp;
            CHero3.heromouseup += CHero3MouseUp;
            CHero4.heromouseup += CHero4MouseUp;
            CHero5.heromouseup += CHero5MouseUp;
            CHero6.heromouseup += CHero6MouseUp;
            CHero7.heromouseup += CHero7MouseUp;
            CHero8.heromouseup += CHero8MouseUp;
            CHero9.heromouseup += CHero9MouseUp;
        }

        public Card Card0
        {
            get { return _card0; }
            set
            {
                _card0 = value;
            }
        }

        public Card Card1
        {
            get { return _card1; }
            set
            {
                _card1 = value;
            }
        }

        public Card Card2
        {
            get { return _card2; }
            set
            {
                _card2 = value;
            }
        }

        public string StringDonate
        {
            get { return "https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=info%40rembound%2ecom&lc=NL&item_name=Rembound%2ecom&currency_code=USD&bn=PP%2dDonationsBF%3abtn_donate_LG%2egif%3aNonHosted"; }
        }

        public string StringWebsite
        {
            get { return "http://rembound.com/?from=ArenaHelper"; }
        }

        public string StringGitHub
        {
            get { return "https://github.com/rembound/Arena-Helper"; }
        }


        public void Update()
        {
            DataContext = null;
            DataContext = this;
        }

        private void MetroWindow_LocationChanged(object sender, EventArgs e)
        {
            if (onwindowlocation != null)
            {
                onwindowlocation();
            }
        }

        public Thickness TitleBarMargin
        {
            get { return new Thickness(0, TitlebarHeight, 0, 0); }
        }

        private void ButtonOptions_Click(object sender, RoutedEventArgs e)
        {
            FlyoutOptions.IsOpen = true;
        }

        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            FlyoutAbout.IsOpen = true;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
        }

        private void AboutDonate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(StringDonate);
        }

        private void AboutVisitWebsite_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(StringWebsite);
        }

        private void AboutVisitGitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(StringGitHub);
        } 

        private void AboutButtonClose_Click(object sender, RoutedEventArgs e)
        {
            FlyoutAbout.IsOpen = false;
        }

        private void ButtonNewArena_Click(object sender, RoutedEventArgs e)
        {
            if (onbuttonnewarenaclick != null)
            {
                onbuttonnewarenaclick();
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (onbuttonsaveclick != null)
            {
                onbuttonsaveclick();
            }
        }

        private void HeroBorder0_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Override mouse detection
            if (onheroclick != null)
            {
                onheroclick(0);
            }
        }

        private void HeroBorder1_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Override mouse detection
            if (onheroclick != null)
            {
                onheroclick(1);
            }
        }

        private void HeroBorder2_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Override mouse detection
            if (onheroclick != null)
            {
                onheroclick(2);
            }
        }

        private void CardControl0_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Override mouse detection
            if (oncardclick != null)
            {
                oncardclick(0);
            }
        }

        private void CardControl1_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Override mouse detection
            if (oncardclick != null)
            {
                oncardclick(1);
            }
        }

        private void CardControl2_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Override mouse detection
            if (oncardclick != null)
            {
                oncardclick(2);
            }
        }


        private void CHero0MouseUp(object sender, RoutedEventArgs e)
        {
            if (oncheroclick != null)
            {
                oncheroclick(0);
            }
        }

        private void CHero1MouseUp(object sender, RoutedEventArgs e)
        {
            if (oncheroclick != null)
            {
                oncheroclick(1);
            }
        }

        private void CHero2MouseUp(object sender, RoutedEventArgs e)
        {
            if (oncheroclick != null)
            {
                oncheroclick(2);
            }
        }

        private void CHero3MouseUp(object sender, RoutedEventArgs e)
        {
            if (oncheroclick != null)
            {
                oncheroclick(3);
            }
        }

        private void CHero4MouseUp(object sender, RoutedEventArgs e)
        {
            if (oncheroclick != null)
            {
                oncheroclick(4);
            }
        }

        private void CHero5MouseUp(object sender, RoutedEventArgs e)
        {
            if (oncheroclick != null)
            {
                oncheroclick(5);
            }
        }

        private void CHero6MouseUp(object sender, RoutedEventArgs e)
        {
            if (oncheroclick != null)
            {
                oncheroclick(6);
            }
        }

        private void CHero7MouseUp(object sender, RoutedEventArgs e)
        {
            if (oncheroclick != null)
            {
                oncheroclick(7);
            }
        }

        private void CHero8MouseUp(object sender, RoutedEventArgs e)
        {
            if (oncheroclick != null)
            {
                oncheroclick(8);
            }
        }

        private void CHero9MouseUp(object sender, RoutedEventArgs e)
        {
            if (oncheroclick != null)
            {
                oncheroclick(9);
            }
        }

        private void CheckBoxOverlay_Checked(object sender, RoutedEventArgs e)
        {
            if (oncheckboxoverlay != null)
            {
                oncheckboxoverlay(true);
            }
        }

        private void CheckBoxOverlay_Unchecked(object sender, RoutedEventArgs e)
        {
            if (oncheckboxoverlay != null)
            {
                oncheckboxoverlay(false);
            }
        }

        private void CheckBoxManual_Checked(object sender, RoutedEventArgs e)
        {
            if (oncheckboxmanual != null)
            {
                oncheckboxmanual(true);
            }
        }

        private void CheckBoxManual_Unchecked(object sender, RoutedEventArgs e)
        {
            if (oncheckboxmanual != null)
            {
                oncheckboxmanual(false);
            }
        }

        private void CheckBoxAutoSave_Checked(object sender, RoutedEventArgs e)
        {
            if (oncheckboxautosave != null)
            {
                oncheckboxautosave(true);
            }
        }

        private void CheckBoxAutoSave_Unchecked(object sender, RoutedEventArgs e)
        {
            if (oncheckboxautosave != null)
            {
                oncheckboxautosave(false);
            }
        }

        private void CheckBoxDebug_Checked(object sender, RoutedEventArgs e)
        {
            if (oncheckboxdebug != null)
            {
                oncheckboxdebug(true);
            }
        }

        private void CheckBoxDebug_Unchecked(object sender, RoutedEventArgs e)
        {
            if (oncheckboxdebug != null)
            {
                oncheckboxdebug(false);
            }
        }

        private void ConfigureHero_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Override hero detection
            if (onconfigurehero != null)
            {
                onconfigurehero();
            }
        }
    }
}