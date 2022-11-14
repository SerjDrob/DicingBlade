using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;
using System.Reflection;
using System.Windows;
using netDxf.Entities;
using netDxf.Entities;
using netDxf;
using Newtonsoft.Json;
using DicingBlade.Properties;
using DicingBlade.ViewModels;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace DicingBlade.Classes
{
    static class StatMethods
    {
        public static void WriteObject<T>(this object obj, string file)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            
            using (FileStream fileStream = new FileStream(file, FileMode.OpenOrCreate))
            {
                serializer.Serialize(fileStream, obj);
            }
        }
        public static T ReadObject<T>(this T obj, string file)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (FileStream fileStream = new FileStream(file, FileMode.Open))
            {
                return (T)serializer.Deserialize(fileStream);
            }
        }

        public static V GetItemByIndex<K,V>(this Dictionary<K,V> dictionary, int index)
        {
            int count = dictionary.Count;
            if (index < 0 || index > count - 1)
                throw new Exception("Индекс вне диапазона коллекции");
            K[] keys = new K[count];
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
            using (StreamWriter file = File.CreateText(filename))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, obj);
            }
        }
        public static T DeSerializeObjectJson<T>(this T _, string filename)
        {
            using (StreamReader file = File.OpenText(filename))
            {
                JsonSerializer serializer = new JsonSerializer();
                return (T)serializer.Deserialize(file,typeof(T));
            }
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
    }
    internal static class ExtensionMethods
    {

        internal static void SerializeObject(this object obj, string filePath)
        {
            var json = JsonConvert.SerializeObject(obj);
            using var writer = new StreamWriter(filePath, false);
            var l = new TextWriterTraceListener(writer);
            l.WriteLine(json);
            l.Flush();
        }
        internal static T? DeserilizeObject<T>(string filePath)
        {
            var obj = JsonConvert.DeserializeObject(File.ReadAllText(filePath), typeof(T));
            return (T?)obj;
        }

        internal static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> en)
        {
            return new ObservableCollection<T>(en);
        }
    }
}
