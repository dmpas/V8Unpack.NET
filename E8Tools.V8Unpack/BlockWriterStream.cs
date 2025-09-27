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
        private uint _pageSize;
        private FormatReader _reader;
        private readonly BlockHeader _startHeader;
        private BlockHeader _currentBlock;
        private long _usedSize = 0;

        internal BlockWriterStream(Stream stream, FormatReader formatReader, BlockHeader startHeader)
        {
            if (!stream.CanSeek)
            {
                throw new StreamMustCanSeekException();
            }
            _stream = stream;
            _firstBlockPosition = _stream.Position;
            _reader = formatReader;
            _startHeader = startHeader;
            _currentBlock = startHeader;

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
        }

        public override void Close()
        {
            FinishBlock();
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
                    throw new NotImplementedException();
                }
            }
            _stream.Write(buffer, offset, count);
            _usedSize += count;
        }

    }
}
