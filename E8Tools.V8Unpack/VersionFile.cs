/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System;
using System.IO;

namespace E8Tools.V8Unpack
{
    internal class VersionFile
    {
        public const int COMPATIBILITY_V80316 = 80316;
        public const int COMPATIBILITY_UNKNOWN = 0;

        public VersionFile(int compatibility) {
            Compatibility = compatibility;
        }

        public int Compatibility { get; }

        public static VersionFile FromStream(Stream stream)
        {
            return new VersionFile(ExtractCompatibility(stream));
        }
        public static VersionFile FromFile(string path)
        {
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return FromStream(fileStream);
            }
        }

        private static int ExtractCompatibility(Stream stream)
        {
            var openCount = 3; // без лишних хитростей ищем третью открывающуюся скобку
            int ch;
            while (openCount > 0)
            {
                ch = stream.ReadByte();
                if (ch == -1)
                {
                    return COMPATIBILITY_UNKNOWN;
                }
                if (ch == '{') --openCount;

            }
            int compatibility = 0;
            ch = stream.ReadByte();
            while (ch >= '0' && ch <= '9')
            {
                compatibility = compatibility * 10 + (ch - '0');
                ch = stream.ReadByte();
            }
            return compatibility;
        }
    }
}
