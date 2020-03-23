using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;

namespace DicingBlade.Classes
{
    static class StatMethods
    {
        public static void WriteObject<T>(this T obj, string file) 
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));            
            using (FileStream fileStream = new FileStream(file,FileMode.Truncate))
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
    }
}
