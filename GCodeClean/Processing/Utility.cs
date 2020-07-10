// Copyright (c) 2020 - Lee HUMPHRIES (lee@md8n.com) and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for details.

using System;
using System.Collections.Generic;
using System.Drawing;

using GCodeClean.Structure;

namespace GCodeClean.Processing
{
    public static class Utility
    {
        /// <summary>
        /// Is B between A and C, inclusive
        /// </summary>
        public static bool WithinRange(this decimal B, decimal A, decimal C)
        {
            return A >= B && B >= C || A <= B && B <= C;
        }

        public static double Angle(this double da, double db)
        {
            var theta = Math.Atan2(da, db); // range (-PI, PI]
            theta *= 180 / Math.PI; // rads to degs, range (-180, 180]

            return theta;
        }

        public static decimal Sqr(this decimal value)
        {
            return value * value;
        }

        public static decimal Distance(this (Coord A, Coord B) c)
        {
            return (decimal)Math.Sqrt((double)((c.B.X - c.A.X).Sqr() + (c.B.Y - c.A.Y).Sqr() + (c.B.Z - c.A.Z).Sqr()));
        }

        public static decimal Angle(this (decimal A, decimal B) d)
        {
            var theta = Math.Atan2((double)d.A, (double)d.B); // range (-PI, PI]
            theta *= 180 / Math.PI; // rads to degs, range (-180, 180]

            return (decimal)theta;
        }

        /// <summary>
        /// Get the number of decimal places in a decimal, igoring any 'significant' zeros at the end
        /// </summary>
        public static int GetDecimalPlaces(this decimal n)
        {
            n = Math.Abs(n); //make sure it is positive.
            n -= (int)n;     //remove the integer part of the number.
            var decimalPlaces = 0;
            while (n > 0)
            {
                decimalPlaces++;
                n *= 10;
                n -= (int)n;
            }

            return decimalPlaces;
        }

        public static decimal ConstrainTolerance(this decimal tolerance, string lengthUnits = "mm") {
            // Retweak tolerance to allow for lengthUnits
            if (lengthUnits == "mm") {
                if (tolerance < 0.005M) {
                    tolerance = 0.005M;
                } else if (tolerance > 0.5M) {
                    tolerance = 0.5M;
                }
            } else {
                if (tolerance < 0.0005M) {
                    tolerance = 0.0005M;
                } else if (tolerance > 0.05M) {
                    tolerance = 0.05M;
                }
            }

            return tolerance;
        }

        /// <summary>
        /// Function to find the circle on which the given three points lie
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static (Coord center, decimal radius, bool isClockwise) FindCircle(Coord a, Coord b, Coord c, Context context)
        {
            var center = new Coord();
            var radius = 0M;
            var isClockwise = false;

            var coordPlane = context.GetModalState(ModalGroup.ModalPlane).ToString();
            var ortho = coordPlane switch
            {
                "G17" => CoordSet.Z,
                "G18" => CoordSet.Y,
                "G19" => CoordSet.X,
                _ => CoordSet.None,
            };
            if (ortho == CoordSet.None)
            {
                ortho = Coord.Ortho(new List<Coord> { a, b, c });
            }
            if (ortho == CoordSet.None)
            {
                return (center, radius, isClockwise);
            }

            // Determine which coordinate we're dropping
            var dropCoord = CoordSet.Z;
            if ((ortho & CoordSet.X) == CoordSet.X)
            {
                dropCoord = CoordSet.X;
            }
            else if ((ortho & CoordSet.Y) == CoordSet.Y)
            {
                dropCoord = CoordSet.Y;
            }
            var droppedCoordOK = dropCoord switch
            {
                CoordSet.Z => a.Z == b.Z && b.Z == c.Z,
                CoordSet.Y => a.Y == b.Y && b.Y == c.Y,
                CoordSet.X => a.X == b.X && b.X == c.X,
                _ => false,
            };
            if (!droppedCoordOK)
            {
                return (center, radius, isClockwise);
            }

            // Convert to points in 2 dimensions
            var pA = a.ToPointF(dropCoord);
            var pB = b.ToPointF(dropCoord);
            var pC = c.ToPointF(dropCoord);

            var xAB = pA.X - pB.X;
            var xAC = pA.X - pC.X;

            var yAB = pA.Y - pB.Y;
            var yAC = pA.Y - pC.Y;

            var yCA = pC.Y - pA.Y;
            var yBA = pB.Y - pA.Y;

            var xCA = pC.X - pA.X;
            var xBA = pB.X - pA.X;

            // pA.X^2 - pC.X^2 
            var sxAC = Math.Pow(pA.X, 2) - Math.Pow(pC.X, 2);

            // pA.Y^2 - pC.Y^2 
            var syAC = Math.Pow(pA.Y, 2) - Math.Pow(pC.Y, 2);

            var sxBA = Math.Pow(pB.X, 2) - Math.Pow(pA.X, 2);

            var syBA = Math.Pow(pB.Y, 2) - Math.Pow(pA.Y, 2);

            var f = (sxAC * xAB
                    + syAC * xAB
                    + sxBA * xAC
                    + syBA * xAC)
                    / (2 * (yCA * xAB - yBA * xAC));
            var g = (sxAC * yAB
                    + syAC * yAB
                    + sxBA * yAC
                    + syBA * yAC)
                    / (2 * (xCA * yAB - xBA * yAC));

            if (double.IsInfinity(f) || double.IsInfinity(g))
            {
                // lines are parallel / co-linear
                return (center, radius, isClockwise);
            }

            var circ = -Math.Pow(pA.X, 2) - Math.Pow(pA.Y, 2) -
                                        2 * g * pA.X - 2 * f * pA.Y;

            // eqn of circle be x^2 + y^2 + 2*g*x + 2*f*y + c = 0 
            // where centre is (h = -g, k = -f) and radius r 
            // as r^2 = h^2 + k^2 - c 
            var h = -g;
            var k = -f;
            var sqrOfR = h * h + k * k - circ;

            radius = (decimal)Math.Round(Math.Sqrt(sqrOfR), 5);
            center = coordPlane switch
            {
                "G17" => new Coord((decimal)h, (decimal)k, b.Z),
                "G18" => new Coord((decimal)h, b.Y, (decimal)k),
                "G19" => new Coord(b.X, (decimal)h, (decimal)k),
                _ => center,
            };

            isClockwise = DirectionOfPoint(pA, pB, center.ToPointF()) < 0;

            return (center, radius, isClockwise);
        }

