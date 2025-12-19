using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using Serilog;
using UltimateFishBot.Classes.Helpers;

namespace UltimateFishBot.Classes.BodyParts {

    class Eyes {
        private Win32.CursorInfo m_noFishCursor;
        private IntPtr Wow;
        private Bitmap capturedCursorIcon;
        private Dictionary<Win32.Point, int> bobberPosDict;
        private Bitmap background;
        private Bitmap castbmp;
        private Rectangle wowRectangle;

        public Eyes(IntPtr wowWindow) {
            SetWow(wowWindow);
            bobberPosDict = new Dictionary<Win32.Point, int>();
        }

        public void SetWow(IntPtr wowWindow) {
            this.Wow = wowWindow;
            m_noFishCursor = Win32.GetNoFishCursor(this.Wow);
            wowRectangle = Win32.GetWowRectangle(this.Wow);
            if (System.IO.File.Exists("capturedcursor.png")) {
                capturedCursorIcon = new Bitmap("capturedcursor.png", true);
            }

        }

        public async Task<Win32.Point> LookForBobber(CancellationToken cancellationToken) {
            String imagePrefix = "sc_" + DateTime.UtcNow.Ticks;
            Win32.Rect scanArea;
            if (!Properties.Settings.Default.customScanArea) {
                scanArea.Left = wowRectangle.X + wowRectangle.Width / 5;
                scanArea.Right = wowRectangle.X + wowRectangle.Width / 5 * 4;
                scanArea.Top = wowRectangle.Y + wowRectangle.Height / 4;
                scanArea.Bottom = wowRectangle.Y + wowRectangle.Height / 4 * 3;
                //Log.Information("Using default area");
            } else {
                scanArea.Left = Properties.Settings.Default.minScanXY.X;
                scanArea.Top = Properties.Settings.Default.minScanXY.Y;
                scanArea.Right = Properties.Settings.Default.maxScanXY.X;
                scanArea.Bottom = Properties.Settings.Default.maxScanXY.Y;
                //Log.Information("Using custom area");
            }
            Log.Information("Scanning area: " + scanArea.Left.ToString() + " , " + scanArea.Top.ToString() + " , " + scanArea.Right.ToString() + " , " + scanArea.Bottom.ToString() + " cs: " + bobberPosDict.Keys.Count.ToString());
            Win32.Point bobberPos = new Win32.Point { x = 0, y = 0 };

            foreach (Win32.Point dp in PointOfScreenDifferences(imagePrefix)) {
                if (await MoveMouseAndCheckCursor(dp.x, dp.y, cancellationToken, 12)) {
                    bobberPos = dp;
                    Log.Information("Bobber imagescan hit. ({bx},{by})", bobberPos.x, bobberPos.y);
                    //try to find near pos in the dict that change the cursor
                    foreach (KeyValuePair<Win32.Point, int> pos in System.Linq.Enumerable.OrderBy(bobberPosDict, (key => key.Value))) {
                        double cd = PointHelper.CalcDistance(dp, pos.Key);
                        if (cd < 10.0) {
                            if (await MoveMouseAndCheckCursor(pos.Key.x, pos.Key.y, cancellationToken, 12)) {
                                bobberPos = pos.Key;
                                Log.Information("Bobber position near cache hit. ({bx},{by}) {size} {hitcount} {cd}", bobberPos.x, bobberPos.y, bobberPosDict.Count, pos.Value, cd);
                                break;
                            }
                        }
                    }
                    break;
                }
            }

            if (PointHelper.Empty(bobberPos)) {
                // utilize previous hits
                foreach (KeyValuePair<Win32.Point, int> pos in System.Linq.Enumerable.OrderBy(bobberPosDict, (key => key.Value))) {
                    // do something with item.Key and item.Value
                    if (await MoveMouseAndCheckCursor(pos.Key.x, pos.Key.y, cancellationToken, 12)) {
                        bobberPos = pos.Key;
                        Log.Information("Bobber position cache hit. ({bx},{by}) {size} {hitcount}", bobberPos.x, bobberPos.y, bobberPosDict.Count, pos.Value);
                        break;
                    }
                }
            }
            if (PointHelper.Empty(bobberPos)) {
                if (Properties.Settings.Default.AlternativeRoute) {
                    bobberPos = await LookForBobberSpiralImpl(scanArea, bobberPos, Properties.Settings.Default.ScanningSteps, Properties.Settings.Default.ScanningRetries, cancellationToken);
                } else {
                    bobberPos = await LookForBobberImpl(scanArea, bobberPos, Properties.Settings.Default.ScanningSteps, Properties.Settings.Default.ScanningRetries, cancellationToken);
                }
            }
            if (!PointHelper.Empty(bobberPos)) {
                int hitcount = 1;
                if (bobberPosDict.ContainsKey(bobberPos)) {
                    bobberPosDict.TryGetValue(bobberPos, out hitcount);
                    hitcount++;
                    bobberPosDict.Remove(bobberPos);
                }
                bobberPosDict.Add(bobberPos, hitcount);
                using (StreamWriter outputFile = new StreamWriter(imagePrefix + ".txt")) {
                    outputFile.WriteLine(bobberPos.x + ";" + bobberPos.y);
                }
            }

            Log.Information("Bobber scan finished. ({bx},{by})", bobberPos.x, bobberPos.y);
            return bobberPos;

        }

