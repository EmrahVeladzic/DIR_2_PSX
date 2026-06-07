
namespace DIR2PSX;

public static class ISO_Writer
{    
    public static Int32 CurrentIndex {get; set;} = 18;

    public static Int32 EOF {get; set;} = 0;

    public static Int32 SectorsNeeded(Int32 byte_length, bool fmv = false)
    {
        Int32 sector_size = fmv ? ISO_Buffer.FMV_SECTOR_SIZE : ISO_Buffer.USER_SECTOR_SIZE;
        return (byte_length + sector_size - 1) / sector_size;
    }

    public static string? WriteMode1(byte[] data)
    {
        Int32 needed = SectorsNeeded(data.Length,false);

        if (ISO_Buffer.SECTOR_COUNT-CurrentIndex<needed)
        {
            return "Out of sectors to write to.";
        }

        for(Int32 i = 0; i<needed; i++)
        {
            Span<byte> raw_sector = ISO_Buffer.GetSector(CurrentIndex+i);
            Utils.WriteSyncAndHeader(CurrentIndex+i,Mode.MODE_1,raw_sector);

            int offset = i * ISO_Buffer.USER_SECTOR_SIZE;
            int remaining = Math.Min(ISO_Buffer.USER_SECTOR_SIZE, data.Length - offset);
            data.AsSpan(offset, remaining).CopyTo(raw_sector.Slice(16));

            UInt32 edc = Utils.ComputeEDC(raw_sector, 0, 2064);
            raw_sector[2064] = (byte)(edc & 0xFF);
            raw_sector[2065] = (byte)((edc >> 8) & 0xFF);
            raw_sector[2066] = (byte)((edc >> 16) & 0xFF);
            raw_sector[2067] = (byte)((edc >> 24) & 0xFF);
            Utils.ComputeECC(raw_sector,Mode.MODE_1);
        }

        CurrentIndex+=needed;

        return null;
    }

    public static string? WriteForm1(byte[] data)
    {
        Int32 needed = SectorsNeeded(data.Length,false);

        if (ISO_Buffer.SECTOR_COUNT-CurrentIndex<needed)
        {
            return "Out of sectors to write to.";
        }

        for(Int32 i = 0; i<needed; i++)
        {
            Span<byte> raw_sector = ISO_Buffer.GetSector(CurrentIndex+i);
            Utils.WriteSyncAndHeader(CurrentIndex+i,Mode.MODE_2F1,raw_sector);

            raw_sector[18]=0x8;

            raw_sector[22]=0x8;

            int offset = i * ISO_Buffer.USER_SECTOR_SIZE;
            int remaining = Math.Min(ISO_Buffer.USER_SECTOR_SIZE, data.Length - offset);
            data.AsSpan(offset, remaining).CopyTo(raw_sector.Slice(24));

            UInt32 edc = Utils.ComputeEDC(raw_sector, 16, 2064);
            raw_sector[2064] = (byte)(edc & 0xFF);
            raw_sector[2065] = (byte)((edc >> 8) & 0xFF);
            raw_sector[2066] = (byte)((edc >> 16) & 0xFF);
            raw_sector[2067] = (byte)((edc >> 24) & 0xFF);
            Utils.ComputeECC(raw_sector,Mode.MODE_2F1);
        }

        CurrentIndex+=needed;

        return null;
    }

    public static string? WriteForm2(byte[] data)
    {
        Int32 needed = SectorsNeeded(data.Length,true);

        if (ISO_Buffer.SECTOR_COUNT-CurrentIndex<needed)
        {
            return "Out of sectors to write to.";
        }


        CurrentIndex+=needed;

        return "Not currently supported.";
    }

