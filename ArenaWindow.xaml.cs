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

#endregion

namespace ArenaHelper
{
    public partial class ArenaWindow
    {
        private Card _card0;
        private Card _card1;
        private Card _card2;

        public delegate void OnEvent();
        public OnEvent onbutton1click = null;
        public OnEvent onbutton2click = null;
        public OnEvent onwindowlocation = null;

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

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            if (onbutton1click != null)
            {
                onbutton1click();
            }
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (onbutton2click != null)
            {
                onbutton2click();
            }
        }

        private void MetroWindow_LocationChanged(object sender, EventArgs e)
        {
            if (onwindowlocation != null)
            {
                onwindowlocation();
            }
        }
    }
}