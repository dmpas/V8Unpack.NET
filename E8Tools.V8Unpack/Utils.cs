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
using System.Text;

namespace E8Tools.V8Unpack
{
    internal static class Utils
    {

        public static Encoding NamesEncoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);

        public static DateTime FromFileDate(UInt64 serializedDate)
        {
            var ticks = (long)serializedDate * 1000;
            if (ticks >= DateTime.MinValue.Ticks &&  ticks <= DateTime.MaxValue.Ticks)
                return new DateTime(ticks);
            return DateTime.MinValue;
        }

        public static ulong ToFileDate(DateTime dateTime)
        {
            return (ulong)(dateTime.Ticks / 1000);
        }

        public static T Read<T>(Stream stream) where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            var buffer = new byte[size];
            var bytesRead = stream.Read(buffer, 0, size);
            if (bytesRead == size)
            {
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    var ptr = handle.AddrOfPinnedObject();
                    var data = (T)Marshal.PtrToStructure(ptr, typeof(T));
                    return data;
                }
                finally
                {
                    handle.Free();
                }
            }
            throw new FileFormatException();
        }

        public static void Write<T>(Stream stream, T value) where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            var buffer = new byte[size];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                Marshal.StructureToPtr(value, ptr, false);
                stream.Write(buffer, 0, size);
            }
            finally
            {
                handle.Free();
            }
        }

        public static void WriteElementHeader(Stream stream, ElementHeaderData data)
        {
            ElementHeaderDataDto header = new ElementHeaderDataDto(
                ToFileDate(data.DateCreation),
                ToFileDate(data.DateModification),
                data.Version
            );
            Write<ElementHeaderDataDto>(stream, header);
            var nameAsBytes = NamesEncoding.GetBytes(data.Name);
            stream.Write(nameAsBytes, 0, nameAsBytes.Length);

            // столько действительно нужно
            stream.WriteByte(0);
            stream.WriteByte(0);
            stream.WriteByte(0);
            stream.WriteByte(0);
        }

        public static ElementHeaderData ReadElementHeader(Stream stream)
        {
            ElementHeaderDataDto header = Utils.Read<ElementHeaderDataDto>(stream);
            var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
            var buffer = new byte[4096];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            var name = encoding.GetString(buffer, 0, bytesRead).TrimEnd('\0');
            return new ElementHeaderData(name,
                FromFileDate(header.DateCreation),
                FromFileDate(header.DateModification),
                header.Version);
        }

        public static bool ReadCertainChar(Stream stream, byte character)
        {
            return stream.ReadByte() == character;
        }

        public static UInt64 ReadUIntFromHexString<T>(Stream stream)
        {
            var size = Marshal.SizeOf(typeof(T)) * 2;
            var buffer = new byte[size];
            var bytesRead = stream.Read(buffer, 0, size);
            if (bytesRead == size)
            {
                UInt64 result = 0;
                for (int i = 0; i < size; i++)
                {
                    result = (result << 4) | (byte)FromHexDigit(buffer[i]);
                }
                return result;
            }
            throw new FileFormatException();
        }

        public static void WriteUIntAsHexString<T>(Stream stream, UInt64 value)
        {
            var size = Marshal.SizeOf(typeof(T)) * 2;
            var buffer = new byte[size];
            for (int i = 0; i < size; i++)
            {
                buffer[size - i - 1] = ToHexDigit((int)(value & 0xf));
                value >>= 4;
            }
            if (value != 0)
            {
                throw new FileFormatException();
            }
            stream.Write(buffer, 0, size);
        }

        public static byte ToHexDigit(int value)
        {
            if (value > 15)
                throw new FileFormatException();
            if (value >= 10)
                return (byte)('a' + value - 10);
            if (value >= 0)
                return (byte)('0' + value);
            throw new FileFormatException();
        }

        public static int FromHexDigit(byte hexByte)
        {
            if (hexByte >= (byte)'0' && hexByte <= (byte)'9') return hexByte - (byte)'0';
            if (hexByte >= (byte)'a' && hexByte <= (byte)'f') return hexByte - (byte)'a' + 10;
            if (hexByte >= (byte)'A' && hexByte <= (byte)'F') return hexByte - (byte)'A' + 10;
            throw new FileFormatException();
        }

    }
}
