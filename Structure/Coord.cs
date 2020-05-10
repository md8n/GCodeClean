using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

[Flags]
public enum CoordSet
{
    None = 0b_0000_0000,  // 0
    X    = 0b_0000_0001,  // 1
    Y    = 0b_0000_0010,  // 2
    Z    = 0b_0000_0100,  // 4
    All  = X | Y | Z
}

public class Coord {
    public decimal X { get; set; }

    public decimal Y { get; set; }

    public decimal Z { get; set; }

    public CoordSet Set { get; set; }

    public Coord()
    {
        this.X = 0M;
        this.Y = 0M;
        this.Z = 0M;
        this.Set = CoordSet.None;
    }

    public Coord(decimal A, decimal B, CoordSet addCoord = CoordSet.Z) {
        if (addCoord == CoordSet.X) 
        {
            this.X = 0M;
            this.Y = A;
            this.Z = B;
            this.Set = CoordSet.Y | CoordSet.Z;
        }
        else if (addCoord == CoordSet.Y)
        {
            this.X = A;
            this.Y = 0M;
            this.Z = B;
            this.Set = CoordSet.X | CoordSet.Z;
        }
        else
        {
            this.X = A;
            this.Y = B;
            this.Z = 0M;
            this.Set = CoordSet.X | CoordSet.Y;
        }
    }

    public Coord(decimal X, decimal Y, decimal Z) {
        this.X = X;
        this.Y = Y;
        this.Z = Z;
        this.Set = CoordSet.All;
    }

    public Coord(float A, float B, CoordSet addCoord = CoordSet.Z): this((decimal)A, (decimal)B, addCoord) { }

    public Coord(float X, float Y, float Z) : this((decimal)X, (decimal)Y, (decimal)Z) { }

    public Coord(PointF ab, CoordSet addCoord = CoordSet.Z): this((decimal)ab.X, (decimal)ab.Y, addCoord) { }

    public (Double X, Double Y, Double Z) ToDouble()
    {
        return ((Double)(this.X), (Double)(this.Y), (Double)(this.Z));
    }

    public PointF ToPointF(CoordSet dropCoord = CoordSet.Z)
    {
        if (dropCoord == CoordSet.X) 
        {
            return new PointF ((float)(this.Y), (float)(this.Z));
        }
        else if (dropCoord == CoordSet.Y)
        {
            return new PointF ((float)(this.X), (float)(this.Z));
        }
        return new PointF ((float)(this.X), (float)(this.Y));
    }

    public static Coord Difference (Coord coords1, Coord coords2)
    {
        var coords3 = coords1 - coords2;
        return new Coord(Math.Abs(coords3.X), Math.Abs(coords3.Y), Math.Abs(coords3.Z));
    }

    public static Coord operator -(Coord coords1, Coord coords2)
    {
        return new Coord((coords2.X - coords1.X), (coords2.Y - coords1.Y), (coords2.Z - coords1.Z));
    }

    public override string ToString()
    {
        var coords = new List<string>();

        if ((this.Set & CoordSet.X) == CoordSet.X)
        {
            coords.Add($"X:{this.X:0.####}");
        }
        if ((this.Set & CoordSet.Y) == CoordSet.Y)
        {
            coords.Add($"Y:{this.Y:0.####}");
        }
        if ((this.Set & CoordSet.Z) == CoordSet.Z)
        {
            coords.Add($"Z:{this.Z:0.####}");
        }

        return string.Join(',', coords);
    }

    /// <summary>
    /// Determines if all supplied coords are in the same orthogonal plane (X, Y or Z)
    /// </summary>
    /// <return>
    /// boolean - false if no coords are supplied, true for 1 or 2 coords, true or false for 3 or more coords
    /// </return>
    public static bool Coplanar(List<Coord> coords)
    {
        if (coords.Count < 3) {
            return coords.Count > 0;
        }

        var allXSame = coords.Select(c => c.X).Distinct().Count() == 1;
        var allYSame = coords.Select(c => c.Y).Distinct().Count() == 1;
        var allZSame = coords.Select(c => c.Z).Distinct().Count() == 1;

        return allXSame || allYSame || allZSame;
    }
    
    /// <summary>
    /// Determines which orthogonal plane (X, Y or Z) if any, that is shared by the coords
    /// </summary>
    /// <return>
    /// string - "" if no coords are supplied, "XYZ" for 1 coords, relevant value for 2 or more coords
    /// </return>
    public static CoordSet Ortho(List<Coord> coords)
    {
        if (coords.Count < 2) {
            return coords.Count == 0 ? CoordSet.None : CoordSet.All;
        }

        var allX = coords.Select(c => c.X).Distinct().Count() == 1 ? CoordSet.X : CoordSet.None;
        var allY = coords.Select(c => c.Y).Distinct().Count() == 1 ? CoordSet.Y : CoordSet.None;
        var allZ = coords.Select(c => c.Z).Distinct().Count() == 1 ? CoordSet.Z : CoordSet.None;

        return allX | allY | allZ;
    }
}