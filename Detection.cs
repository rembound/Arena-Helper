using Hearthstone_Deck_Tracker;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Emgu.CV;
using Emgu.CV.Structure;

namespace ArenaHelper
{
    public class Detection
    {
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

        private const int maxcarddistance = 10;
        private const int maxherodistance = 14;

        // Arena detection
        public static Rectangle arenarect = new Rectangle(305, 0, 349, 69);
        public static ulong arenahash = 14739256890895383027;
        public static ulong arenahash2 = 18342314164188135155; // Dark arena hash in PluginState.DetectedHeroes

        // Left card dimensions
        public static int scalewidth = 1280;
        public static int scaleheight = 960;
        public static Rectangle cardrect = new Rectangle(100, 152, 260, 393);
        public static Rectangle cardcroprect = new Rectangle(127, 226, 204, 157);
        public static int cardwidth = 250;

        // Portrait detection
        public static Rectangle portraitcroprect = new Rectangle(143, 321, 173, 107);
        public static int portraitwidth = 250;

        // Big portrait detection
        Rectangle portraitbigcroprect = new Rectangle(381, 376, 471, 291);

        public Bitmap fullcapture = null;

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

        public int GetUndetectedCount(List<int> indices)
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

        public int ConfirmDetected(List<DetectedInfo> detected, List<int> indices, int confirmations)
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

        public Tuple<List<int>, List<Tuple<ulong, List<Tuple<int, int>>>>> DetectCards(IEnumerable<HashData> cardhashlist)
        {
            // [cardhash, [cardindex, hashdistance]]
            List<Tuple<ulong, List<Tuple<int, int>>>> detected = new List<Tuple<ulong, List<Tuple<int, int>>>>();
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

                detected.Add(new Tuple<ulong, List<Tuple<int, int>>>(cardhash, cardindices));
            }

            return new Tuple<List<int>, List<Tuple<ulong, List<Tuple<int, int>>>>>(indices, detected);
        }

        public Tuple<List<int>, List<Tuple<ulong, List<Tuple<int, int>>>>> DetectHeroes(IEnumerable<HeroHashData> herohashlist)
        {
            // [herohash, [heroindex, hashdistance]]
            List<Tuple<ulong, List<Tuple<int, int>>>> detected = new List<Tuple<ulong, List<Tuple<int, int>>>>();
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

                detected.Add(new Tuple<ulong, List<Tuple<int, int>>>(herohash, heroindices));
            }

            return new Tuple<List<int>,List<Tuple<ulong,List<Tuple<int,int>>>>>(indices, detected);
        }

        public Tuple<List<int>, List<Tuple<ulong, List<Tuple<int, int>>>>> DetectBigHero(IEnumerable<HeroHashData> herohashlist)
        {
            // [bigherohash, [heroindex, hashdistance]]
            List<Tuple<ulong, List<Tuple<int, int>>>> detected = new List<Tuple<ulong, List<Tuple<int, int>>>>();
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

            detected.Add(new Tuple<ulong, List<Tuple<int, int>>>(bigherohash, bigheroindices));

            return new Tuple<List<int>, List<Tuple<ulong, List<Tuple<int, int>>>>>(indices, detected);
        }

        public ulong GetScreenCardHash(int index)
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

                    System.Windows.Controls.Image imagecontrol = Debug.debugimages[index];
                    hash = GetImageHash(capture, ref imagecontrol);
                }
                catch (Exception e)
                {
                    string errormsg = "Error2: " + e.Message + "\n" + e.ToString();
                    Debug.Log(errormsg);
                    Log.Info(errormsg);
                }
            }

            return hash;
        }

        public ulong GetScreenHeroHash(int index)
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
                    System.Windows.Controls.Image imagecontrol = Debug.debugimages[index];
                    hash = GetImageHash(capture, ref imagecontrol);
                }
                catch (Exception e)
                {
                    string errormsg = "Error3: " + e.Message + "\n" + e.ToString();
                    Debug.Log(errormsg);
                    Log.Info(errormsg);
                }
            }

            return hash;
        }

        public ulong GetScreenHash(Rectangle rect, int scalewidth, int scaleheight)
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
                    System.Windows.Controls.Image imagecontrol = Debug.debugimages[0];
                    hash = GetImageHash(capture, ref imagecontrol);
                }
                catch (Exception e)
                {
                    string errormsg = "Error4: " + e.Message + "\n" + e.ToString();
                    Debug.Log(errormsg);
                    Log.Info(errormsg);
                }
            }

            return hash;
        }

        // Perceptual hash using the techniques from: http://www.hackerfactor.com/blog/?/archives/432-Looks-Like-It.html
        public ulong GetImageHash(Bitmap bitmap, ref System.Windows.Controls.Image imagecontrol)
        {
            // Copy the image and convert to grayscale
            Bitmap sourcebm = new Bitmap(bitmap);
            Image<Gray, float> sourceimage = new Image<Gray, float>(sourcebm);

            // Apply a convolution filter of 4x4
            CvInvoke.Blur(sourceimage, sourceimage, new Size(4, 4), new Point(-1, -1));

            // Show image for debugging
            //Image<Bgra, Byte> convimage = Image<Bgra, Byte>.FromIplImagePtr(sourceimage);
            //ShowBitmap(convimage.ToBitmap(), ref imagecontrol);

            // Resize to 64x64 pixels
            Image<Gray, float> resimage = new Image<Gray, float>(new Size(64, 64));
            CvInvoke.Resize(sourceimage, resimage, new Size(64, 64));
            //ShowBitmap(resimage.ToBitmap(), ref imagecontrol);

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

        public int GetHashDistance(ulong hash1, ulong hash2)
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

        public List<Tuple<int, int>> FindHashIndex(ulong hash, IEnumerable<HashData> hashlist, int maxdistance)
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

        public List<Tuple<int, int>> FindAllHashIndex(ulong hash, IList<CardHashData> hashlist, int maxdistance)
        {
            List<Tuple<int, int>> indices = new List<Tuple<int, int>>();
            for (var i = 0; i < hashlist.Count; i++)
            {
                // Check all item hashes
                foreach (var itemhash in hashlist[i].hashes)
                {
                    int distance = GetHashDistance(hash, itemhash);
                    if (distance < maxdistance)
                    {
                        indices.Add(new Tuple<int, int>(i, distance));
                    }
                }
            }

            return indices;
        }

        public void CropBitmapRelative(ref Bitmap bm, Rectangle fullrect, Rectangle croprect)
        {
            double cropx = (double)(croprect.X - fullrect.X) / fullrect.Width;
            double cropy = (double)(croprect.Y - fullrect.Y) / fullrect.Height;
            double cropwidth = (double)croprect.Width / fullrect.Width;
            double cropheight = (double)croprect.Height / fullrect.Height;

            bm = bm.Clone(new Rectangle((int)(cropx * bm.Width), (int)(cropy * bm.Height), (int)(cropwidth * bm.Width), (int)(cropheight * bm.Height)), bm.PixelFormat);
        }

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


        // Doesn't work on some systems
        /*private void ShowBitmap(Bitmap bm, ref System.Windows.Controls.Image imagecontrol)
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
        }*/
    }
}
