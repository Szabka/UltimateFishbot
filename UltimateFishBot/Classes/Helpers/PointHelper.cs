using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UltimateFishBot.Classes.Helpers {
    public static class PointHelper {
        public static double CalcDistance(Win32.Point p1, Win32.Point p2) {
            return Math.Sqrt((p1.x - p2.x) ^ 2 + (p1.y - p2.y) ^ 2);
        }

        public static Boolean Empty(Win32.Point p) {
            return p.x == 0 && p.y == 0;
        }
    }
}
