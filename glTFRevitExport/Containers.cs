using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace glTFRevitExport
{
    /// <summary>
    /// Intermediate data format for 
    /// converting between Revit Polymesh
    /// and glTF buffers.
    /// </summary>
    public class GeometryData
    {
        public VertexLookupInt vertDictionary = new VertexLookupInt();
        public List<long> vertices = new List<long>();
        public List<double> normals = new List<double>();
        public List<double> uvs = new List<double>();
        public List<int> faces = new List<int>();
    }

    /// <summary>
    /// Container for holding a strict set of items
    /// that is also addressable by a unique ID.
    /// </summary>
    /// <typeparam name="T">The type of item contained.</typeparam>
    public class IndexedDictionary<T>
    {
        private Dictionary<string, int> _dict = new Dictionary<string, int>();
        public List<T> List { get; } = new List<T>();
        public string CurrentKey { get; private set; }
        public Dictionary<string,T> Dict
        {
            get
            {
                var output = new Dictionary<string, T>();
                foreach (var kvp in _dict)
                {
                    output.Add(kvp.Key, List[kvp.Value]);
                }
                return output;
            }
        }

        /// <summary>
        /// The most recently accessed item (not effected by GetElement()).
        /// </summary>
        public T CurrentItem
        {
            get { return List[_dict[CurrentKey]]; }
        }

        /// <summary>
        /// The index of the most recently accessed item (not effected by GetElement()).
        /// </summary>
        public int CurrentIndex
        {
            get { return _dict[CurrentKey]; }
        }

        /// <summary>
        /// Add a new item to the list, if it already exists then the 
        /// current item will be set to this item.
        /// </summary>
        /// <param name="uuid">Unique identifier for the item.</param>
        /// <param name="elem">The item to add.</param>
        /// <returns>true if item did not already exist.</returns>
        public bool AddOrUpdateCurrent(string uuid, T elem)
        {
            if (!_dict.ContainsKey(uuid))
            {
                List.Add(elem);
                _dict.Add(uuid, (List.Count - 1));
                CurrentKey = uuid;
                return true;
            }

            CurrentKey = uuid;
            return false;
        }

        /// <summary>
        /// Check if the container already has an item with this key.
        /// </summary>
        /// <param name="uuid">Unique identifier for the item.</param>
        /// <returns></returns>
        public bool Contains(string uuid)
        {
            return _dict.ContainsKey(uuid);
        }

        /// <summary>
        /// Returns the index for an item given it's unique identifier.
        /// </summary>
        /// <param name="uuid">Unique identifier for the item.</param>
        /// <returns>index of item or -1</returns>
        public int GetIndexFromUUID(string uuid)
        {
            if (!Contains(uuid)) throw new Exception("Specified item could not be found.");
            return _dict[uuid];
        }

        /// <summary>
        /// Returns an item given it's unique identifier.
        /// </summary>
        /// <param name="uuid">Unique identifier for the item</param>
        /// <returns>the item</returns>
        public T GetElement(string uuid)
        {
            int index = GetIndexFromUUID(uuid);
            return List[index];
        }

        /// <summary>
        /// Returns as item given it's index location.
        /// </summary>
        /// <param name="index">The item's index location.</param>
        /// <returns>the item</returns>
        public T GetElement(int index)
        {
            if (index < 0 || index > List.Count - 1) throw new Exception("Specified item could not be found.");
            return List[index];
        }
    }

    /// <summary>
    /// From Jeremy Tammik's RvtVa3c exporter:
    /// https://github.com/va3c/RvtVa3c
    /// An integer-based 3D point class.
    /// </summary>
    public class PointInt : IComparable<PointInt>
    {
        public long X { get; set; }
        public long Y { get; set; }
        public long Z { get; set; }

        /// <summary>
        /// Consider a Revit length zero 
        /// if is smaller than this.
        /// </summary>
        const double _eps = 1.0e-9;

        /// <summary>
        /// Conversion factor from feet to millimetres.
        /// </summary>
        const double _feet_to_mm = 25.4 * 12;

        /// <summary>
        /// Conversion a given length value 
        /// from feet to millimetre.
        /// </summary>
        public static long ConvertFeetToMillimetres(double d)
        {
            if (0 < d)
            {
                return _eps > d
                  ? 0
                  : (long)(_feet_to_mm * d + 0.5);
            }
            else
            {
                return _eps > -d
                  ? 0
                  : (long)(_feet_to_mm * d - 0.5);
            }
        }

        public PointInt(XYZ p, bool switch_coordinates)
        {
            X = ConvertFeetToMillimetres(p.X);
            Y = ConvertFeetToMillimetres(p.Y);
            Z = ConvertFeetToMillimetres(p.Z);

            if (switch_coordinates)
            {
                X = -X;
                long tmp = Y;
                Y = Z;
                Z = tmp;
            }
        }

        public int CompareTo(PointInt a)
        {
            long d = X - a.X;
            if (0 == d)
            {
                d = Y - a.Y;
                if (0 == d)
                {
                    d = Z - a.Z;
                }
            }
            return (0 == d) ? 0 : ((0 < d) ? 1 : -1);
        }
    }

    /// <summary>
    /// From Jeremy Tammik's RvtVa3c exporter:
    /// https://github.com/va3c/RvtVa3c
    /// A vertex lookup class to eliminate 
    /// duplicate vertex definitions.
    /// </summary>
    public class VertexLookupInt : Dictionary<PointInt, int>
    {
        /// <summary>
        /// Define equality for integer-based PointInt.
        /// </summary>
        class PointIntEqualityComparer : IEqualityComparer<PointInt>
        {
            public bool Equals(PointInt p, PointInt q)
            {
                return 0 == p.CompareTo(q);
            }

            public int GetHashCode(PointInt p)
            {
                return (p.X.ToString()
                  + "," + p.Y.ToString()
                  + "," + p.Z.ToString())
                  .GetHashCode();
            }
        }

        public VertexLookupInt() : base(new PointIntEqualityComparer())
        {
        }

        /// <summary>
        /// Return the index of the given vertex,
        /// adding a new entry if required.
        /// </summary>
        public int AddVertex(PointInt p)
        {
            return ContainsKey(p)
              ? this[p]
              : this[p] = Count;
        }
    }
}