    public static string? WriteDirectory()
    {
        if (Directory_Mapper.Root == null || Directory_Mapper.Program == null || !Directory_Mapper.Root.Files.Any(f => ReferenceEquals(f, Directory_Mapper.CNF))||!Directory_Mapper.Root.Files.Any(f => ReferenceEquals(f, Directory_Mapper.Program)))
        {
            return "Mapper state corruption. Please do not modify the subfolder during execution.";
        }

        string? err = null;

        for (int i = 0; i < 16; i++)
        {
            Span<byte> sector = ISO_Buffer.GetSector(i);
            Utils.WriteSyncAndHeader(i, Mode.MODE_1, sector);
            
            UInt32 edc = Utils.ComputeEDC(sector, 0, 2064);
            sector[2064] = (byte)(edc & 0xFF);
            sector[2065] = (byte)((edc >> 8) & 0xFF);
            sector[2066] = (byte)((edc >> 16) & 0xFF);
            sector[2067] = (byte)((edc >> 24) & 0xFF);
            Utils.ComputeECC(sector, Mode.MODE_1);
        }

        CurrentIndex = Directory_Mapper.FileStartingSector;

        UInt32 BEIndex =(UInt32)( 18 + SectorsNeeded(Directory_Mapper.PathTableSize));

        foreach(GameDirectory gd in Directory_Mapper.DirectoryTable)
        {
            foreach(IFileEntry file in gd.Files)
            {
                file.LBA=CurrentIndex;

                err = WriteForm1(file.Data);

                if(err!=null){return err;}
            }


        }

        EOF = CurrentIndex;

        foreach(GameDirectory gd in Directory_Mapper.DirectoryTable)
        {
            CurrentIndex = gd.LBA;

            byte[] record = gd.ToRecord();
            if (record.Length > ISO_Buffer.USER_SECTOR_SIZE)
            {
                throw new Exception();
            }

            WriteForm1(record);


        }

        byte[] le_table = Utils.GetPathTable(false);
        byte[] be_table = Utils.GetPathTable(true);

        if (le_table.Length != be_table.Length || le_table.Length != Directory_Mapper.PathTableSize)
        {
            return "Path table size calculation error.";
        }

        CurrentIndex = 18;

        WriteForm1(le_table);
        WriteForm1(be_table);

        CurrentIndex = 16;

        byte[] pvd_term = new byte[ISO_Buffer.USER_SECTOR_SIZE*2];

        pvd_term[0]=0x1;
        Utils.WriteString(pvd_term,1,5,"CD001");
        pvd_term[6]=0x1;

        Utils.WriteString(pvd_term,8,32,"PLAYSTATION".PadRight(32));

        string title = Directory_Mapper.Program.Name.Split(".")[0].ToUpperInvariant();

        Utils.WriteString(pvd_term,40,32,title.PadRight(32));

        Utils.WriteInt32BothEndian(pvd_term,80,(UInt32)EOF);

        Utils.WriteInt16BothEndian(pvd_term,120,0x1);

        Utils.WriteInt16BothEndian(pvd_term,124,0x1);

        Utils.WriteInt16BothEndian(pvd_term,128,(UInt16)ISO_Buffer.USER_SECTOR_SIZE);

        Utils.WriteInt32BothEndian(pvd_term,132,(UInt32)Directory_Mapper.PathTableSize);

        Utils.WriteInt32LE(pvd_term,140,18);

        Utils.WriteInt32BE(pvd_term,148,BEIndex);

        byte[] root = Directory_Mapper.Root.ToRecordEntry();
        if (root.Length > 34)
        {
            return "Root data corruption.";
        }

        for(int i = 0; i<34; i++)
        {
            pvd_term[156+i]=root[i];
        }

        for(int i = 318; i<813; i++)
        {
            pvd_term[i]=0x20;
        }

        pvd_term[881]=0x1;

        pvd_term[ISO_Buffer.USER_SECTOR_SIZE]=0xFF;

        Utils.WriteString(pvd_term,ISO_Buffer.USER_SECTOR_SIZE+1,5,"CD001");

        pvd_term[ISO_Buffer.USER_SECTOR_SIZE+6]=0x1;


        WriteForm1(pvd_term);

        return err;
        
    }




  

}

