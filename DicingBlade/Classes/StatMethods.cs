using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;
using System.Windows;
using netDxf.Entities;
using netDxf.Entities;
using netDxf;

namespace DicingBlade.Classes
{
    static class StatMethods
    {
        public static void WriteObject<T>(this T obj, string file)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (FileStream fileStream = new FileStream(file, FileMode.Truncate))
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
}
