/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System.IO;

namespace E8Tools.V8Unpack
{
    internal class PageAllocator
    {
        private ulong _nextFreePageAddress;
        private readonly FormatReader _reader;
        private readonly Stream _stream;

        public PageAllocator(Stream stream, FormatReader reader, ContainerHeader header)
        {
            _stream = stream;
            _nextFreePageAddress = header.FreePageAddress;
            _reader = reader;
        }

        public BlockHeader NextPage()
        {
            return NextPage(_reader.V8_FF_SIGNATURE);
        }

        public BlockHeader NextPage(ulong pageSize)
        {
            _stream.Seek(0, SeekOrigin.End);
            var blockHeader = new BlockHeader(0, pageSize, _reader.V8_FF_SIGNATURE);
            return blockHeader;
        }
    }
}
