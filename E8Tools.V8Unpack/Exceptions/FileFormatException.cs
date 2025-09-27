/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System;

namespace E8Tools.V8Unpack.Exceptions
{
    internal class FileFormatException : Exception
    {
        public FileFormatException() : base() { }
        public FileFormatException(string message) : base($"Format exception: {message}") { }
    }

    internal class BlockHeaderExpected : FileFormatException
    {
        public BlockHeaderExpected(long position) : base(MakeMessage(position)) { }
        public BlockHeaderExpected(long position, long offset) : base(MakeMessageWithOffset(position, offset)) { }

        private static string MakeMessage(long position) {
            return $"Block header expected at {position:X8}";
        }
        private static string MakeMessageWithOffset(long position, long offset)
        {
            var global = position + offset;
            return $"Block header expected at {position:X16} (absolute: {global:X16})";
        }

    }
}
