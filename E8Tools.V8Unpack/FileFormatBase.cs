/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using E8Tools.V8Unpack.Exceptions;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace E8Tools.V8Unpack
{
    public class ContainerHeader
    {
        public ulong FreePageAddress;
        public uint PageSize;
        public uint StorageVer;
        public uint Reserved;

        public ContainerHeader(ulong freePageAddress, uint pageSize, uint storageVer, uint reserved)
        {
            FreePageAddress = freePageAddress;
            PageSize = pageSize;
            StorageVer = storageVer;
            Reserved = reserved;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ContainerHeaderDto64
    {
        public readonly UInt64 FreePageAddress;
        public readonly UInt32 PageSize;
        public readonly UInt32 StorageVer;
        public readonly UInt32 Reserved;

        public ContainerHeaderDto64(UInt64 freePageAddress, UInt32 pageSize, UInt32 storageVer, UInt32 reserved)
        {
            FreePageAddress = freePageAddress;
            PageSize = pageSize;
            StorageVer = storageVer;
            Reserved = reserved;
        }

        public ContainerHeader Cast()
        {
            return new ContainerHeader(
                FreePageAddress,
                PageSize,
                StorageVer,
                Reserved
            );
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ContainerHeaderDto32
    {
        public readonly UInt32 FreePageAddress;
        public readonly UInt32 PageSize;
        public readonly UInt32 StorageVer;
        public readonly UInt32 Reserved;

        public ContainerHeaderDto32(UInt32 freePageAddress, UInt32 pageSize, UInt32 storageVer, UInt32 reserved)
        {
            FreePageAddress = freePageAddress;
            PageSize = pageSize;
            StorageVer = storageVer;
            Reserved = reserved;
        }

        public ContainerHeader Cast()
        {
            return new ContainerHeader(
                FreePageAddress,
                PageSize,
                StorageVer,
                Reserved
            );
        }
    }

    public class ElementAddress
    {
        public readonly ulong HeaderAddress;
        public readonly ulong DataAddress;
        public readonly ulong Signature;

        public ElementAddress(ulong headerAddress, ulong dataAddress, ulong signature)
        {
            HeaderAddress = headerAddress;
            DataAddress = dataAddress;
            Signature = signature;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ElementAddressDto32
    {
        public readonly UInt32 HeaderAddress;
        public readonly UInt32 DataAddress;
        public readonly UInt32 Signature;

        public ElementAddressDto32(UInt32 headerAddress, UInt32 dataAddress, UInt32 signature)
        {
            HeaderAddress = headerAddress;
            DataAddress = dataAddress;
            Signature = signature;
        }

        public ElementAddress Cast()
        {
            return new ElementAddress(HeaderAddress, DataAddress, Signature);
        }
    }

    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ElementAddressDto64
    {
        public readonly UInt64 HeaderAddress;
        public readonly UInt64 DataAddress;
        public readonly UInt64 Signature;

        public ElementAddressDto64(UInt64 headerAddress, UInt64 dataAddress, UInt64 signature)
        {
            HeaderAddress = headerAddress;
            DataAddress = dataAddress;
            Signature = signature;
        }

        public ElementAddress Cast()
        {
            return new ElementAddress(HeaderAddress, DataAddress, Signature);
        }
    }

    public class ElementHeaderData
    {
        public DateTime DateCreation;
        public DateTime DateModification;
        public uint Version;

        public string Name;

        public ElementHeaderData(string name) : this(name, DateTime.Now, DateTime.Now, 0)
        {

        }

        public ElementHeaderData(string name, DateTime dateCreation, DateTime dateModification, uint version)
        {
            DateCreation = dateCreation;
            DateModification = dateModification;
            Version = version;
            Name = name;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ElementHeaderDataDto
    {
        public readonly UInt64 DateCreation;
        public readonly UInt64 DateModification;
        public readonly UInt32 Version;

        public ElementHeaderDataDto(UInt64 dateCreation, UInt64 dateModification, UInt32 version)
        {
            DateCreation = dateCreation;
            DateModification = dateModification;
            Version = version;
        }
    }

    public sealed class BlockHeader
    {
        public ulong DataSize;
        public ulong PageSize;
        public ulong NextPageAddr;

        public BlockHeader(ulong dataSize, ulong pageSize, ulong nextPageAddr)
        {
            DataSize = dataSize;
            PageSize = pageSize;
            NextPageAddr = nextPageAddr;
        }

        public static bool TryRead<AddressUIntType>(Stream stream, out BlockHeader header) where AddressUIntType : struct
        {
            header = null;
            
            if (!Utils.ReadCertainChar(stream, 13) || !Utils.ReadCertainChar(stream, 10)) return false;
            
            ulong DataSize = Utils.ReadUIntFromHexString<AddressUIntType>(stream);
            
            if (!Utils.ReadCertainChar(stream, 32)) return false;
            
            ulong PageSize = Utils.ReadUIntFromHexString<AddressUIntType>(stream);

            if (!Utils.ReadCertainChar(stream, 32)) return false;

            ulong NextPageAddr = Utils.ReadUIntFromHexString<AddressUIntType>(stream);

            if (!Utils.ReadCertainChar(stream, 32)) return false;
            if (!Utils.ReadCertainChar(stream, 13) || !Utils.ReadCertainChar(stream, 10)) return false;

            header = new BlockHeader(DataSize, PageSize, NextPageAddr);
            return true;
        }

        public static bool TryWrite<AddressUIntType>(Stream stream, BlockHeader header) where AddressUIntType : struct
        {
            stream.WriteByte(13);
            stream.WriteByte(10);
            Utils.WriteUIntAsHexString<AddressUIntType>(stream, header.DataSize);
            stream.WriteByte(32);
            Utils.WriteUIntAsHexString<AddressUIntType>(stream, header.PageSize);
            stream.WriteByte(32);
            Utils.WriteUIntAsHexString<AddressUIntType>(stream, header.NextPageAddr);
            stream.WriteByte(32);
            stream.WriteByte(13);
            stream.WriteByte(10);

            return true;
        }
    }

    public class FormatReader
    {

        public static readonly FormatReader Instance = new FormatReader();

        public virtual ContainerAddressType AddressType { get; } = ContainerAddressType._32bit;

        public virtual ulong V8_FF_SIGNATURE { get; } = 0x7fffffff;
        public virtual uint DEFAULT_PAGE_SIZE { get; } = 0x200;

        public virtual ContainerHeader ReadContainerHeader(Stream stream)
        {
            return Utils.Read<ContainerHeaderDto32>(stream).Cast();
        }

        public virtual void WriteContainerHeader(Stream stream, ContainerHeader header)
        {
            ContainerHeaderDto32 dto = new ContainerHeaderDto32(
                (UInt32)header.FreePageAddress,
                header.PageSize,
                header.StorageVer,
                header.Reserved
            );
            Utils.Write<ContainerHeaderDto32>(stream, dto);
        }

        public virtual BlockHeader ReadBlockHeader(Stream stream)
        {
            var initialPosition = stream.Position;
            if (BlockHeader.TryRead<UInt32>(stream, out BlockHeader blockHeader))
            {
                return blockHeader;
            }
            throw new BlockHeaderExpected(initialPosition);
        }

        public virtual Stream WrapStream(Stream stream, bool create = false)
        {
            return stream;
        }

        public virtual ElementAddress ReadElementAddress(Stream stream)
        {
            var elementAddress = Utils.Read<ElementAddressDto32>(stream);
            return elementAddress.Cast();
        }

        public virtual void WriteElementAddress(Stream stream, ElementAddress elementAddress)
        {
            if (elementAddress.HeaderAddress > UInt32.MaxValue) throw new FileFormatException();
            if (elementAddress.DataAddress > UInt32.MaxValue) throw new FileFormatException();
            ElementAddressDto32 dto = new ElementAddressDto32(
                (UInt32)elementAddress.HeaderAddress,
                (UInt32)elementAddress.DataAddress,
                (UInt32)V8_FF_SIGNATURE);
            Utils.Write<ElementAddressDto32>(stream, dto);
        }

        public virtual void WriteBlockHeader(Stream stream, BlockHeader header)
        {
            BlockHeader.TryWrite<UInt32>(stream, header);
        }

    }

    public sealed class FormatReader64 : FormatReader
    {

        public static new readonly FormatReader64 Instance = new FormatReader64();
        
        public const ulong V8_OFFSET_80316 = 0x1359;  // волшебное смещение, откуда такая цифра неизвестно...
        
        public override ulong V8_FF_SIGNATURE { get; } = 0xffffffffffffffff;

        public override uint DEFAULT_PAGE_SIZE { get; } = 0x200;

        public override ContainerAddressType AddressType { get; } = ContainerAddressType._64bit;

        public override ContainerHeader ReadContainerHeader(Stream stream)
        {
            return Utils.Read<ContainerHeaderDto64>(stream).Cast();
        }

       public override BlockHeader ReadBlockHeader(Stream stream)
        {
            var initialPosition = stream.Position;
            if (BlockHeader.TryRead<UInt64>(stream, out BlockHeader blockHeader))
            {
                return blockHeader;
            }
            throw new BlockHeaderExpected(initialPosition, (long)V8_OFFSET_80316);
        }
        public override Stream WrapStream(Stream stream, bool create = false)
        {
            if (create)
            {
                for (int i = 0; i < (int)V8_OFFSET_80316; i++) stream.WriteByte(0);
            }
            return new OffsetBasedStream(stream, (long)V8_OFFSET_80316);
        }
        public override void WriteContainerHeader(Stream stream, ContainerHeader header)
        {
            ContainerHeaderDto64 dto = new ContainerHeaderDto64(
                header.FreePageAddress,
                header.PageSize,
                header.StorageVer,
                header.Reserved
            );
            Utils.Write<ContainerHeaderDto64>(stream, dto);
        }

        public override void WriteElementAddress(Stream stream, ElementAddress elementAddress)
        {
            if (elementAddress.HeaderAddress > UInt64.MaxValue) throw new FileFormatException();
            if (elementAddress.DataAddress > UInt64.MaxValue) throw new FileFormatException();
            ElementAddressDto64 dto = new ElementAddressDto64(
                (UInt64)elementAddress.HeaderAddress,
                (UInt64)elementAddress.DataAddress,
                (UInt64)V8_FF_SIGNATURE);
            Utils.Write<ElementAddressDto64>(stream, dto);
        }

        public override ElementAddress ReadElementAddress(Stream stream)
        {
            var elementAddress = Utils.Read<ElementAddressDto64>(stream);
            return elementAddress.Cast();
        }

        public override void WriteBlockHeader(Stream stream, BlockHeader header)
        {
            BlockHeader.TryWrite<UInt64>(stream, header);
        }
    }

}
