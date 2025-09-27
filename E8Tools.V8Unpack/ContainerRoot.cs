/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using E8Tools.V8Unpack.Exceptions;
using System.Collections.Generic;
using System.IO;

namespace E8Tools.V8Unpack
{
    internal class ContainerRoot
    {
        public readonly ContainerHeader _header;
        public readonly List<ElementAddress> _elements = new List<ElementAddress>();
        public readonly FormatReader _formatReader;

        public ContainerRoot(ContainerHeader header, List<ElementAddress> elements, FormatReader formatReader)
        {
            _header = header;
            _elements = elements;
            _formatReader = formatReader;
        }

        public ContainerRoot() {
        }

        private static List<ElementAddress> ReadFiles(Stream stream, FormatReader reader)
        {
            byte[] addressTable;
            try
            {
                addressTable = BlockReaderStream.ReadDataBlock(stream, reader);
            }
            catch (FileFormatException)
            {
                return null;
            }

            var _elements = new List<ElementAddress>();
            var ms = new MemoryStream(addressTable);
            while (true)
            {
                try
                {
                    var fileAddress = reader.ReadElementAddress(ms);
                    _elements.Add(fileAddress);
                }
                catch (FileFormatException)
                {
                    break;

                }
            }
            return _elements;
        }

        public static bool TryReader(Stream source, FormatReader reader, ref ContainerRoot containerRoot)
        {
            ContainerHeader containerHeader = null;
            Stream stream = reader.WrapStream(source);
            stream.Seek(0, SeekOrigin.Begin);
            try
            {
                containerHeader = reader.ReadContainerHeader(stream);
                if (containerHeader == null) return false;
            }
            catch (FileFormatException)
            {
                return false;
            }

            var elements = ReadFiles(stream, reader);
            if (elements == null) return false;

            containerRoot = new ContainerRoot(containerHeader, elements, reader);
            return true;
        }

        public static ContainerRoot FindRoot(Stream stream)
        {
            ContainerRoot root = null;
            if (TryReader(stream, FormatReader64.Instance, ref root)) return root;
            if (TryReader(stream, FormatReader.Instance, ref root)) return root;
            return null;
        }

    }
}
