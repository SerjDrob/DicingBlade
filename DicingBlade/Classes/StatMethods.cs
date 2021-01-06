using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.IO;
using System.Reflection;
using netDxf;
using Newtonsoft.Json;

namespace DicingBlade.Classes
{
    internal static class StatMethods
    {
        public static void WriteObject<T>(this object obj, string file)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));

            using FileStream fileStream = new FileStream(file, FileMode.OpenOrCreate);
            serializer.Serialize(fileStream, obj);
        }
        public static T ReadObject<T>(this T obj, string file)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using FileStream fileStream = new FileStream(file, FileMode.Open);
            return (T)serializer.Deserialize(fileStream);
        }

        public static TV GetItemByIndex<TK, TV>(this Dictionary<TK, TV> dictionary, int index)
        {
            int count = dictionary.Count;
            if (index < 0 || index > count - 1)
                throw new Exception("Индекс вне диапазона коллекции");
            var keys = new TK[count];
            dictionary.Keys.CopyTo(keys, 0);
            return dictionary[keys[index]];
        }
        public static void CopyPropertiesTo<T>(this T source, T dest)
        {
            var plist = from prop in typeof(T).GetProperties() where prop.CanRead && prop.CanWrite select prop;

            foreach (PropertyInfo prop in plist)
            {
                prop.SetValue(dest, prop.GetValue(source, null), null);
            }
        }

        public static void SerializeObjectJson(this object obj, string filename)
        {
            using StreamWriter file = File.CreateText(filename);
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(file, obj);
        }
        public static T DeSerializeObjectJson<T>(this T _, string filename)
        {
            using StreamReader file = File.OpenText(filename);
            JsonSerializer serializer = new JsonSerializer();
            return (T)serializer.Deserialize(file, typeof(T));
        }
        /// <summary>
        /// Преобразует Vector3 в Vector2 отсекая Z
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector2 SplitZ(this Vector3 vector)
        {
            return new Vector2(vector.X, vector.Y);
        }
        public static int SetBit(this int variable, int pos)
        {
           var res = variable | 1 << pos;
            return res;
        }
        public static int ResetBit(this int variable, int pos)
        {
            var res = variable & ~(1 << pos);
            return res;
        }

        public static double GetVal(this (Ax num, double val)[] arr, Ax @enum)
        {
            return arr.Where(n => n.num == @enum).Select(v => v.val).First();
        }
    }
}