        // reference capture
        public void updateBackground() {
            Bitmap cw = Win32.CaptureWindow(Wow);
            background = cw.Clone(new Rectangle(0, 0, cw.Width, cw.Height), PixelFormat.Format24bppRgb);
            try {
                new RedifyFilter().ApplyInPlace(background);
                //                new ColorFiltering(new IntRange(20, 155), new IntRange(0, 40), new IntRange(0, 20)).ApplyInPlace(background);
                //                new Pixellate().ApplyInPlace(background);
            } catch (UnsupportedImageFormatException e) {
                Log.Information("bg format: " + background.PixelFormat.ToString() + e);
            }
        }


        private List<Win32.Point> PointOfScreenDifferences(String imagePrefix) {
            Bitmap cw = Win32.CaptureWindow2(Wow);
            castbmp = cw.Clone(new Rectangle(0, 0, cw.Width, cw.Height), PixelFormat.Format24bppRgb);

            background.Save(imagePrefix + "_bg.png", ImageFormat.Png);
            castbmp.Save(imagePrefix + "_cs.png", ImageFormat.Png);
            var blobCounter = new BlobCounter();
            try {
                FiltersSequence processingFilter = new FiltersSequence();
                processingFilter.Add(new RedifyFilter());
                //                processingFilter.Add(new ColorFiltering(new IntRange(20, 155), new IntRange(0, 40), new IntRange(0, 20)));
                //                processingFilter.Add(new Pixellate());
                processingFilter.Add(new Difference(background));
                //                processingFilter.Add(new Threshold(15));
                processingFilter.Add(new Erosion());

                Bitmap filteredbmp = processingFilter.Apply(castbmp);
                filteredbmp.Save(imagePrefix + "_fb.png", ImageFormat.Png);
                blobCounter.ProcessImage(filteredbmp);
            } catch (UnsupportedImageFormatException e) {
                Log.Information("castbmp format: " + castbmp.PixelFormat.ToString() + " " + e);
            }


            Rectangle[] brl = blobCounter.GetObjectsRectangles();
            Log.Information("Bobber imagescan brl: {brl}", brl.Length);
            List<Win32.Point> sdl = new List<Win32.Point>();
            foreach (Rectangle br in brl) {
                Win32.Point pt = new Win32.Point { x = (8 * br.Left + 4 * br.Right) / 12, y = (4 * br.Top + 8 * br.Bottom) / 12 };
                Win32.ClientToScreen(Wow, ref pt);
                if ((br.Right - br.Left) > 9 && (br.Bottom - br.Top) > 4) {
                    //                    Win32.Point pt = new Win32.Point { x= wowRectangle.X+(br.Left + br.Right) / 2, y= wowRectangle.Y+(br.Top+br.Bottom)/2 };
                    Log.Information("Bobber imagescan br: {bx},{by} - {w},{h}", pt.x, pt.y, (br.Right - br.Left), (br.Bottom - br.Top));
                    if (sdl.Count < 10) {
                        sdl.Add(pt);
                    } else {
                        Log.Information("Bobber imagescan overflow br: {bx},{by} - {w},{h}", pt.x, pt.y, (br.Right - br.Left), (br.Bottom - br.Top));
                    }
                    //                } else {
                }
            }
            // debug
            /*
            */
            Bitmap bmpDst = new Bitmap(castbmp);
            using (var g = Graphics.FromImage(bmpDst)) {
                foreach (var br in brl) {
                    if ((br.Right - br.Left) > 9 && (br.Bottom - br.Top) > 4) {
                        g.DrawRectangle(Pens.White, br);
                    }
                }
            }
            bmpDst.Save(imagePrefix + "_bd.png", ImageFormat.Png);

            return sdl;
        }