        public static int DirectionOfPoint(PointF pA, PointF pB, PointF pC)
        {
            // subtracting co-ordinates of point A  
            // from B and P, to make A as origin
            pB.X -= pA.X;
            pB.Y -= pA.Y;
            pC.X -= pA.X;
            pC.Y -= pA.Y;

            // Determining cross product 
            var crossProduct = pB.X * pC.Y - pB.Y * pC.X;

            // return the sign of the cross product 
            if (crossProduct > 0)
            {
                return 1;
            }
            if (crossProduct < 0)
            {
                return -1;
            }
            return 0;
        }

        public static List<Coord> FindIntersections(Coord cA, Coord cB, decimal radius, Context context)
        {
            var intersections = new List<Coord>();

            // We only calculate a circle through one orthogonal plane,
            // therefore at least one of the dimensions must be the same for both coords
            var ortho = context.GetModalState(ModalGroup.ModalPlane).ToString() switch
            {
                "G17" => CoordSet.Z,
                "G18" => CoordSet.Y,
                "G19" => CoordSet.X,
                _ => CoordSet.None,
            };
            if (ortho == CoordSet.None)
            {
                ortho = Coord.Ortho(new List<Coord> { cA, cB });
            }
            if (ortho == CoordSet.None)
            {
                return intersections;
            }

            // Determine which coordinate we're dropping
            var dropCoord = CoordSet.Z;
            if ((ortho & CoordSet.X) == CoordSet.X)
            {
                dropCoord = CoordSet.X;
            }
            else if ((ortho & CoordSet.Y) == CoordSet.Y)
            {
                dropCoord = CoordSet.Y;
            }
            var droppedCoordOK = dropCoord switch
            {
                CoordSet.Z => cA.Z == cB.Z,
                CoordSet.Y => cA.Y == cB.Y,
                CoordSet.X => cA.X == cB.X,
                _ => false,
            };
            if (!droppedCoordOK)
            {
                return intersections;
            }

            // Convert to points in 2 dimensions
            var pA = cA.ToPointF(dropCoord);
            var pB = cB.ToPointF(dropCoord);

            // Find the distance between the centers.
            var dx = pA.X - pB.X;
            var dy = pA.Y - pB.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            // See how many solutions there are.
            if (dist > (double)(radius * 2) || dist == 0)
            {
                // No solutions, the circles are too far apart or coincide, must be malformed
                return intersections;
            }

            // Find a and h.
            var a = dist * dist / (2 * dist);
            var h = Math.Sqrt((double)radius.Sqr() - a * a);

            // Find pC.
            var pC = new PointF((float)(pA.X + a * (pB.X - pA.X) / dist), (float)(pA.Y + a * (pB.Y - pA.Y) / dist));

            // Get the points P3.
            intersections.Add(new Coord(new PointF(
                (float)(pC.X + h * (pB.Y - pA.Y) / dist),
                (float)(pC.Y - h * (pB.X - pA.X) / dist)), dropCoord));

            // Do we have 1 or 2 solutions.
            if (dist < (double)(radius * 2))
            {
                intersections.Add(new Coord(new PointF(
                (float)(pC.X - h * (pB.Y - pA.Y) / dist),
                (float)(pC.Y + h * (pB.X - pA.X) / dist)), dropCoord));
            }

            return intersections;
        }
    }
}
