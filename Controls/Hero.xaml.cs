using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ArenaHelper.Controls
{
    /// <summary>
    /// Interaction logic for Hero.xaml
    /// </summary>
    public partial class Hero : UserControl
    {
        public Hero()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler heromouseup = null;
        private void Hero_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (heromouseup != null)
            {
                heromouseup(sender, e);
            }
        }
    }
}
