/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using E8Tools.V8Unpack.Exceptions;
using System;
using System.IO;
using System.IO.Compression;

namespace E8Tools.V8Unpack
{
    /// <summary>
    /// Поточное чтение данных из блочного файла.
    /// </summary>
    public class BlockReaderStream : Stream
    {
        private BlockHeader currentHeader;
        private readonly Stream _reader;
        private readonly FormatReader _formatReader;
        private readonly ulong _dataSize;

        private byte[] _currentPageData;
        private int _currentPageOffset;
        private long _position;
        private bool _isPacked;
        private readonly long _streamStartPosition;
        private bool _isContainer;

        public BlockReaderStream(Stream basicStream, FormatReader formatReader)
        {
            _reader = basicStream;
            _formatReader = formatReader;

            currentHeader = _formatReader.ReadBlockHeader(_reader);
            _dataSize = currentHeader.DataSize;
            _position = 0;
            _streamStartPosition = basicStream.Position;
            ReadPage();
            AnalyzeState();
        }

        public void Reset()
        {
            _position = 0;
            _reader.Seek(_streamStartPosition, SeekOrigin.Begin);
            ReadPage();
        }

        private void ReadPage()
        {
            var currentDataSize = Math.Min(_dataSize, currentHeader.PageSize);
            if (currentDataSize > int.MaxValue)
            {
                // больше 2ГБ на страницу не умеем
                throw new FileFormatException();
            }
            _currentPageData = new byte[currentDataSize];
            _reader.Read(_currentPageData, 0, (int)currentDataSize);
            _currentPageOffset = 0;
        }

        private void MoveNextBlock()
        {
            if (currentHeader.NextPageAddr == _formatReader.V8_FF_SIGNATURE)
            {
                _currentPageData = null;
                return;
            }
            _reader.Seek((long)currentHeader.NextPageAddr, SeekOrigin.Begin);
            currentHeader = _formatReader.ReadBlockHeader(_reader);
            if (currentHeader == null)
            {
                throw new FileFormatException();
            }
            ReadPage();
        }

        private void AnalyzeState()
        {
            byte[] bufferToCheck = _currentPageData;
            try
            {
                using (var srcMem = new MemoryStream(_currentPageData))
                using (var decompressor = new DeflateStream(srcMem, CompressionMode.Decompress))
                using (var destMem = new MemoryStream(bufferToCheck.Length * 2))
                {

                    decompressor.CopyTo(destMem);
                    _isPacked = true;
                    bufferToCheck = destMem.ToArray();
                }
            }
            catch (InvalidDataException)
            {
                _isPacked = false;
            }

            using (var memoryStream = new MemoryStream(bufferToCheck))
            {
                _isContainer = Container.IsContainer(memoryStream);
            }
        }

        /// <summary>
        /// Указывает на то, что поток упакован Deflate
        /// </summary>
        public bool IsPacked => _isPacked;

        /// <summary>
        /// Указывает на то, что данные потока могут быть контейнером
        /// </summary>
        public bool IsContainer => _isContainer;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => (long)_dataSize;

        public override long Position
        {
            get => _position;

            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_currentPageData == null)
            {
                return 0;
            }

            int bytesRead = 0;
            int countLeft = count;

            while (countLeft > 0)
            {
                var leftInPage = _currentPageData.Length - _currentPageOffset;
                if (leftInPage == 0)
                {
                    MoveNextBlock();
                    if (_currentPageData == null)
                    {
                        break;
                    }
                }

                var readFromCurrentPage = Math.Min(leftInPage, countLeft);

                Buffer.BlockCopy(_currentPageData, _currentPageOffset, buffer, offset, readFromCurrentPage);
                _currentPageOffset += readFromCurrentPage;
                offset += readFromCurrentPage;

                bytesRead += readFromCurrentPage;
                countLeft -= readFromCurrentPage;
            }
            
            _position += bytesRead;

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Считывает текущий блок данных из файла.
        /// </summary>
        /// <returns>Считанные данные.</returns>
        /// <param name="reader">Входящий поток.</param>
        public static byte[] ReadDataBlock(Stream reader, FormatReader formatReader)
        {
            using (var blockReader = new BlockReaderStream(reader, formatReader))
            {
                var buf = new byte[blockReader.Length];
                blockReader.Read(buf, 0, buf.Length);
                return buf;
            }
        }
    }
}
