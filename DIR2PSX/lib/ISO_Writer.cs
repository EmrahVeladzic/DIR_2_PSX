
namespace DIR2PSX;

public static class ISO_Writer
{    
    public static Int32 CurrentIndex {get; set;} = 18;

    public static Int32 SectorsNeeded(Int32 byte_length, bool raw = false)
    {
        Int32 sector_size = raw ? ISO_Buffer.RAW_SECTOR_SIZE : ISO_Buffer.USER_SECTOR_SIZE;
        return (byte_length + sector_size - 1) / sector_size;
    }

    public static string? WriteData(byte[] data)
    {
        if (ISO_Buffer.SECTOR_COUNT-CurrentIndex<SectorsNeeded(data.Length,false))
        {
            return "Out of sectors to write to.";
        }


        return null;
    }

    public static string? WriteRaw(byte[] data)
    {
        if (ISO_Buffer.SECTOR_COUNT-CurrentIndex<SectorsNeeded(data.Length,true))
        {
            return "Out of sectors to write to.";
        }



        return null;
    }

    public static string? WritePVD()
    {
        if (Directory_Mapper.Root == null || Directory_Mapper.Program == null)
        {
            return "Mapper state corruption. Please do not modify the subfolder during execution.";
        }

        byte[] pvd = new byte[ISO_Buffer.USER_SECTOR_SIZE];

        pvd[0] = 0x1;

        Utils.WriteString(pvd,1,5,"CD001");

        pvd[6]=0x1;

        Utils.WriteString(pvd,8,32,"PLAYSTATION");

        Utils.WriteString(pvd,40,32,Directory_Mapper.Root.Name);
        
        

        return WriteData(pvd);
    }




  

}

