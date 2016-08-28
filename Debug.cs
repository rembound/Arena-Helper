using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArenaHelper
{
    public static class Debug
    {
        public static Controls.DebugTextBlock debugtext = null;
        public static List<System.Windows.Controls.Image> debugimages = null;

        public static void SetTextControl(Controls.DebugTextBlock control)
        {
            debugtext = control;
        }

        public static void SetImageControls(List<System.Windows.Controls.Image> controls)
        {
            debugimages = controls;
        }

        public static void Log(string msg)
        {
            if (debugtext != null)
            {
                debugtext.Text = msg;
            }
        }

        public static void AppendLog(string msg)
        {
            if (debugtext != null)
            {
                debugtext.Text += msg;
            }
        }
    }
}
