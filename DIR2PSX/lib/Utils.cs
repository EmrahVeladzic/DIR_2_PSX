using System.Text;

namespace DIR2PSX;

public static class Utils {
    public static void WriteString(byte[] buffer, Int32 offset, Int32 length, string value)
    {
        string padded = value.PadRight(length, ' ');
        byte[] bytes = Encoding.ASCII.GetBytes(padded.Substring(0, length));
        Array.Copy(bytes, 0, buffer, offset, length);
    }

    public static void WriteInt16LE(byte[] buffer, Int32 offset, UInt16 value)
    {
        buffer[offset + 0] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)(value >> 8);
    }
    public static void WriteInt16BE(byte[] buffer, Int32 offset, UInt16 value)
    {
        buffer[offset + 0] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }
    public static void WriteInt32LE(byte[] buffer, Int32 offset, UInt32 value)
    {
        buffer[offset + 0] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)(value >> 8 & 0xFF);
        buffer[offset + 2] = (byte)(value >> 16 & 0xFF);
        buffer[offset + 3] = (byte)(value >> 24);
    }
    public static void WriteInt32BE(byte[] buffer, Int32 offset, UInt32 value)
    {
        buffer[offset + 0] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16 & 0xFF);
        buffer[offset + 2] = (byte)(value >> 8 & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }


    public static void WriteInt16BothEndian(byte[] buffer, Int32 offset, UInt16 value)
    {
        WriteInt16LE(buffer,offset,value);
        WriteInt16BE(buffer,offset+2,value);
    }

    public static void WriteInt32BothEndian(byte[] buffer, Int32 offset, UInt32 value)
    {
        WriteInt32LE(buffer,offset,value);
        WriteInt32BE(buffer,offset+4,value);
    }

    public static string ReadString(byte[] data, int offset, int length)
    {
        return System.Text.Encoding.ASCII.GetString(data, offset, length);
    }

    static byte ToBcd(int value)
    {
        return (byte)(((value / 10) << 4) | (value % 10));
    }

    static UInt32 HeaderLBA(int lba, Mode md)
    {
        int absolute = lba + 150;

        byte m = ToBcd(absolute / 4500);
        byte s = ToBcd((absolute / 75) % 60);
        byte f = ToBcd(absolute % 75);

        return ((UInt32)m << 24)
            | ((UInt32)s << 16)
            | ((UInt32)f << 8)
            | ((md==Mode.MODE_1)?0x1u:0x2u);
    }

    public static void WriteSyncAndHeader(int lba, Mode md, Span<byte> sector)
    {
        ISO_Buffer.Sync.CopyTo(sector);
        UInt32 header = HeaderLBA(lba,md);
        sector[12]=(byte)((header>>24)&0XFF);
        sector[13]=(byte)((header>>16)&0XFF);
        sector[14]=(byte)((header>>8)&0XFF);
        sector[15]=(byte)(header&0XFF);
    }

    public static UInt32[] BuildEDCTable()
    {
        UInt32 variant = 0xD8018001;

        UInt32[] table = new UInt32[256];

        for(UInt32 i = 0; i<256; i++)
        {
            UInt32 crc = i;
            for(UInt32 j = 0; j<8; j++)
            {
                crc = (crc >> 1) ^ ((crc & 0x1) != 0 ? variant : 0);
            }
            table[i]=crc;
        }
        return table;
    }

    public static UInt32 ComputeEDC(Span<byte> sector, Int32 offset, Int32 length)
    {
        UInt32 crc = 0;
        for (int i = offset; i < offset + length; i++)
        {
            crc = (crc >> 8) ^ ISO_Buffer.EDC_TABLE[(crc ^ sector[i]) & 0xFF];
        }
        return crc;
    }


    public static void ComputeECC(Span<byte> sector, Mode mode)
    {
        Int32 start = 12;

        if(mode != Mode.MODE_1){start = 16;}

        
        for (Int32 major = 0; major < 43; major++)
        {
            Int32 index = major;
            byte a = 0, b = 0;
            for (Int32 minor = 0; minor < 24; minor++)
            {
                byte temp = sector[start + index];
                index += 43;
                a ^= temp;
                b ^= temp;
                a = ISO_Buffer.ECC_F_LUT[a];
            }
            a = ISO_Buffer.ECC_B_LUT[ISO_Buffer.ECC_F_LUT[a] ^ b];
            b = (byte)(a ^ b);
            sector[2076 + major * 2] = a;
            sector[2076 + major * 2 + 1] = b;
        }


        for (Int32 major = 0; major < 26; major++)
        {
            Int32 index = major;
            byte a = 0, b = 0;
            for (Int32 minor = 0; minor < 43; minor++)
            {
                byte temp = sector[start + index];
                index += 44;
                if (index >= 43 * 44) index -= 43 * 44;
                a ^= temp;
                b ^= temp;
                a = ISO_Buffer.ECC_F_LUT[a];
            }
            a = ISO_Buffer.ECC_B_LUT[ISO_Buffer.ECC_F_LUT[a] ^ b];
            b = (byte)(a ^ b);
            sector[2248 + major * 2] = a;
            sector[2248 + major * 2 + 1] = b;
        }
    }


    public static byte[] GetPathTable(bool big_endian = false)
    {
        List<byte> output = new();

        foreach(GameDirectory gd in Directory_Mapper.DirectoryTable)
        {
            Int32 length = gd.Name.Length+8;
            if ((length & 0x1) == 1)
            {
                length++;
            }
            byte[] entry = new byte[length];

            entry[0]=(byte)gd.Name.Length;

            if (big_endian)
            {
                WriteInt32BE(entry,2,(UInt32)gd.LBA);
                WriteInt16BE(entry,6,(UInt16)(gd.Parent?.TableIndex??gd.TableIndex));
            }
            else
            {
                WriteInt32LE(entry,2,(UInt32)gd.LBA);
                WriteInt16LE(entry,6,(UInt16)(gd.Parent?.TableIndex??gd.TableIndex));
            }
            WriteString(entry,8,gd.Name.Length,gd.Name);

            output.AddRange(entry);
        }

        return output.ToArray();
    }



}