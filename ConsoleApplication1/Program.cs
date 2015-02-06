using ConsoleApplication1.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {

                WhiteToAlpha(@"C:\Users\Boris\Pictures\podpis2.png", @"C:\Users\Boris\Pictures\podpis2_.png");

                //BumpToNormalMap(@"C:\Users\Boris\Desktop\bump.png", @"C:\Users\Boris\Desktop\normal.png");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            Console.ReadLine();
        }

        private static void WhiteToAlpha(string fileInMap, string fileOutMap)
        {
            using (var input = new Bitmap(fileInMap))
            {
                int w = input.Width;
                int h = input.Height;
                using (var output = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    double globalDarkest = GetDarkest(w / 2, h / 2, w, h, Math.Max(w / 2, h / 2) + 1, input);
                    double darkenConst = (1 - 32.0 / 255) / (1 - globalDarkest);
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            var inColor = input.GetPixel(x, y);
                            double inDarkness = inColor.GetBrightness();
                            double darkenFactor = 1 - (1 - inDarkness) * darkenConst;
                            double alpha = 1;
                            if (inColor.A != 0)
                            {
                                double darkest = GetDarkest(x, y, w, h, 6, input);
                                if (darkest < 1)
                                {
                                    alpha = 1 - Math.Pow((inDarkness - darkest) / (1 - darkest), 2);
                                }
                            }
                            output.SetPixel(x, y, Color.FromArgb((byte)(inColor.A * alpha),
                                                                    (byte)(inColor.R * darkenFactor * alpha),
                                                                    (byte)(inColor.G * darkenFactor * alpha),
                                                                    (byte)(inColor.B * darkenFactor * alpha)));
                        }
                        Console.Write(string.Format("Line {0} from {1}\r", y + 1, h));
                    }
                    Console.WriteLine();
                    output.Save(fileOutMap);
                }
            }
        }

        private static float GetDarkest(int x, int y, int w, int h, int d, Bitmap input)
        {
            float darkness = 1;
            for (int dy = y - d; dy <= y + d; dy++)
            {
                if (dy < 0 || dy >= h)
                    continue;

                for (int dx = x - d; dx <= x + d; dx++)
                {
                    if (dx < 0 || dx >= w)
                        continue;

                    var inColor = input.GetPixel(dx, dy);
                    if (inColor.A == 0)
                        continue;

                    float inDarkness = inColor.GetBrightness();
                    if (inDarkness < darkness)
                    {
                        darkness = inDarkness;
                    }
                }
            }
            return darkness;
        }

        private static void BumpToNormalMap(string fileBumpMap, string fileNormalMap)
        {
            using (var input = new Bitmap(fileBumpMap))
            {
                int w = input.Width;
                int h = input.Height;
                using (var output = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            output.SetPixel(x, y, CalcNormalFromBump(x, y, w, h, input));
                        }
                        Console.Write(string.Format("Line {0} from {1}\r", y + 1, h));
                    }
                    Console.WriteLine();
                    output.Save(fileNormalMap);
                }
            }
        }

        private static Color CalcNormalFromBump(int x, int y, int w, int h, Bitmap input)
        {
            float dx;
            if (x == 0)
            {
                dx = CalcDiff2Pts(GetFunc(1, y, input), GetFunc(0, y, input));
            }
            else if (x == w - 1)
            {
                dx = CalcDiff2Pts(GetFunc(w - 1, y, input), GetFunc(w - 2, y, input));
            }
            else
            {
                dx = CalcDiff3Pts(GetFunc(x + 1, y, input), GetFunc(x, y, input), GetFunc(x - 1, y, input));
            }
            float dy;
            if (y == 0)
            {
                dy = CalcDiff2Pts(GetFunc(x, 1, input), GetFunc(x, 0, input));
            }
            else if (y == h - 1)
            {
                dy = CalcDiff2Pts(GetFunc(x, h - 1, input), GetFunc(x, h - 2, input));
            }
            else
            {
                dy = CalcDiff3Pts(GetFunc(x, y + 1, input), GetFunc(x, y, input), GetFunc(x, y - 1, input));
            }

            return Color.FromArgb((int)((dx + 0.5) * 255), (int)((dy + 0.5) * 255), 255);
        }

        private static float GetFunc(int x, int y, Bitmap input)
        {
            var color = input.GetPixel(x, y);
            return color.R / 255f;
        }

        private static float CalcDiff3Pts(float f1, float f2, float f3)
        {
            return (f1 + f3 - 2 * f2) / 4;
        }

        private static float CalcDiff2Pts(float f1, float f2)
        {
            return (f1 - f2) / 2;
        }

    }
}
