using Hearthstone_Deck_Tracker;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace ArenaHelper
{
    public class Detection
    {
        // Left card dimensions
        public static int scalewidth = 1280;
        public static int scaleheight = 960;
        public static Rectangle cardrect = new Rectangle(100, 152, 260, 393);
        public static int cardwidth = 250;

        // Reference resolution: 1280 x 960 (4:3)
        public static Point GetHSPos(Rectangle hsrect, int x, int y, int width, int height)
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
        public static Point GetHSSize(Rectangle hsrect, int x, int y, int width, int height)
        {
            double scalefactor = (double)hsrect.Height / height;

            return new Point((int)(scalefactor * x), (int)(scalefactor * y));
        }

    }
}
