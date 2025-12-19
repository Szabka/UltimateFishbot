using AForge.Imaging;
using AForge.Imaging.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UltimateFishBot.Classes.Helpers {
    public class RedifyFilter : BaseInPlacePartialFilter {

        // private format translation dictionary
        private Dictionary<PixelFormat, PixelFormat> formatTranslations = new Dictionary<PixelFormat, PixelFormat>();

        public RedifyFilter() {
            formatTranslations[PixelFormat.Format24bppRgb] = PixelFormat.Format24bppRgb;
            formatTranslations[PixelFormat.Format32bppRgb] = PixelFormat.Format32bppRgb;
            formatTranslations[PixelFormat.Format32bppArgb] = PixelFormat.Format32bppArgb;
        }

        /// <summary>
        /// Format translations dictionary.
        /// </summary>
        public override Dictionary<PixelFormat, PixelFormat> FormatTranslations {
            get { return formatTranslations; }
        }
        /// <summary>
        /// Process the filter on the specified image.
        /// </summary>
        /// 
        /// <param name="image">Source image data.</param>
        /// <param name="rect">Image rectangle for processing by the filter.</param>
        ///
        protected override unsafe void ProcessFilter(UnmanagedImage image, Rectangle rect) {
            // get pixel size
            int pixelSize = (image.PixelFormat == PixelFormat.Format24bppRgb) ? 3 : 4;

            int startX = rect.Left;
            int startY = rect.Top;
            int stopX = startX + rect.Width;
            int stopY = startY + rect.Height;
            int offset = image.Stride - rect.Width * pixelSize;

            // do the job
            byte* ptr = (byte*)image.ImageData.ToPointer();
            byte r, g, b, cm;

            // allign pointer to the first pixel to process
            ptr += (startY * image.Stride + startX * pixelSize);

            // for each row
            for (int y = startY; y < stopY; y++) {
                // for each pixel
                for (int x = startX; x < stopX; x++, ptr += pixelSize) {
                    r = ptr[RGB.R];
                    g = ptr[RGB.G];
                    b = ptr[RGB.B];
                    cm = Math.Min(r, Math.Min(g, b));
                    r -= cm;
                    g -= cm;
                    b -= cm;
                    // check pixel
                    if (
                        r<g||r<b||r<20
                        ) {
                        ptr[RGB.R] = 0;
                        ptr[RGB.G] = 0;
                        ptr[RGB.B] = 0;
                    } else {
                        cm = Math.Max(g, b);
                        if (r - cm < 20) {
                            r = 0;
                        }
                        ptr[RGB.R] = r;
                        ptr[RGB.G] = 0;
                        ptr[RGB.B] = 0;
                    }
                }
                ptr += offset;
            }
        }
    }
}
