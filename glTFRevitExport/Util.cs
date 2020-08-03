using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace glTFRevitExport
{
    class Util
    {
        public static int[] GetVec3MinMax(List<int> vec3)
        {
            int minVertexX = int.MaxValue;
            int minVertexY = int.MaxValue;
            int minVertexZ = int.MaxValue;
            int maxVertexX = int.MinValue;
            int maxVertexY = int.MinValue;
            int maxVertexZ = int.MinValue;
            for (int i = 0; i < vec3.Count; i += 3)
            {
                if (vec3[i] < minVertexX) minVertexX = vec3[i];
                if (vec3[i] > maxVertexX) maxVertexX = vec3[i];

                if (vec3[i + 1] < minVertexY) minVertexY = vec3[i + 1];
                if (vec3[i + 1] > maxVertexY) maxVertexY = vec3[i + 1];

                if (vec3[i + 2] < minVertexZ) minVertexZ = vec3[i + 2];
                if (vec3[i + 2] > maxVertexZ) maxVertexZ = vec3[i + 2];
            }
            return new int[] { minVertexX, maxVertexX, minVertexY, maxVertexY, minVertexZ, maxVertexZ };
        }

        public static long[] GetVec3MinMax(List<long> vec3)
        {
            long minVertexX = long.MaxValue;
            long minVertexY = long.MaxValue;
            long minVertexZ = long.MaxValue;
            long maxVertexX = long.MinValue;
            long maxVertexY = long.MinValue;
            long maxVertexZ = long.MinValue;
            for (int i = 0; i < (vec3.Count / 3); i += 3)
            {
                if (vec3[i] < minVertexX) minVertexX = vec3[i];
                if (vec3[i] > maxVertexX) maxVertexX = vec3[i];

                if (vec3[i + 1] < minVertexY) minVertexY = vec3[i + 1];
                if (vec3[i + 1] > maxVertexY) maxVertexY = vec3[i + 1];

                if (vec3[i + 2] < minVertexZ) minVertexZ = vec3[i + 2];
                if (vec3[i + 2] > maxVertexZ) maxVertexZ = vec3[i + 2];
            }
            return new long[] { minVertexX, maxVertexX, minVertexY, maxVertexY, minVertexZ, maxVertexZ };
        }

        public static float[] GetVec3MinMax(List<float> vec3)
        {
            
            List<float> xValues = new List<float>();
            List<float> yValues = new List<float>();
            List<float> zValues = new List<float>();
            for (int i = 0; i < vec3.Count; i++)
            {
                if ((i % 3) == 0) xValues.Add(vec3[i]);
                if ((i % 3) == 1) yValues.Add(vec3[i]);
                if ((i % 3) == 2) zValues.Add(vec3[i]);
            }

            float maxX = xValues.Max();
            float minX = xValues.Min();
            float maxY = yValues.Max();
            float minY = yValues.Min();
            float maxZ = zValues.Max();
            float minZ = zValues.Min();

            return new float[] { minX, maxX, minY, maxY, minZ, maxZ };
        }

        public static int[] GetScalarMinMax(List<int> scalars)
        {
            int minFaceIndex = int.MaxValue;
            int maxFaceIndex = int.MinValue;
            for (int i = 0; i < scalars.Count; i++)
            {
                int currentMin = Math.Min(minFaceIndex, scalars[i]);
                if (currentMin < minFaceIndex) minFaceIndex = currentMin;

                int currentMax = Math.Max(maxFaceIndex, scalars[i]);
                if (currentMax > maxFaceIndex) maxFaceIndex = currentMax;
            }
            return new int[] { minFaceIndex, maxFaceIndex };
        }

        /// <summary>
        /// From Jeremy Tammik's RvtVa3c exporter:
        /// https://github.com/va3c/RvtVa3c
        /// Return a string for a real number
        /// formatted to two decimal places.
        /// </summary>
        public static string RealString(double a)
        {
            return a.ToString("0.##");
        }

        /// <summary>
        /// From Jeremy Tammik's RvtVa3c exporter:
        /// https://github.com/va3c/RvtVa3c
        /// Return a string for an XYZ point
        /// or vector with its coordinates
        /// formatted to two decimal places.
        /// </summary>
        public static string PointString(XYZ p)
        {
            return string.Format("({0},{1},{2})",
              RealString(p.X),
              RealString(p.Y),
              RealString(p.Z));
        }

        /// <summary>
        /// From Jeremy Tammik's RvtVa3c exporter:
        /// https://github.com/va3c/RvtVa3c
        /// Return an integer value for a Revit Color.
        /// </summary>
        public static int ColorToInt(Color color)
        {
            return ((int)color.Red) << 16
              | ((int)color.Green) << 8
              | (int)color.Blue;
        }

        /// <summary>
        /// From Jeremy Tammik's RvtVa3c exporter:
        /// https://github.com/va3c/RvtVa3c
        /// Extract a true or false value from the given
        /// string, accepting yes/no, Y/N, true/false, T/F
        /// and 1/0. We are extremely tolerant, i.e., any
        /// value starting with one of the characters y, n,
        /// t or f is also accepted. Return false if no 
        /// valid Boolean value can be extracted.
        /// </summary>
        public static bool GetTrueOrFalse(string s, out bool val)
        {
            val = false;

            if (s.Equals(Boolean.TrueString,
              StringComparison.OrdinalIgnoreCase))
            {
                val = true;
                return true;
            }
            if (s.Equals(Boolean.FalseString,
              StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (s.Equals("1"))
            {
                val = true;
                return true;
            }
            if (s.Equals("0"))
            {
                return true;
            }
            s = s.ToLower();

            if ('t' == s[0] || 'y' == s[0])
            {
                val = true;
                return true;
            }
            if ('f' == s[0] || 'n' == s[0])
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// From Jeremy Tammik's RvtVa3c exporter:
        /// https://github.com/va3c/RvtVa3c
        /// Return a string describing the given element:
        /// .NET type name,
        /// category name,
        /// family and symbol name for a family instance,
        /// element id and element name.
        /// </summary>
        public static string ElementDescription(Element e)
        {
            if (null == e)
            {
                return "<null>";
            }

            // For a wall, the element name equals the
            // wall type name, which is equivalent to the
            // family name ...

            FamilyInstance fi = e as FamilyInstance;

            string typeName = e.GetType().Name;

            string categoryName = (null == e.Category)
              ? string.Empty
              : e.Category.Name + " ";

            string familyName = (null == fi)
              ? string.Empty
              : fi.Symbol.Family.Name + " ";

            string symbolName = (null == fi
              || e.Name.Equals(fi.Symbol.Name))
                ? string.Empty
                : fi.Symbol.Name + " ";

            return string.Format("{0} {1}{2}{3}<{4} {5}>",
              typeName, categoryName, familyName,
              symbolName, e.Id.IntegerValue, e.Name);
        }

        /// <summary>
        /// From Jeremy Tammik's RvtVa3c exporter:
        /// https://github.com/va3c/RvtVa3c
        /// Return a dictionary of all the given 
        /// element parameter names and values.
        /// </summary>
        public static Dictionary<string, string> GetElementProperties(Element e, bool includeType)
        {
            IList<Parameter> parameters
              = e.GetOrderedParameters();

            Dictionary<string, string> a = new Dictionary<string, string>(parameters.Count);

            // Add element category
            if (e.Category != null)
            {
                a.Add("Element Category", e.Category.Name);
            }



            foreach (Parameter p in parameters)
            {
                string key = p.Definition.Name;

                if (!a.ContainsKey(key))
                {
                    string val;
                    if (StorageType.String == p.StorageType)
                    {
                        val = p.AsString();
                    }
                    else
                    {
                        val = p.AsValueString();
                    }
                    if (!string.IsNullOrEmpty(val))
                    {
                        a.Add(key, val);
                    }
                }
            }

            if (includeType)
            {
                ElementId idType = e.GetTypeId();

                if (idType != null && ElementId.InvalidElementId != idType)
                {
                    Document doc = e.Document;
                    Element typ = doc.GetElement(idType);
                    parameters = typ.GetOrderedParameters();
                    foreach (Parameter p in parameters)
                    {
                        string key = "Type " + p.Definition.Name;

                        if (!a.ContainsKey(key))
                        {
                            string val;
                            if (StorageType.String == p.StorageType)
                            {
                                val = p.AsString();
                            }
                            else
                            {
                                val = p.AsValueString();
                            }
                            if (!string.IsNullOrEmpty(val))
                            {
                                a.Add(key, val);
                            }
                        }
                    }
                }
            }

            if (a.Count == 0) return null;
            else return a;
        }
    }
}
