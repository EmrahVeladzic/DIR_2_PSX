using System.Text;

namespace DIR2PSX;

public class EXEResult
{
    public byte[]? Data {get; set;} = null;
    public string? Error {get; set;} = null;
}

public static class EXE_Creator
{
    public static readonly Int32 PADDING_HEADER_SIZE = 2048;

    public static string Stack {get; set;}="";

    public static UInt32 GetSymbolAddress(byte[] elf, string symbol)
    {
        UInt32 e_shoff = BitConverter.ToUInt32(elf,0x20);
        UInt16 e_shentsize = BitConverter.ToUInt16(elf,0x2E);
        UInt16 e_shnum = BitConverter.ToUInt16(elf,0x30);

        UInt32 e_symtaboff = 0x0;
        UInt32 e_symtabsize = 0x0;
        UInt32 e_strtaboff = 0x0;

        for (int i = 0; i < e_shnum; i++)
        {
            int shBase  = (int)(e_shoff + i * e_shentsize);
            UInt32 e_shtype = BitConverter.ToUInt32(elf, shBase + 0x04);
            UInt32 shoff  = BitConverter.ToUInt32(elf, shBase + 0x10);
            UInt32 shsize = BitConverter.ToUInt32(elf, shBase + 0x14);
            UInt32 shlink = BitConverter.ToUInt32(elf, shBase + 0x18);

            if (e_shtype == 0x2) 
            {
                e_symtaboff = shoff;
                e_symtabsize   = shsize;

                int strshbase = (int)(e_shoff + shlink * e_shentsize);
                e_strtaboff  = BitConverter.ToUInt32(elf, strshbase + 0x10);
            }
        }

        if (e_symtaboff == 0) {return 0;}

        int symcount = (int)(e_symtabsize / 16);
        for (int i = 0; i < symcount; i++)
        {
            int symbase  = (int)(e_symtaboff + i * 16);
            UInt32 nameidx = BitConverter.ToUInt32(elf, symbase + 0x00);
            UInt32 value   = BitConverter.ToUInt32(elf, symbase + 0x04);

            string name = Encoding.ASCII.GetString(elf, (int)(e_strtaboff + nameidx), symbol.Length + 1);
            if (name.TrimEnd('\0') == symbol) { return value; }
        }

        return 0;
    }

    public static EXEResult GetEXE (byte[] elf, string title)
    {
        EXEResult output = new();

        if (elf[0] != 0x7F || Utils.ReadString(elf, 1, 3) != "ELF")
        {
            output.Error="Invalid ELF format.";
            return output;
        }

        if (elf[4] != 0x1|| elf[5]!=0x1)
        {
            output.Error="ELF must be 32-bit, LE.";
            return output;
        }

        UInt32 _gp = GetSymbolAddress(elf,"_gp");
        if (_gp == 0)
        {
            output.Error="Could not find _gp.";
            return output;
        }

        UInt32 _sp = GetSymbolAddress(elf,"_stackTop");
        if (_sp == 0)
        {
            output.Error = "Could not find _stackTop.";
            return output;
        }

        Stack = _sp.ToString("X8");

        UInt32 e_entry = BitConverter.ToUInt32(elf,0x18);
        UInt32 e_phoff = BitConverter.ToUInt32(elf,0x1C);
        UInt16 e_phentsize = BitConverter.ToUInt16(elf,0x2A);
        UInt16 e_phnum = BitConverter.ToUInt16(elf,0x2C);

        if (e_phentsize == 0 || e_phnum == 0)
        {
            output.Error = "No program headers.";
            return output;
        }


        uint lo_addr = uint.MaxValue;
        uint hi_end = 0;

 
        for (int i = 0; i < e_phnum; i++)
        {
            int e_phbase = (int)(e_phoff + i * e_phentsize);
            UInt32 ptype   = BitConverter.ToUInt32(elf, e_phbase + 0x00);
            UInt32 pfilesz = BitConverter.ToUInt32(elf, e_phbase + 0x10);
            UInt32 ppaddr  = BitConverter.ToUInt32(elf, e_phbase + 0x0C);

            if (ptype != 1 || pfilesz == 0){ continue;}
            if (ppaddr < lo_addr) lo_addr = ppaddr;
            if (ppaddr + pfilesz > hi_end) hi_end = ppaddr + pfilesz;
        }

        if (lo_addr == uint.MaxValue)
        {
            output.Error = "No loadable segments found.";
            return output;
        }

        uint binsize = hi_end - lo_addr;

        byte[] binary = new byte[binsize];

        for (int i = 0; i < e_phnum; i++)
        {
            int e_phbase = (int)(e_phoff + i * e_phentsize);
            UInt32 ptype   = BitConverter.ToUInt32(elf, e_phbase + 0x00);
            UInt32 poff = BitConverter.ToUInt32(elf, e_phbase + 0x04);
            UInt32 ppaddr  = BitConverter.ToUInt32(elf, e_phbase + 0x0C);
            UInt32 pfilesz = BitConverter.ToUInt32(elf, e_phbase + 0x10);

            if (ptype != 1 || pfilesz == 0){ continue;}

            uint destoff = ppaddr - lo_addr;
            Array.Copy(elf, (int)poff, binary, (int)destoff, (int)pfilesz);
        }

        Int32 padded_size = (binary.Length + (PADDING_HEADER_SIZE-1)) & ~(PADDING_HEADER_SIZE-1);
        byte[] padded_bin = new byte[padded_size];
        Array.Copy(binary, padded_bin, binary.Length);

        byte[] header = new byte[PADDING_HEADER_SIZE];

        Utils.WriteString(header,0,8,"PS-X EXE");

        Array.Copy(BitConverter.GetBytes(e_entry), 0, header, 0x10, 4);

        Array.Copy(BitConverter.GetBytes(_gp), 0, header, 0x14, 4);

        Array.Copy(BitConverter.GetBytes(lo_addr), 0, header, 0x18, 4);

        Array.Copy(BitConverter.GetBytes((UInt32)padded_size), 0, header, 0x1C, 4);
                

        Array.Copy(BitConverter.GetBytes(_sp), 0, header, 0x30, 4);

        byte[] titleBytes = Encoding.ASCII.GetBytes(title);
        Array.Copy(titleBytes, 0, header, 0x54, Math.Min(titleBytes.Length, 1972));


        byte[] result = new byte[2048 + padded_size];
        Array.Copy(header, 0, result, 0, 2048);
        Array.Copy(padded_bin, 0, result, 2048, padded_size);

        output.Data=result;





        return output;
    }

    


}