/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using E8Tools.V8Unpack.Exceptions;
using System;
using System.IO;

namespace E8Tools.V8Unpack
{
    /// <summary>
    /// Поточная запись в блочный файл.
    /// </summary>
    public class BlockWriterStream : Stream
    {

        private long _length = 0;
        private readonly Stream _stream;
        private long _firstBlockPosition;
        private FormatReader _reader;
        private readonly BlockHeader _startHeader;
        private BlockHeader _currentBlock;
        private long _currentBlockPosition;
        private long _usedSize = 0;
        private long _totalDataSize = 0;
        private readonly PageAllocator _pageAllocator;

        internal BlockWriterStream(Stream stream, FormatReader formatReader, BlockHeader startHeader, PageAllocator allocator)
        {
            if (!stream.CanSeek)
            {
                throw new StreamMustCanSeekException();
            }
            _stream = stream;
            _firstBlockPosition = _stream.Position;
            _currentBlockPosition = _stream.Position;
            _reader = formatReader;
            _startHeader = startHeader;
            _currentBlock = startHeader;
            _pageAllocator = allocator;

            _reader.WriteBlockHeader(_stream, _startHeader);
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position { get => _length; set => throw new NotSupportedException(); }

        public override void Flush()
        {
            _stream.Flush();
        }

        private void FinishBlock()
        {
            _currentBlock.DataSize = (ulong)_usedSize;
            if (_currentBlock.PageSize == _reader.V8_FF_SIGNATURE)
            {
                _currentBlock.PageSize = _currentBlock.DataSize;
            }
            else
            {
                while (_usedSize < (long)_currentBlock.PageSize)
                {
                    _stream.WriteByte(0);
                    _usedSize++;
                }
            }
            _stream.Seek(_currentBlockPosition, SeekOrigin.Begin);
            _reader.WriteBlockHeader(_stream, _currentBlock);
        }

        public override void Close()
        {
            FinishBlock();
            _startHeader.DataSize = (ulong)_totalDataSize;
            _stream.Seek(_firstBlockPosition, SeekOrigin.Begin);
            _reader.WriteBlockHeader(_stream, _startHeader);
            base.Close();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
            if (_currentBlock.PageSize != _reader.V8_FF_SIGNATURE)
            {
                var maxSize = (long)_currentBlock.PageSize - _usedSize;
                if (maxSize < count)
                {
                    // Блок заканчивается
                    var toWrite = (int)maxSize;
                    _stream.Write(buffer, offset, toWrite);
                    _currentBlock.DataSize = _currentBlock.PageSize;
                    var newBlock = _pageAllocator.NextPage();
                    var newBlockPosition = _stream.Position;
                    _currentBlock.NextPageAddr = (ulong)newBlockPosition;
                    
                    FinishBlock();

                    _currentBlock = newBlock;
                    _currentBlockPosition = newBlockPosition;
                    _usedSize = 0;

                    _stream.Seek(newBlockPosition, SeekOrigin.Begin);
                    _reader.WriteBlockHeader(_stream, _currentBlock);
                    offset += toWrite;
                    count -= toWrite;
                }
            }
            _stream.Write(buffer, offset, count);
            _usedSize += count;
            _totalDataSize += count;
        }

    }
}