        public async Task<bool> SetMouseToBobber(Win32.Point bobberPos, CancellationToken cancellationToken) {// move mouse to previous recorded position and check shape
            if (!await MoveMouseAndCheckCursor(bobberPos.x, bobberPos.y, cancellationToken, 12)) {
                Log.Information("Bobber lost. ({bx},{by})", bobberPos.x, bobberPos.y);
                int fixr = 24;
                Win32.Rect scanArea;
                scanArea.Left = bobberPos.x - fixr;
                scanArea.Right = bobberPos.x + fixr;
                scanArea.Top = bobberPos.y - fixr;
                scanArea.Bottom = bobberPos.y + fixr;
                // initiate a small-area search for bobber
                Win32.Point npos;
                npos.x = 0;
                npos.y = 0;
                npos = await LookForBobberSpiralImpl(scanArea, npos, 4, 1, cancellationToken);
                if (npos.x != 0 && npos.y != 0) {
                    // search was successful
                    Log.Information("Bobber found. ({bx},{by})", npos.x, npos.y);
                    return true;
                } else {
                    Log.Information("Bobber flost. ({bx},{by})", npos.x, npos.y);
                    return false;
                }
            }
            return true;
        }


        private async Task<Win32.Point> LookForBobberImpl(Win32.Rect scanArea, Win32.Point bobberPos, int steps, int retries, CancellationToken cancellationToken) {

            int XPOSSTEP = (int)((scanArea.Right - scanArea.Left) / steps);
            int YPOSSTEP = (int)((scanArea.Bottom - scanArea.Top) / steps);
            int XOFFSET = (int)(XPOSSTEP / retries);

            for (int tryCount = 0; tryCount < retries; ++tryCount) {
                for (int x = (int)(scanArea.Left + (XOFFSET * tryCount)); x < scanArea.Right; x += XPOSSTEP) {
                    for (int y = scanArea.Top; y < scanArea.Bottom; y += YPOSSTEP) {
                        if (await MoveMouseAndCheckCursor(x, y, cancellationToken, 10)) {
                            bobberPos.x = x;
                            bobberPos.y = y;
                            return bobberPos;
                        }
                    }
                }
            }
            return bobberPos;
        }

        private async Task<Win32.Point> LookForBobberSpiralImpl(Win32.Rect scanArea, Win32.Point bobberPos, int steps, int retries, CancellationToken cancellationToken) {

            int XPOSSTEP = (int)((scanArea.Right - scanArea.Left) / steps);
            int YPOSSTEP = (int)((scanArea.Bottom - scanArea.Top) / steps);
            int XOFFSET = (int)(XPOSSTEP / retries);
            int YOFFSET = (int)(YPOSSTEP / retries);

            for (int tryCount = 0; tryCount < retries; ++tryCount) {
                int x = (int)((scanArea.Left + scanArea.Right) / 2) + XOFFSET * tryCount;
                int y = (int)((scanArea.Top + scanArea.Bottom) / 2) + YOFFSET * tryCount;

                for (int i = 0; i <= 2 * steps; i++) {
                    for (int j = 0; j <= (i / 2); j++) {
                        int dx = 0, dy = 0;
                        if (i % 2 == 0) {
                            if ((i / 2) % 2 == 0) {
                                dx = XPOSSTEP;
                                dy = 0;
                            } else {
                                dx = -XPOSSTEP;
                                dy = 0;
                            }
                        } else {
                            if ((i / 2) % 2 == 0) {
                                dx = 0;
                                dy = YPOSSTEP;
                            } else {
                                dx = 0;
                                dy = -YPOSSTEP;
                            }
                        }
                        x += dx;
                        y += dy;
                        if (await MoveMouseAndCheckCursor(x, y, cancellationToken, 10)) {
                            bobberPos.x = x;
                            bobberPos.y = y;
                            return bobberPos;
                        }
                    }
                }
            }
            return bobberPos;
        }

