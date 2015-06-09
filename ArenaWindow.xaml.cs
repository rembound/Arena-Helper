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
        public OnEvent onaboutclick = null;

        public delegate void OnOverrideClick(int index);
        public OnOverrideClick onheroclick = null;
        public OnOverrideClick oncardclick = null;

        public delegate void OnCheckbox(bool check);
        public OnCheckbox oncheckboxoverlay = null;
        public OnCheckbox oncheckboxmanual = null;

        public bool initconfig = false;

        public ArenaWindow()
        {
            InitializeComponent();

            DataContext = this;
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
            if (onaboutclick != null)
            {
                onaboutclick();
            }
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
    }
}