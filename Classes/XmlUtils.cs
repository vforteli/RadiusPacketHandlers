using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Flexinets.Radius
{
    public static class XmlUtils
    {
        public static String ToReadableString(this XmlNode node)
        {
            using (var sw = new StringWriter())
            {
                var xtw = new XmlTextWriter(sw) { Formatting = Formatting.Indented };
                node.WriteTo(xtw);
                return sw.ToString();
            }
        }
    }
}