        private async Task<bool> MoveMouseAndCheckCursor(int x, int y, CancellationToken cancellationToken, int mpy) {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();

            Win32.MoveRelMouse(x, y);

            // Pause (give the OS a chance to change the cursor)
            await Task.Delay(mpy * Properties.Settings.Default.ScanningDelay / 10, cancellationToken);

            Win32.CursorInfo actualCursor = Win32.GetCurrentCursor();

            if (actualCursor.flags == m_noFishCursor.flags &&
                actualCursor.hCursor == m_noFishCursor.hCursor)
                return false;

            // Compare the actual icon with our fishIcon if user want it
            if (Properties.Settings.Default.CheckCursor) {
                if (ImageCompare(Win32.GetCursorIcon(actualCursor), Properties.Resources.fishIcon35x35)) {
                    // We found a fish!
                    return true;
                }
                if (capturedCursorIcon != null && ImageCompare(Win32.GetCursorIcon(actualCursor), capturedCursorIcon)) {
                    // We found a fish!
                    return true;
                }
                return false;
            }

            return true;
        }


        private static bool ImageCompare(Bitmap bmp1, Bitmap bmp2) {

            if (bmp1 == null || bmp2 == null) {
                return false;
            }
            if (object.Equals(bmp1, bmp2)) {
                return true;
            }
            if (!bmp1.Size.Equals(bmp2.Size) || !bmp1.PixelFormat.Equals(bmp2.PixelFormat)) {
                return false;
            }

            int bytes = bmp1.Width * bmp1.Height * (System.Drawing.Image.GetPixelFormatSize(bmp1.PixelFormat) / 8);

            bool result = true;
            byte[] b1bytes = new byte[bytes];
            byte[] b2bytes = new byte[bytes];

            BitmapData bitmapData1 = bmp1.LockBits(new Rectangle(0, 0, bmp1.Width - 1, bmp1.Height - 1), ImageLockMode.ReadOnly, bmp1.PixelFormat);
            BitmapData bitmapData2 = bmp2.LockBits(new Rectangle(0, 0, bmp2.Width - 1, bmp2.Height - 1), ImageLockMode.ReadOnly, bmp2.PixelFormat);

            Marshal.Copy(bitmapData1.Scan0, b1bytes, 0, bytes);
            Marshal.Copy(bitmapData2.Scan0, b2bytes, 0, bytes);

            for (int n = 0; n <= bytes - 1; n++) {
                if (b1bytes[n] != b2bytes[n]) {
                    result = false;
                    break;
                }
            }

            bmp1.UnlockBits(bitmapData1);
            bmp2.UnlockBits(bitmapData2);

            return result;
        }

        public void CaptureCursor() {
            Win32.CursorInfo actualCursor = Win32.GetCurrentCursor();
            Bitmap cursorIcon = Win32.GetCursorIcon(actualCursor);
            if (capturedCursorIcon != null) {
                capturedCursorIcon.Dispose();
                capturedCursorIcon = null;
            }
            if (System.IO.File.Exists("capturedcursor.png")) {
                System.IO.File.Delete("capturedcursor.png");
            }
            cursorIcon.Save("capturedcursor.png", ImageFormat.Png);
            capturedCursorIcon = cursorIcon;
        }

    }
}