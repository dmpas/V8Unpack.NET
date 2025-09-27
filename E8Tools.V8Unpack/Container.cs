/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using E8Tools.V8Unpack.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace E8Tools.V8Unpack
{
    /// <summary>
    /// Работа с контейнером
    /// </summary>
    public class Container : IDisposable
    {
        private readonly bool _ownedStream;
        private readonly ContainerHeader _header;
        private readonly List<ElementAddress> _elements;
        private readonly FormatReader _formatReader;
        private readonly PageAllocator _pageAllocator;
        private bool _changed = false;

        private Container(Stream stream, bool ownedStream = false) {

            if (!stream.CanSeek)
            {
                throw new StreamMustCanSeekException();
            }

            var root = ContainerRoot.FindRoot(stream) ?? throw new NotAContainerException();
            _header = root._header;
            _elements = root._elements;
            _formatReader = root._formatReader;

            Stream = _formatReader.WrapStream(stream);
            _ownedStream = ownedStream;
            _pageAllocator = new PageAllocator(Stream, _formatReader, _header);
        }

        private Container(Stream stream, FormatReader formatReader)
        {

            if (!stream.CanSeek)
            {
                throw new StreamMustCanSeekException();
            }

            _header = new ContainerHeader(formatReader.V8_FF_SIGNATURE, formatReader.DEFAULT_PAGE_SIZE, 0, 0);
            _elements = new List<ElementAddress>();
            _formatReader = formatReader;

            Stream = _formatReader.WrapStream(stream, true);
            _formatReader.WriteContainerHeader(Stream, _header);
            _pageAllocator = new PageAllocator(Stream, _formatReader, _header);
            ReservePage();
              
            _ownedStream = true;
        }

        private Stream Stream { get; }

        /// <summary>
        /// Определяет тип адресации внутри контейнера
        /// </summary>
        public ContainerAddressType AddressType => _formatReader.AddressType;

        /// <summary>
        /// Возвращает количество файлов в контейнере.
        /// </summary>
        public int Count => _elements.Count;

        /// <summary>
        /// Считывает информацию о файлах контейнера.
        /// </summary>
        /// <returns>Файлы контейнера</returns>
        public IEnumerable<File> Files()
        {
            foreach (var entry in _elements)
            {
                Stream.Seek((long)entry.HeaderAddress, SeekOrigin.Begin);
                using (var headerReader = new BlockReaderStream(Stream, _formatReader))
                {
                    var elementHeader = Utils.ReadElementHeader(headerReader);
                    yield return new File(this,
                        elementHeader.Name,
                        elementHeader.DateCreation,
                        elementHeader.DateModification,
                        entry.DataAddress
                    );
                }
            }
        }

        public void Close()
        {
            if (_changed)
            {
                _header.StorageVer += 1;
                UpdateHeader();
                _changed = false;
            }
            Stream.Flush();
        }

        private void UpdateHeader()
        {
            Stream.Seek(0, SeekOrigin.Begin);
            _formatReader.WriteContainerHeader(Stream, _header);
            var page = new BlockHeader(0, _header.PageSize, _formatReader.V8_FF_SIGNATURE);
            using (var writer = new BlockWriterStream(Stream, _formatReader, page, _pageAllocator))
            {
                foreach (var el in _elements)
                {
                    if (el.HeaderAddress != _formatReader.V8_FF_SIGNATURE)
                    {
                        _formatReader.WriteElementAddress(writer, el);
                    }
                }
            }
        }

        private void ReservePage()
        {
            var page = _pageAllocator.NextPage(_formatReader.DEFAULT_PAGE_SIZE);
            using (var writer = new BlockWriterStream(Stream, _formatReader, page, _pageAllocator))
            {
                //
            }
        }

        public File AddFile(string name, Stream data, bool packData = true)
        {
            var page = _pageAllocator.NextPage();
            var dataPosition = (ulong)Stream.Position;
            using (var blockWriter = new BlockWriterStream(Stream, _formatReader, page, _pageAllocator))
            {
                if (packData)
                {
                    using (var deflator = new DeflateStream(blockWriter, CompressionLevel.Fastest))
                    {
                        data.CopyTo(deflator);
                    }
                }
                else
                {
                    data.CopyTo(blockWriter);
                }
            }

            var elementHeader = new ElementHeaderData(name);
            var headerPage = _pageAllocator.NextPage(_header.PageSize);
            var headerPosition = (ulong)Stream.Position;
            using (var blockWriter = new BlockWriterStream(Stream, _formatReader, headerPage, _pageAllocator)) {
                Utils.WriteElementHeader(blockWriter, elementHeader);
            }

            var entryElement = new ElementAddress(headerPosition, dataPosition, _formatReader.V8_FF_SIGNATURE);
            _elements.Add(entryElement);
            _changed = true;

            var file = new File(this, name, DateTime.Now, DateTime.Now, dataPosition);
            return file;
        }

        /// <summary>
        /// Создает поток чтения данных файла контейнера.
        /// </summary>
        /// <param name="file">Файл контейнера</param>
        /// <param name="forceDecompression">Признак необходимости принудительной распаковки данных файла</param>
        /// <returns>Поток для чтения</returns>
        public Stream OpenStream(File file, bool forceDecompression = true)
        {
            if (file.DataOffset != 0 && file.DataOffset != _formatReader.V8_FF_SIGNATURE)
            {
                Stream.Seek((long)file.DataOffset, SeekOrigin.Begin);
                var blockReader = new BlockReaderStream(Stream, _formatReader);
                if (blockReader.IsPacked && forceDecompression)
                {
                    return new DeflateStream(blockReader, CompressionMode.Decompress);
                }

                return blockReader;

            }
            return Stream.Null;
        }

        /// <summary>
        /// Определяет, что поток содержит в себе контейнер
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>true, если из потока можно прочитать контейнер. false, если поток не содержит контейнер</returns>
        public static bool IsContainer(Stream stream)
        {
            return ContainerRoot.FindRoot(stream) != null;
        }

        /// <summary>
        /// Создает контейнер из потока
        /// </summary>
        /// <param name="stream">Поток</param>
        /// <returns>Контейнер</returns>
        public static Container FromStream(Stream stream)
        {
            return new Container(stream);
        }

        /// <summary>
        /// Создает поток по имени файла.
        /// </summary>
        /// <param name="path">Путь к файлу</param>
        /// <returns>Контейнер</returns>
        public static Container FromFile(string path)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return new Container(stream, true);
        }

        public static void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName, ContainerAddressType addressType = ContainerAddressType.Auto, bool packFiles = true)
        {
            if (addressType == ContainerAddressType.Auto)
            {
                FileInfo fi = new FileInfo(destinationArchiveFileName);
                if (string.Equals(fi.Extension, ".EPF", StringComparison.InvariantCultureIgnoreCase)
                    || string.Equals(fi.Extension, ".ERF", StringComparison.InvariantCultureIgnoreCase))
                {
                    addressType = ContainerAddressType._32bit;
                }
                else
                {
                    addressType = AnalyzeAddressType(sourceDirectoryName);
                }
            }

            FormatReader formatReader = addressType == ContainerAddressType._64bit ? FormatReader64.Instance : FormatReader.Instance;

            var containerStream = new FileStream(destinationArchiveFileName, FileMode.Create, FileAccess.Write);
            using (var container = new Container(containerStream, formatReader))
            {
                foreach (var dir in Directory.EnumerateDirectories(sourceDirectoryName))
                {
                    var dirPath = new DirectoryInfo(dir);
                    var tempFilename = Path.GetTempFileName();
                    CreateFromDirectory(dir, tempFilename, ContainerAddressType.Auto, false);
                    using (var fileStream = new FileStream(tempFilename, FileMode.Open, FileAccess.Read)) {
                        container.AddFile(dirPath.Name, fileStream, packFiles);
                    }
                    System.IO.File.Delete(tempFilename);
                }
                foreach (var file in Directory.EnumerateFiles(sourceDirectoryName))
                {
                    var filePath = new FileInfo(file);
                    using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        container.AddFile(filePath.Name, fileStream, packFiles);
                    }
                }
            }
        }

        private static ContainerAddressType AnalyzeAddressType(string sourceDirectoryName)
        {
            var versionFilePath = Path.Combine(sourceDirectoryName, "version");
            if (System.IO.File.Exists(versionFilePath))
            {
                var version = VersionFile.FromFile(versionFilePath);
                if (version.Compatibility >= VersionFile.COMPATIBILITY_V80316)
                {
                    return ContainerAddressType._64bit;
                }
            }
            return ContainerAddressType._32bit;
        }

        public void Dispose()
        {
            Close();
            if (_ownedStream)
            {
                Stream.Dispose();
            }
        }
    }
}
